// <copyright file="SceneSearchProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Search.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;
using slskd.Search;
using Soulseek;
using SearchOptions = Soulseek.SearchOptions;
using SearchQuery = Soulseek.SearchQuery;
using SearchScope = Soulseek.SearchScope;

/// <summary>
///     Search provider for Soulseek Scene (wraps existing Soulseek search logic).
/// </summary>
public class SceneSearchProvider : ISearchProvider
{
    private readonly ISoulseekClient _soulseekClient;
    private readonly ISoulseekSafetyLimiter _safetyLimiter;
    private readonly ILogger<SceneSearchProvider> _logger;

    public SceneSearchProvider(
        ISoulseekClient soulseekClient,
        ISoulseekSafetyLimiter safetyLimiter,
        ILogger<SceneSearchProvider> logger)
    {
        _soulseekClient = soulseekClient ?? throw new ArgumentNullException(nameof(soulseekClient));
        _safetyLimiter = safetyLimiter ?? throw new ArgumentNullException(nameof(safetyLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "scene";

    public async Task StartSearchAsync(SearchRequest request, ISearchResultSink sink, CancellationToken ct)
    {
        // H-08: Check Soulseek safety caps before initiating search
        if (!_safetyLimiter.TryConsumeSearch("scene-provider"))
        {
            _logger.LogWarning("[SceneProvider] Search rejected for query='{Query}': rate limit exceeded", request.SearchText);
            return; // Silently fail - don't block pod provider
        }

        try
        {
            var query = SearchQuery.FromText(request.SearchText);
            var scope = SearchScope.Network;

            var searchOptions = new SearchOptions(
                searchTimeout: (request.TimeoutSeconds ?? 15) * 1000,
                responseLimit: request.ResponseLimit ?? 100,
                fileLimit: request.FileLimit ?? 10000,
                filterResponses: true,
                minimumResponseFileCount: 1);

            var responses = new List<Soulseek.SearchResponse>();

            // Use timeout to prevent blocking pod provider
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds ?? 15));

            await _soulseekClient.SearchAsync(
                query,
                responseHandler: (response) => responses.Add(response),
                scope,
                token: _soulseekClient.GetNextToken(),
                options: searchOptions,
                cancellationToken: timeoutCts.Token);

            // Convert Soulseek responses to SearchResult with provenance
            // Group files by response to create one SearchResult per response
            foreach (var response in responses)
            {
                var firstFile = response.Files.FirstOrDefault();
                if (firstFile == null)
                {
                    continue;
                }

                var responseObj = Response.FromSoulseekSearchResponse(response);
                // Attach provenance to Response
                responseObj.SourceProviders = new List<string> { "scene" };
                responseObj.PrimarySource = "scene";
                responseObj.SceneContentRef = new SceneContentRef
                {
                    Username = response.Username,
                    Filename = firstFile.Filename,
                    Size = firstFile.Size
                };

                var searchResult = new SearchResult
                {
                    Provider = "scene",
                    SourceProviders = new List<string> { "scene" },
                    PrimarySource = "scene",
                    Response = responseObj,
                    SceneContentRef = responseObj.SceneContentRef,
                    SceneUserHint = response.Username
                };

                sink.AddResult(searchResult);
            }

            _logger.LogDebug("[SceneProvider] Search completed for '{Query}': {Count} responses", request.SearchText, responses.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("[SceneProvider] Search cancelled for '{Query}'", request.SearchText);
            throw;
        }
        catch (Exception ex)
        {
            // Don't block pod provider - log and continue
            _logger.LogDebug(ex, "[SceneProvider] Search failed for '{Query}': {Message}", request.SearchText, ex.Message);
        }
    }
}
