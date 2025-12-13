// <copyright file="ProofOfPossessionService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Messages;

/// <summary>
///     Verifies proof-of-possession for mesh hash entries via challenge/response (T-1434).
/// </summary>
public interface IProofOfPossessionService
{
    /// <summary>
    ///     Verifies that a peer can prove possession of the file for a hash entry.
    /// </summary>
    /// <param name="peer">Peer username.</param>
    /// <param name="entry">Mesh hash entry.</param>
    /// <param name="sendChallengeAsync">Callback to send a challenge and await response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if proof succeeds or is cached; false otherwise.</returns>
    Task<bool> VerifyAsync(
        string peer,
        MeshHashEntry entry,
        Func<MeshChallengeRequestMessage, Task<MeshChallengeResponseMessage?>> sendChallengeAsync,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Proof-of-possession verifier that challenges peers to return the first 32KB chunk.
/// </summary>
public class ProofOfPossessionService : IProofOfPossessionService
{
    private const int ChallengeChunkLength = 32 * 1024; // 32KB (matches ByteHash computation)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly ILogger<ProofOfPossessionService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProofOfPossessionService(ILogger<ProofOfPossessionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(
        string peer,
        MeshHashEntry entry,
        Func<MeshChallengeRequestMessage, Task<MeshChallengeResponseMessage?>> sendChallengeAsync,
        CancellationToken cancellationToken = default)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.FlacKey) || string.IsNullOrWhiteSpace(entry.ByteHash) || entry.Size <= 0)
        {
            return false;
        }

        var cacheKey = $"{peer}:{entry.FlacKey}";
        if (IsCachedValid(cacheKey))
        {
            return true;
        }

        var length = (int)Math.Min(entry.Size, ChallengeChunkLength);
        var challenge = new MeshChallengeRequestMessage
        {
            ChallengeId = Guid.NewGuid().ToString("N"),
            FlacKey = entry.FlacKey,
            ByteHash = entry.ByteHash,
            Offset = 0,
            Length = length,
        };

        _logger.LogDebug("[MESH][POP] Sending challenge {ChallengeId} to {Peer} for {FlacKey}", challenge.ChallengeId, peer, entry.FlacKey);
        var response = await sendChallengeAsync(challenge);
        if (response == null)
        {
            _logger.LogWarning("[MESH][POP] No response to challenge {ChallengeId} from {Peer}", challenge.ChallengeId, peer);
            return false;
        }

        if (!response.Success)
        {
            _logger.LogWarning("[MESH][POP] Challenge {ChallengeId} failed from {Peer}: {Error}", challenge.ChallengeId, peer, response.Error);
            return false;
        }

        if (!string.Equals(response.ChallengeId, challenge.ChallengeId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(response.FlacKey, entry.FlacKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[MESH][POP] Challenge response mismatch from {Peer}", peer);
            return false;
        }

        if (response.Data == null || response.Data.Length != length)
        {
            _logger.LogWarning("[MESH][POP] Challenge response length mismatch from {Peer}: expected {Expected}, got {Actual}", peer, length, response.Data?.Length ?? 0);
            return false;
        }

        // Validate hash of returned chunk matches ByteHash (first 32KB hash).
        using var sha256 = SHA256.Create();
        var chunkHash = sha256.ComputeHash(response.Data);
        var chunkHashHex = Convert.ToHexString(chunkHash).ToLowerInvariant();
        var claimedHash = entry.ByteHash.ToLowerInvariant();

        if (!string.Equals(chunkHashHex, claimedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[MESH][POP] Challenge hash mismatch for {FlacKey} from {Peer}", entry.FlacKey, peer);
            return false;
        }

        SetCache(cacheKey);
        _logger.LogInformation("[MESH][POP] Proof-of-possession succeeded for {FlacKey} from {Peer}", entry.FlacKey, peer);
        return true;
    }

    private bool IsCachedValid(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                return true;
            }

            _cache.TryRemove(cacheKey, out _);
        }

        return false;
    }

    private void SetCache(string cacheKey)
    {
        _cache[cacheKey] = new CacheEntry { ExpiresAt = DateTime.UtcNow.Add(CacheDuration) };
    }

    private class CacheEntry
    {
        public DateTime ExpiresAt { get; init; }
    }
}
