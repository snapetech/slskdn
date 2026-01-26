// <copyright file="MeshContentFetcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;

/// <summary>
/// Fetches content from mesh overlay network by ContentId with size and hash validation.
/// Uses IMeshServiceClient to call MeshContent service on remote peers.
/// </summary>
public sealed class MeshContentFetcher : IMeshContentFetcher
{
    private readonly IMeshServiceClient _meshClient;
    private readonly ILogger<MeshContentFetcher> _logger;

    public MeshContentFetcher(
        IMeshServiceClient meshClient,
        ILogger<MeshContentFetcher> logger)
    {
        _meshClient = meshClient ?? throw new ArgumentNullException(nameof(meshClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<MeshContentFetchResult> FetchAsync(
        string peerId,
        string contentId,
        long? expectedSize = null,
        string? expectedHash = null,
        long offset = 0,
        int length = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(peerId))
            throw new ArgumentException("Peer ID cannot be empty", nameof(peerId));
        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("Content ID cannot be empty", nameof(contentId));

        try
        {
            // Build request payload
            var request = new
            {
                contentId = contentId,
                range = offset > 0 || length > 0 ? new
                {
                    offset = offset,
                    length = length
                } : null
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(request);
            var correlationId = Guid.NewGuid().ToString("N");

            var call = new ServiceCall
            {
                CorrelationId = correlationId,
                ServiceName = "MeshContent",
                Method = "GetByContentId",
                Payload = payload
            };

            _logger.LogDebug(
                "[MeshContentFetcher] Fetching content {ContentId} from peer {PeerId} (offset={Offset}, length={Length})",
                contentId, peerId, offset, length);

            var reply = await _meshClient.CallAsync(peerId, call, cancellationToken);

            if (reply.StatusCode != ServiceStatusCodes.OK)
            {
                return new MeshContentFetchResult
                {
                    Error = reply.ErrorMessage ?? $"Mesh service returned status {reply.StatusCode}",
                    SizeValid = false,
                    HashValid = false
                };
            }

            if (reply.Payload == null || reply.Payload.Length == 0)
            {
                return new MeshContentFetchResult
                {
                    Error = "Empty response from mesh service",
                    SizeValid = false,
                    HashValid = false
                };
            }

            var actualSize = reply.Payload.Length;
            var result = new MeshContentFetchResult
            {
                Data = new MemoryStream(reply.Payload),
                Size = actualSize,
                SizeValid = !expectedSize.HasValue || actualSize == expectedSize.Value
            };

            // Hash validation (if expected hash provided)
            if (!string.IsNullOrEmpty(expectedHash))
            {
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(reply.Payload);
                var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                result.Hash = actualHash;
                result.HashValid = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

                if (!result.HashValid)
                {
                    _logger.LogWarning(
                        "[MeshContentFetcher] Hash mismatch for {ContentId} from {PeerId}: expected {Expected}, got {Actual}",
                        contentId, peerId, expectedHash, actualHash);
                }
            }

            if (!result.SizeValid)
            {
                _logger.LogWarning(
                    "[MeshContentFetcher] Size mismatch for {ContentId} from {PeerId}: expected {Expected}, got {Actual}",
                    contentId, peerId, expectedSize, actualSize);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MeshContentFetcher] Failed to fetch {ContentId} from {PeerId}", contentId, peerId);
            return new MeshContentFetchResult
            {
                Error = ex.Message,
                SizeValid = false,
                HashValid = false
            };
        }
    }
}
