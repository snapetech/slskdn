// <copyright file="PodSearchProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Search.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.DhtRendezvous.Search;
using slskd.Search;

/// <summary>
///     Search provider for Pod/Mesh network.
/// </summary>
public class PodSearchProvider : ISearchProvider
{
    private readonly IMeshOverlaySearchService _meshOverlaySearchService;
    private readonly ILogger<PodSearchProvider> _logger;

    public PodSearchProvider(
        IMeshOverlaySearchService meshOverlaySearchService,
        ILogger<PodSearchProvider> logger)
    {
        _meshOverlaySearchService = meshOverlaySearchService ?? throw new ArgumentNullException(nameof(meshOverlaySearchService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "pod";

    public async Task StartSearchAsync(SearchRequest request, ISearchResultSink sink, CancellationToken ct)
    {
        try
        {
            // Use mesh overlay search service to query pod peers
            var responses = await _meshOverlaySearchService.SearchAsync(request.SearchText, ct);

            // Convert mesh responses to SearchResult with provenance
            foreach (var response in responses)
            {
                // Extract ContentId from filename if available (mesh search may include it)
                // For now, we'll use filename as the identifier
                var firstFile = response.Files?.FirstOrDefault();
                if (firstFile == null)
                {
                    continue;
                }

                // Try to extract ContentId from filename or use filename as identifier
                // In a full implementation, mesh search would return ContentId directly
                var contentId = ExtractContentIdFromFilename(firstFile.Filename) ?? $"pod:{response.Username}:{firstFile.Filename}";

                // Attach provenance to Response
                response.SourceProviders = new List<string> { "pod" };
                response.PrimarySource = "pod";
                response.PodContentRef = new PodContentRef
                {
                    ContentId = contentId,
                    Hash = null // Mesh search may provide hash in future
                };

                var searchResult = new SearchResult
                {
                    Provider = "pod",
                    SourceProviders = new List<string> { "pod" },
                    PrimarySource = "pod",
                    Response = response,
                    PodContentRef = response.PodContentRef,
                    PeerHint = response.Username // Internal use only - never exposed to scene
                };

                sink.AddResult(searchResult);
            }

            _logger.LogDebug("[PodProvider] Search completed for '{Query}': {Count} responses", request.SearchText, responses.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("[PodProvider] Search cancelled for '{Query}'", request.SearchText);
            throw;
        }
        catch (Exception ex)
        {
            // Don't block scene provider - log and continue
            _logger.LogDebug(ex, "[PodProvider] Search failed for '{Query}': {Message}", request.SearchText, ex.Message);
        }
    }

    /// <summary>
    ///     Extracts ContentId from filename if present (e.g., "content:mb:recording:.../filename.ext").
    /// </summary>
    private string? ExtractContentIdFromFilename(string filename)
    {
        // Check if filename contains ContentId prefix
        if (filename.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
        {
            var slashIndex = filename.IndexOf('/');
            if (slashIndex > 0)
            {
                return filename.Substring(0, slashIndex);
            }
        }
        return null;
    }
}
