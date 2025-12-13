// <copyright file="ReplayCache.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Thread-safe cache for detecting replay attacks via nonce tracking.
/// Tracks seen nonces per peer with automatic expiration.
/// </summary>
public sealed class ReplayCache : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTimeOffset>> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _entryTtl;
    private readonly int _maxEntriesPerPeer;
    private bool _disposed;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayCache"/> class.
    /// </summary>
    /// <param name="entryTtl">How long to keep nonces (default: 10 minutes).</param>
    /// <param name="maxEntriesPerPeer">Max nonces to track per peer (default: 1000).</param>
    public ReplayCache(TimeSpan? entryTtl = null, int maxEntriesPerPeer = 1000)
    {
        _entryTtl = entryTtl ?? TimeSpan.FromMinutes(10);
        _maxEntriesPerPeer = maxEntriesPerPeer;
        
        // Clean up expired entries every minute
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }
    
    /// <summary>
    /// Checks if a nonce has been seen before for a given peer.
    /// If not, records it.
    /// </summary>
    /// <param name="peerId">The peer ID (or IP address for pre-auth).</param>
    /// <param name="nonce">The nonce to check.</param>
    /// <returns>True if this is a replay (nonce already seen), false if new.</returns>
    public bool IsReplay(string peerId, string nonce)
    {
        if (string.IsNullOrEmpty(peerId) || string.IsNullOrEmpty(nonce))
        {
            return false; // Can't determine replay without identifiers
        }
        
        var peerCache = _cache.GetOrAdd(peerId, _ => new ConcurrentDictionary<string, DateTimeOffset>());
        
        // Check if already exists
        if (peerCache.ContainsKey(nonce))
        {
            return true; // REPLAY DETECTED
        }
        
        // Check if we've hit the limit for this peer
        if (peerCache.Count >= _maxEntriesPerPeer)
        {
            // Remove oldest entry to make room
            var oldest = DateTimeOffset.MaxValue;
            string? oldestKey = null;
            
            foreach (var kvp in peerCache)
            {
                if (kvp.Value < oldest)
                {
                    oldest = kvp.Value;
                    oldestKey = kvp.Key;
                }
            }
            
            if (oldestKey != null)
            {
                peerCache.TryRemove(oldestKey, out _);
            }
        }
        
        // Record new nonce
        peerCache[nonce] = DateTimeOffset.UtcNow;
        return false; // Not a replay
    }
    
    /// <summary>
    /// Gets the number of peers being tracked.
    /// </summary>
    public int TrackedPeerCount => _cache.Count;
    
    /// <summary>
    /// Gets the total number of nonces being tracked across all peers.
    /// </summary>
    public int TotalNonceCount
    {
        get
        {
            var total = 0;
            foreach (var peerCache in _cache.Values)
            {
                total += peerCache.Count;
            }
            return total;
        }
    }
    
    /// <summary>
    /// Clears all cached nonces for a specific peer.
    /// </summary>
    public void ClearPeer(string peerId)
    {
        _cache.TryRemove(peerId, out _);
    }
    
    /// <summary>
    /// Clears all cached nonces.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }
    
    private void CleanupExpiredEntries()
    {
        if (_disposed)
        {
            return;
        }
        
        try
        {
            var now = DateTimeOffset.UtcNow;
            var expiredPeers = new System.Collections.Generic.List<string>();
            
            foreach (var peerEntry in _cache)
            {
                var peerId = peerEntry.Key;
                var peerCache = peerEntry.Value;
                
                // Remove expired nonces for this peer
                var expiredNonces = new System.Collections.Generic.List<string>();
                foreach (var nonceEntry in peerCache)
                {
                    if (now - nonceEntry.Value > _entryTtl)
                    {
                        expiredNonces.Add(nonceEntry.Key);
                    }
                }
                
                foreach (var nonce in expiredNonces)
                {
                    peerCache.TryRemove(nonce, out _);
                }
                
                // If peer has no nonces left, remove peer entry
                if (peerCache.IsEmpty)
                {
                    expiredPeers.Add(peerId);
                }
            }
            
            foreach (var peerId in expiredPeers)
            {
                _cache.TryRemove(peerId, out _);
            }
        }
        catch
        {
            // Suppress cleanup errors to prevent timer from stopping
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        _cleanupTimer?.Dispose();
        _cache.Clear();
    }
}

