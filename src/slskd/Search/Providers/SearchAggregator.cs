// <copyright file="SearchAggregator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Search.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using slskd.Search;
using ILogger = Serilog.ILogger;

/// <summary>
///     Aggregates search results from multiple providers (Pod and Scene) with deduplication.
/// </summary>
public class SearchAggregator
{
    private readonly ILogger _logger;
    private readonly string _preferredPrimarySource; // "pod" or "scene", default "pod"

    public SearchAggregator(ILogger logger, string preferredPrimarySource = "pod")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _preferredPrimarySource = preferredPrimarySource;
    }

    /// <summary>
    ///     Merges search results from multiple providers with deduplication.
    /// </summary>
    /// <param name="results">All search results from all providers.</param>
    /// <returns>Merged and deduplicated results.</returns>
    public List<SearchResult> MergeResults(IEnumerable<SearchResult> results)
    {
        var resultsList = results.ToList();
        if (resultsList.Count == 0)
        {
            return new List<SearchResult>();
        }

        // Deduplication keys (ordered by priority):
        // 1. Hash (exact match)
        // 2. Normalized filename + size
        // 3. Optional metadata fields (duration, bitrate)

        var seenByHash = new Dictionary<string, SearchResult>();
        var seenByFilename = new Dictionary<(string NormalizedFilename, long Size), SearchResult>();
        var merged = new List<SearchResult>();

        string NormalizeFilename(string filename)
        {
            return filename?.ToLowerInvariant()
                .Replace('\\', '/')
                .Trim() ?? string.Empty;
        }

        // Deduplicate by response (username + first file) rather than individual files
        // This matches the existing SearchResponseMerger behavior
        foreach (var result in resultsList)
        {
            if (result.Response?.Files == null || !result.Response.Files.Any())
            {
                continue;
            }

            // Use first file for deduplication key
            // For cross-provider deduplication, we ignore username (pod and scene may have different usernames)
            // and match on normalized filename + size only
            var firstFile = result.Response.Files.First();
            var normalizedFilename = NormalizeFilename(firstFile.Filename);
            var key = (normalizedFilename, firstFile.Size);

            if (seenByFilename.TryGetValue(key, out var existingResult))
            {
                // Merge: add provider to SourceProviders, update PrimarySource if preferred
                if (!existingResult.SourceProviders.Contains(result.Provider))
                {
                    existingResult.SourceProviders.Add(result.Provider);
                }

                // Prefer pod as primary source if available
                if (result.Provider == _preferredPrimarySource && existingResult.PrimarySource != _preferredPrimarySource)
                {
                    existingResult.PrimarySource = _preferredPrimarySource;
                }

                // Merge ContentRefs (keep both if different)
                if (result.PodContentRef != null && existingResult.PodContentRef == null)
                {
                    existingResult.PodContentRef = result.PodContentRef;
                    existingResult.PeerHint = result.PeerHint;
                }

                if (result.SceneContentRef != null && existingResult.SceneContentRef == null)
                {
                    existingResult.SceneContentRef = result.SceneContentRef;
                    existingResult.SceneUserHint = result.SceneUserHint;
                }
            }
            else
            {
                // New result - add to merged list
                seenByFilename[key] = result;
                merged.Add(result);
            }
        }

        // Update PrimarySource for merged results
        foreach (var result in merged)
        {
            if (result.SourceProviders.Count > 1)
            {
                // Multiple sources - prefer configured primary source if available
                if (result.SourceProviders.Contains(_preferredPrimarySource))
                {
                    result.PrimarySource = _preferredPrimarySource;
                }
                else
                {
                    result.PrimarySource = result.SourceProviders.First();
                }
            }
            else if (result.SourceProviders.Count == 1)
            {
                // Single source - use that as primary
                result.PrimarySource = result.SourceProviders.First();
            }
        }

        _logger.Debug("[SearchAggregator] Merged {InputCount} results into {OutputCount} unique results", resultsList.Count, merged.Count);

        return merged;
    }

    /// <summary>
    ///     Starts searches from multiple providers in parallel and aggregates results.
    /// </summary>
    /// <param name="providers">The search providers to use.</param>
    /// <param name="request">The search request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated search results.</returns>
    public async Task<List<SearchResult>> AggregateAsync(
        IEnumerable<ISearchProvider> providers,
        SearchRequest request,
        CancellationToken ct)
    {
        var providersList = providers.ToList();
        if (providersList.Count == 0)
        {
            return new List<SearchResult>();
        }

        var sink = new CollectingSearchResultSink();
        var tasks = providersList.Select(provider =>
            provider.StartSearchAsync(request, sink, ct)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.Debug(t.Exception?.GetBaseException(), "[SearchAggregator] Provider {Provider} failed: {Message}",
                            provider.Name, t.Exception?.GetBaseException()?.Message);
                    }
                }, ct));

        await Task.WhenAll(tasks);

        return MergeResults(sink.Results);
    }

    /// <summary>
    ///     Simple collecting sink that stores all results.
    /// </summary>
    private class CollectingSearchResultSink : ISearchResultSink
    {
        private readonly List<SearchResult> _results = new();

        public void AddResult(SearchResult result)
        {
            _results.Add(result);
        }

        public List<SearchResult> Results => _results;
    }
}
