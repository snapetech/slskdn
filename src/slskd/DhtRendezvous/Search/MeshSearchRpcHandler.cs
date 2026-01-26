// <copyright file="MeshSearchRpcHandler.cs" company="slskdn Team">
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
using slskd.DhtRendezvous.Security;
using slskd.Shares;
using Soulseek;

/// <summary>
/// Handles inbound mesh_search_req on the overlay: runs local share search and returns mesh_search_resp.
/// </summary>
public interface IMeshSearchRpcHandler
{
    /// <summary>
    /// Executes a local share search and returns a mesh search response.
    /// </summary>
    /// <param name="request">The mesh search request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response message to send back to the initiator.</returns>
    Task<MeshSearchResponseMessage> HandleAsync(MeshSearchRequestMessage request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles mesh_search_req by searching the local share repository and mapping results to MeshSearchResponseMessage.
/// Never returns absolute paths; uses virtual/masked paths from the repository.
/// </summary>
public sealed class MeshSearchRpcHandler : IMeshSearchRpcHandler
{
    private readonly IShareService _shareService;
    private readonly ILogger<MeshSearchRpcHandler> _logger;

    public MeshSearchRpcHandler(IShareService shareService, ILogger<MeshSearchRpcHandler> logger)
    {
        _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<MeshSearchResponseMessage> HandleAsync(MeshSearchRequestMessage request, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = SearchQuery.FromText(request.SearchText);
            var maxResults = Math.Clamp(request.MaxResults, MessageValidator.MinMeshSearchMaxResults, MessageValidator.MaxMeshSearchMaxResults);

            var files = await _shareService.SearchLocalAsync(query).ConfigureAwait(false);

            // Deterministic ordering by filename; take up to maxResults+1 to detect truncation
            var ordered = files
                .OrderBy(f => f.Filename, StringComparer.Ordinal)
                .Take(maxResults + 1)
                .ToList();

            var truncated = ordered.Count > maxResults;
            var toReturn = truncated ? ordered.Take(maxResults) : ordered;

            var dtos = toReturn
                .Select(f => new MeshSearchFileDto
                {
                    Filename = f.Filename, // Virtual share path only; repository must not expose absolute paths
                    Size = f.Size,
                    Extension = string.IsNullOrEmpty(f.Extension) ? null : f.Extension,
                    Bitrate = f.BitRate,
                    Duration = f.Length,
                    Codec = DeriveCodec(f.Extension),
                })
                .ToList();

            // Enforce response amplification limit
            if (dtos.Count > MessageValidator.MaxMeshSearchResponseFiles)
            {
                dtos = dtos.Take(MessageValidator.MaxMeshSearchResponseFiles).ToList();
                truncated = true;
            }

            return new MeshSearchResponseMessage
            {
                RequestId = request.RequestId,
                Files = dtos,
                Truncated = truncated,
                Error = null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mesh search failed for request {RequestId}: {Message}", request.RequestId, ex.Message);
            return new MeshSearchResponseMessage
            {
                RequestId = request.RequestId,
                Files = new List<MeshSearchFileDto>(),
                Truncated = false,
                Error = "Search failed",
            };
        }
    }

    private static string? DeriveCodec(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        return (extension.TrimStart('.').ToLowerInvariant()) switch
        {
            "flac" => "FLAC",
            "mp3" => "MP3",
            "m4a" or "aac" => "AAC",
            "opus" => "Opus",
            "ogg" => "Vorbis",
            "wav" => "WAV",
            _ => null,
        };
    }
}
