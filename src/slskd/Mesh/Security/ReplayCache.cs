// <copyright file="ReplayCache.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Overlay;

/// <summary>
/// Detects and prevents replay attacks by tracking seen MessageIds per peer.
/// Also enforces timestamp skew windows.
/// </summary>
public interface IReplayCache
{
    /// <summary>
    /// Checks if an envelope is valid (not a replay, timestamp within window).
    /// If valid and not seen before, records it.
    /// </summary>
    /// <param name="peerId">The peer ID sending the message.</param>
    /// <param name="envelope">The control envelope to validate.</param>
    /// <returns>True if envelope is valid and not a replay.</returns>
    bool ValidateAndRecord(string peerId, ControlEnvelope envelope);

    /// <summary>
    /// Clears expired entries from the cache (for memory management).
    /// Call periodically from a background task.
    /// </summary>
    void PurgeExpired();
}

public class ReplayCache : IReplayCache
{
    private readonly ILogger<ReplayCache> _logger;
    private readonly ConcurrentDictionary<string, PeerMessageCache> _peerCaches = new();

    // Configuration: timestamp skew window (2 minutes in each direction)
    private readonly TimeSpan _maxTimestampSkew = TimeSpan.FromMinutes(2);

    // Configuration: how long to remember MessageIds (10 minutes)
    private readonly TimeSpan _cacheRetention = TimeSpan.FromMinutes(10);

    public ReplayCache(ILogger<ReplayCache> logger)
    {
        _logger = logger;
    }

    public bool ValidateAndRecord(string peerId, ControlEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            _logger.LogWarning("[ReplayCache] Cannot validate envelope with null/empty peerId");
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.MessageId))
        {
            _logger.LogWarning("[ReplayCache] Envelope missing MessageId (peerId: {PeerId})", peerId);
            return false;
        }

        // 1. Validate timestamp skew
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var skew = Math.Abs(now - envelope.TimestampUnixMs);
        var skewMs = skew;

        if (skewMs > _maxTimestampSkew.TotalMilliseconds)
        {
            _logger.LogWarning(
                "[ReplayCache] Timestamp skew too large: {SkewMs}ms (peerId: {PeerId}, msgId: {MessageId})",
                skewMs,
                peerId,
                envelope.MessageId);
            return false;
        }

        // 2. Check for replay
        var cache = _peerCaches.GetOrAdd(peerId, _ => new PeerMessageCache());

        if (cache.HasSeen(envelope.MessageId))
        {
            _logger.LogWarning(
                "[ReplayCache] Replay detected (peerId: {PeerId}, msgId: {MessageId})",
                peerId,
                envelope.MessageId);
            return false;
        }

        // 3. Record as seen
        cache.Record(envelope.MessageId, DateTimeOffset.UtcNow);
        return true;
    }

    public void PurgeExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - _cacheRetention;
        var purgedCount = 0;

        foreach (var (peerId, cache) in _peerCaches)
        {
            var before = cache.Count;
            cache.PurgeOlderThan(cutoff);
            purgedCount += before - cache.Count;

            // Remove empty peer caches
            if (cache.Count == 0)
            {
                _peerCaches.TryRemove(peerId, out _);
            }
        }

        if (purgedCount > 0)
        {
            _logger.LogDebug("[ReplayCache] Purged {Count} expired entries", purgedCount);
        }
    }

    private class PeerMessageCache
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _seenMessages = new();

        public int Count => _seenMessages.Count;

        public bool HasSeen(string messageId) => _seenMessages.ContainsKey(messageId);

        public void Record(string messageId, DateTimeOffset timestamp) =>
            _seenMessages.TryAdd(messageId, timestamp);

        public void PurgeOlderThan(DateTimeOffset cutoff)
        {
            var toRemove = _seenMessages
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var messageId in toRemove)
            {
                _seenMessages.TryRemove(messageId, out _);
            }
        }
    }
}

