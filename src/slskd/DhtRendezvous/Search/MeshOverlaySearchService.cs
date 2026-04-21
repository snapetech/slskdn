// <copyright file="MeshOverlaySearchService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Search;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.DhtRendezvous.Messages;
using slskd.Search;
using slskd.Search.Providers;
using SlskdSearchFile = slskd.Search.File;

/// <summary>
/// Initiator-side mesh overlay search: sends mesh_search_req to overlay peers and aggregates mesh_search_resp into Search.Response.
/// Only queries outbound connections (we initiated) so request-response doesn't compete with the server's read loop.
/// </summary>
public interface IMeshOverlaySearchService
{
    /// <summary>
    /// Searches mesh overlay peers in parallel and returns aggregated Response per peer.
    /// </summary>
    /// <param name="searchText">The search query text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One Response per peer that returned results (empty list if none).</returns>
    Task<IReadOnlyList<Response>> SearchAsync(string searchText, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends mesh_search_req to outbound overlay connections with MeshSearch feature, waits for mesh_search_resp with per-peer timeout.
/// </summary>
public sealed class MeshOverlaySearchService : IMeshOverlaySearchService
{
    private const int PerPeerTimeoutSeconds = 3;
    private const int DefaultMaxResults = 200;

    private readonly MeshNeighborRegistry _registry;
    private readonly MeshOverlayRequestRouter _requestRouter;
    private readonly ILogger<MeshOverlaySearchService> _logger;

    public MeshOverlaySearchService(
        MeshNeighborRegistry registry,
        MeshOverlayRequestRouter requestRouter,
        ILogger<MeshOverlaySearchService> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _requestRouter = requestRouter ?? throw new ArgumentNullException(nameof(requestRouter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Response>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return Array.Empty<Response>();
        }

        var connections = _registry.GetAllConnections()
            .Where(c =>
                c.IsOutbound
                && c.IsHandshakeComplete
                && c.IsConnected
                && (c.Features?.Contains(OverlayFeatures.MeshSearch) == true))
            .ToList();

        if (connections.Count == 0)
        {
            _logger.LogDebug("[MeshSearch] No outbound mesh peers with MeshSearch feature; skipping overlay search for '{Query}'", searchText);
            return Array.Empty<Response>();
        }

        _logger.LogDebug("[MeshSearch] Fanning out '{Query}' to {Count} mesh peer(s)", searchText, connections.Count);

        var tasks = connections.Select(c => QueryPeerAsync(c, searchText, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var nonEmpty = results.Where(r => r != null).Cast<Response>().ToList();
        _logger.LogDebug("[MeshSearch] '{Query}' returned results from {Hits}/{Total} peer(s)", searchText, nonEmpty.Count, connections.Count);
        return nonEmpty;
    }

    private async Task<Response?> QueryPeerAsync(MeshOverlayConnection connection, string searchText, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var req = new MeshSearchRequestMessage
        {
            RequestId = requestId,
            SearchText = searchText.Trim(),
            MaxResults = DefaultMaxResults,
        };

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(PerPeerTimeoutSeconds));

            var responseTask = _requestRouter.WaitForMeshSearchResponseAsync(connection, requestId, timeoutCts.Token);
            await connection.WriteMessageAsync(req, timeoutCts.Token).ConfigureAwait(false);
            var resp = await responseTask.ConfigureAwait(false);

            if (resp.RequestId != requestId)
            {
                _logger.LogDebug("Mesh search response request_id mismatch from {Username}, ignoring", OverlayLogSanitizer.Username(connection.Username));
                return null;
            }

            if (!string.IsNullOrEmpty(resp.Error))
            {
                _logger.LogDebug("Mesh search error from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), resp.Error);
                return null;
            }

            if (resp.Files == null || resp.Files.Count == 0)
            {
                return null;
            }

            var files = resp.Files
                .Select(f => new SlskdSearchFile
                {
                    Code = 1,
                    Filename = f.Filename,
                    Size = f.Size,
                    Extension = f.Extension ?? string.Empty,
                    BitRate = f.Bitrate,
                    Length = f.Duration,
                    IsLocked = false,
                    ContentId = f.ContentId,
                    Hash = f.Hash,
                })
                .ToList();

            var firstContentFile = resp.Files.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.ContentId));

            return new Response
            {
                Username = connection.Username ?? "?",
                Token = 0,
                HasFreeUploadSlot = true,
                UploadSpeed = 0,
                QueueLength = 0,
                FileCount = files.Count,
                Files = files,
                LockedFileCount = 0,
                LockedFiles = new List<SlskdSearchFile>(),
                SourceProviders = new List<string> { "pod" },
                PrimarySource = firstContentFile == null ? string.Empty : "pod",
                PodContentRef = firstContentFile == null
                    ? null
                    : new PodContentRef
                    {
                        ContentId = firstContentFile.ContentId!,
                        Hash = firstContentFile.Hash,
                    },
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Mesh overlay search to {Username} failed: {Message}", OverlayLogSanitizer.Username(connection.Username), ex.Message);
            return null;
        }
        finally
        {
            _requestRouter.RemoveMeshSearchResponse(connection, requestId);
        }
    }
}
