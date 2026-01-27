// <copyright file="SearchActionsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Search.API;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd;
using slskd.Common;
using slskd.Core.Security;
using slskd.Mesh;
using slskd.Search.Providers;
using slskd.Streaming;
using slskd.Transfers.Downloads;
using Search = slskd.Search;

/// <summary>
///     Action routing for search results (download/stream based on source).
/// </summary>
[Route("api/v{version:apiVersion}/searches")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[ValidateCsrfForCookiesOnly]
public class SearchActionsController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly IDownloadService _downloadService;
    private readonly IContentLocator _contentLocator;
    private readonly IMeshContentFetcher _meshContentFetcher;
    private readonly IMeshDirectory _meshDirectory;
    private readonly IOptionsMonitor<slskd.Options> _optionsMonitor;
    private readonly ILogger<SearchActionsController> _logger;

    public SearchActionsController(
        ISearchService searchService,
        IDownloadService downloadService,
        IContentLocator contentLocator,
        IMeshContentFetcher meshContentFetcher,
        IMeshDirectory meshDirectory,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        ILogger<SearchActionsController> logger)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _contentLocator = contentLocator ?? throw new ArgumentNullException(nameof(contentLocator));
        _meshContentFetcher = meshContentFetcher ?? throw new ArgumentNullException(nameof(meshContentFetcher));
        _meshDirectory = meshDirectory ?? throw new ArgumentNullException(nameof(meshDirectory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Initiates a download for a search result item, routing to pod or scene based on source.
    /// </summary>
    /// <param name="searchId">The search ID.</param>
    /// <param name="itemId">The item ID (response index or file identifier).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download result.</returns>
    [HttpPost("{searchId}/items/{itemId}/download")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DownloadItem(
        [FromRoute] Guid searchId,
        [FromRoute] string itemId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("[SearchActions] Download request: searchId={SearchId}, itemId={ItemId}", searchId, itemId);

        // Find the search
        var search = await _searchService.FindAsync(s => s.Id == searchId, includeResponses: true);
        if (search == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "search_not_found",
                Title = "Search not found",
                Detail = $"Search {searchId} not found"
            });
        }

        // Parse itemId (format: "responseIndex:fileIndex" or just response index)
        if (!TryParseItemId(itemId, out var responseIndex, out var fileIndex))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "invalid_item_id",
                Title = "Invalid item ID",
                Detail = "Item ID must be in format 'responseIndex:fileIndex' or 'responseIndex'"
            });
        }

        if (responseIndex < 0 || responseIndex >= search.Responses.Count())
        {
            return NotFound(new ProblemDetails
            {
                Type = "item_not_found",
                Title = "Item not found",
                Detail = $"Response index {responseIndex} not found in search {searchId}"
            });
        }

        var response = search.Responses.ElementAt(responseIndex);
        var file = fileIndex >= 0 && fileIndex < response.Files.Count
            ? response.Files.ElementAt(fileIndex)
            : response.Files.FirstOrDefault();

        if (file == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "file_not_found",
                Title = "File not found",
                Detail = $"File index {fileIndex} not found in response {responseIndex}"
            });
        }

        // Route based on primary source
        var primarySource = response.PrimarySource ?? "scene"; // Default to scene if not set

        if (primarySource == "pod" && response.PodContentRef != null)
        {
            // Pod download - use ContentId-based download
            return await HandlePodDownloadAsync(response.PodContentRef.ContentId, file, response.Username, cancellationToken);
        }
        else if (primarySource == "scene" && response.SceneContentRef != null)
        {
            // Scene download - use existing Soulseek download pipeline
            return await HandleSceneDownloadAsync(response.SceneContentRef, file, cancellationToken);
        }
        else
        {
            return BadRequest(new ProblemDetails
            {
                Type = "invalid_source",
                Title = "Invalid source",
                Detail = $"Cannot determine download source for item {itemId}"
            });
        }
    }

    /// <summary>
    ///     Initiates a stream for a search result item (pod only).
    /// </summary>
    /// <param name="searchId">The search ID.</param>
    /// <param name="itemId">The item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream URL or error.</returns>
    [HttpPost("{searchId}/items/{itemId}/stream")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> StreamItem(
        [FromRoute] Guid searchId,
        [FromRoute] string itemId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("[SearchActions] Stream request: searchId={SearchId}, itemId={ItemId}", searchId, itemId);

        // Find the search
        var search = await _searchService.FindAsync(s => s.Id == searchId, includeResponses: true);
        if (search == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "search_not_found",
                Title = "Search not found",
                Detail = $"Search {searchId} not found"
            });
        }

        // Parse itemId
        if (!TryParseItemId(itemId, out var responseIndex, out var fileIndex))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "invalid_item_id",
                Title = "Invalid item ID",
                Detail = "Item ID must be in format 'responseIndex:fileIndex' or 'responseIndex'"
            });
        }

        if (responseIndex < 0 || responseIndex >= search.Responses.Count())
        {
            return NotFound(new ProblemDetails
            {
                Type = "item_not_found",
                Title = "Item not found",
                Detail = $"Response index {responseIndex} not found in search {searchId}"
            });
        }

        var response = search.Responses.ElementAt(responseIndex);
        var primarySource = response.PrimarySource ?? "scene";

        if (primarySource != "pod" || response.PodContentRef == null)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "scene_streaming_not_supported",
                Title = "Scene streaming not supported",
                Detail = "Streaming is only supported for pod results. Use download endpoint for scene results."
            });
        }

        // Pod streaming - return stream URL
        var contentId = response.PodContentRef.ContentId;
        var streamUrl = $"/api/v0/streams/{Uri.EscapeDataString(contentId)}";

        return Ok(new
        {
            stream_url = streamUrl,
            content_id = contentId,
            source = "pod"
        });
    }

    private async Task<IActionResult> HandlePodDownloadAsync(string contentId, Search.File file, string peerId, CancellationToken ct)
    {
        _logger.LogInformation("[SearchActions] Pod download: contentId={ContentId}, filename={Filename}, peerId={PeerId}", contentId, file.Filename, peerId);

        try
        {
            // Check if content is available locally (in our share library)
            var resolved = _contentLocator.Resolve(contentId, ct);
            if (resolved != null)
            {
                // Content is already local - return success
                _logger.LogDebug("[SearchActions] Pod content {ContentId} is already local at {Path}", contentId, resolved.AbsolutePath);
                return Ok(new
                {
                    success = true,
                    content_id = contentId,
                    source = "pod",
                    local = true,
                    path = resolved.AbsolutePath,
                    message = "Content is already available locally"
                });
            }

            // Content is not local - download from pod peers
            _logger.LogInformation("[SearchActions] Pod content {ContentId} is not local - downloading from peer {PeerId}", contentId, peerId);

            // Try to find peers that have this content (fallback if peerId from search is unavailable)
            string targetPeerId = peerId;
            if (string.IsNullOrWhiteSpace(targetPeerId))
            {
                var peers = await _meshDirectory.FindPeersByContentAsync(contentId, ct);
                if (peers == null || peers.Count == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Type = "pod_peer_not_found",
                        Title = "Pod peer not found",
                        Detail = $"No pod peers found hosting content {contentId}"
                    });
                }
                targetPeerId = peers[0].PeerId;
                _logger.LogDebug("[SearchActions] Using peer {PeerId} from mesh directory lookup", targetPeerId);
            }

            // Fetch content from mesh peer
            var fetchResult = await _meshContentFetcher.FetchAsync(
                peerId: targetPeerId,
                contentId: contentId,
                expectedSize: file.Size > 0 ? file.Size : null,
                expectedHash: null, // Hash validation can be added later if needed
                offset: 0,
                length: 0, // Fetch entire file
                cancellationToken: ct);

            if (fetchResult.Error != null || fetchResult.Data == null)
            {
                _logger.LogWarning("[SearchActions] Failed to fetch pod content {ContentId} from peer {PeerId}: {Error}",
                    contentId, targetPeerId, fetchResult.Error ?? "Unknown error");
                return StatusCode(502, new ProblemDetails
                {
                    Type = "pod_fetch_failed",
                    Title = "Pod content fetch failed",
                    Detail = fetchResult.Error ?? "Failed to fetch content from pod peer"
                });
            }

            // Validate size if expected size was provided
            if (file.Size > 0 && fetchResult.Size != file.Size)
            {
                _logger.LogWarning("[SearchActions] Size mismatch for pod content {ContentId}: expected {Expected}, got {Actual}",
                    contentId, file.Size, fetchResult.Size);
                // Continue anyway - size mismatch might be acceptable
            }

            // Write content to incomplete downloads directory
            var incompleteDir = _optionsMonitor.CurrentValue.Directories.Incomplete;
            var localFilename = file.Filename.ToLocalFilename(baseDirectory: incompleteDir);
            var localDirectory = System.IO.Path.GetDirectoryName(localFilename);
            if (!string.IsNullOrEmpty(localDirectory) && !System.IO.Directory.Exists(localDirectory))
            {
                System.IO.Directory.CreateDirectory(localDirectory);
            }

            using (var fileStream = System.IO.File.Create(localFilename))
            {
                await fetchResult.Data.CopyToAsync(fileStream, ct);
            }

            fetchResult.Data.Dispose();

            _logger.LogInformation("[SearchActions] Successfully downloaded pod content {ContentId} from peer {PeerId} to {Path}",
                contentId, targetPeerId, localFilename);

            return Ok(new
            {
                success = true,
                content_id = contentId,
                source = "pod",
                local = false,
                path = localFilename,
                message = "Content downloaded from pod peer"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SearchActions] Pod download failed: {Message}", ex.Message);
            return StatusCode(500, new ProblemDetails
            {
                Type = "pod_download_exception",
                Title = "Pod download exception",
                Detail = ex.Message
            });
        }
    }

    private async Task<IActionResult> HandleSceneDownloadAsync(SceneContentRef sceneRef, Search.File file, CancellationToken ct)
    {
        _logger.LogInformation("[SearchActions] Scene download: username={Username}, filename={Filename}",
            sceneRef.Username, sceneRef.Filename);

        try
        {
            // Use existing Soulseek download pipeline
            var files = new[] { (sceneRef.Filename, file.Size) };
            var (enqueued, failed) = await _downloadService.EnqueueAsync(sceneRef.Username, files, ct);

            if (enqueued.Count > 0)
            {
                return Ok(new
                {
                    success = true,
                    download_id = enqueued[0].Id.ToString("N"),
                    source = "scene"
                });
            }
            else if (failed.Count > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Type = "download_failed",
                    Title = "Download failed",
                    Detail = string.Join("; ", failed)
                });
            }
            else
            {
                return StatusCode(500, new ProblemDetails
                {
                    Type = "download_error",
                    Title = "Download error",
                    Detail = "Download enqueue returned no results"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SearchActions] Scene download failed: {Message}", ex.Message);
            return StatusCode(500, new ProblemDetails
            {
                Type = "download_exception",
                Title = "Download exception",
                Detail = ex.Message
            });
        }
    }

    private static bool TryParseItemId(string itemId, out int responseIndex, out int fileIndex)
    {
        responseIndex = -1;
        fileIndex = -1;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        var parts = itemId.Split(':');
        if (parts.Length == 1)
        {
            // Just response index
            if (int.TryParse(parts[0], out responseIndex))
            {
                fileIndex = 0; // Default to first file
                return true;
            }
        }
        else if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out responseIndex) && int.TryParse(parts[1], out fileIndex))
            {
                return true;
            }
        }

        return false;
    }
}
