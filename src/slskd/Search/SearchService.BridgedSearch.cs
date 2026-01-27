// <copyright file="SearchService.BridgedSearch.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Search;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using slskd.Search.API;
using slskd.Search.Providers;
using Soulseek;
using SearchOptions = Soulseek.SearchOptions;
using SearchQuery = Soulseek.SearchQuery;
using SearchScope = Soulseek.SearchScope;
using SearchStates = Soulseek.SearchStates;
using ILogger = Serilog.ILogger;

/// <summary>
///     Scene ↔ Pod Bridging search implementation.
/// </summary>
public partial class SearchService
{
    /// <summary>
    ///     Starts a bridged search using Scene ↔ Pod Bridging providers.
    /// </summary>
    private async Task<Search> StartBridgedSearchAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null, List<ISearchProvider> providersToUse = null)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        CancellationTokens.TryAdd(id, cancellationTokenSource);

        // Initialize search record
        var search = new Search()
        {
            SearchText = query.SearchText,
            Token = 0, // Not using Soulseek token for bridged searches
            Id = id,
            State = SearchStates.Requested,
            StartedAt = DateTime.UtcNow,
        };

        bool searchCreated = false;
        bool searchBroadcasted = false;

        try
        {
            using var context = ContextFactory.CreateDbContext();
            context.Add(search);
            context.SaveChanges();

            searchCreated = true;

            await SearchHub.BroadcastCreateAsync(search);

            searchBroadcasted = true;

            // Create aggregator and provider request
            var aggregator = new SearchAggregator(
                Log.ForContext<SearchAggregator>(),
                preferredPrimarySource: "pod");
            var providerRequest = new Providers.SearchRequest
            {
                SearchText = query.SearchText,
                TimeoutSeconds = options?.SearchTimeout / 1000 ?? 15,
                ResponseLimit = options?.ResponseLimit ?? 100,
                FileLimit = options?.FileLimit ?? 10000
            };

            // Aggregate results from requested providers (or all if not specified)
            var providers = providersToUse ?? SearchProviders;
            var aggregatedResults = await aggregator.AggregateAsync(providers, providerRequest, cancellationTokenSource.Token);

            // Convert SearchResult back to Response format for compatibility
            // Responses already have provenance attached by providers
            var responses = aggregatedResults.Select(r => r.Response).ToList();

            // Update search record
            search.State = SearchStates.Completed;
            search.EndedAt = DateTime.UtcNow;
            search.Responses = responses;
            search.ResponseCount = responses.Count;
            search.FileCount = responses.Sum(r => r.FileCount);
            search.LockedFileCount = responses.Sum(r => r.LockedFileCount);

            Update(search);
            await SearchHub.BroadcastUpdateAsync(search);

            Log.Information("[ScenePodBridge] Bridged search completed for '{Query}': {ResponseCount} responses, {FileCount} files (id: {Id})",
                query.SearchText, search.ResponseCount, search.FileCount, id);

            return search;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.Token.IsCancellationRequested)
        {
            Log.Information("[ScenePodBridge] Search for '{Query}' was cancelled", query.SearchText);
            search.State = SearchStates.Completed | SearchStates.Cancelled;
            search.EndedAt = DateTime.UtcNow;
            Update(search);
            await SearchHub.BroadcastUpdateAsync(search);
            return search;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ScenePodBridge] Failed to execute bridged search for '{Query}': {Message}", query.SearchText, ex.Message);
            search.State = SearchStates.Completed | SearchStates.Errored;
            search.EndedAt = DateTime.UtcNow;
            Update(search);
            await SearchHub.BroadcastUpdateAsync(search);
            return search;
        }
        finally
        {
            CancellationTokens.TryRemove(id, out _);
            cancellationTokenSource.Dispose();
        }
    }
}
