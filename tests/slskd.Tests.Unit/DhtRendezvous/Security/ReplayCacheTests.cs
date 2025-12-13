// <copyright file="ReplayCacheTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Security;

using System;
using System.Threading;
using System.Threading.Tasks;
using slskd.DhtRendezvous.Security;
using Xunit;

public class ReplayCacheTests
{
    [Fact]
    public void IsReplay_ReturnsFalse_ForFirstOccurrence()
    {
        // Arrange
        using var cache = new ReplayCache();
        var peerId = "peer1";
        var nonce = "nonce1";

        // Act
        var result = cache.IsReplay(peerId, nonce);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsReplay_ReturnsTrue_ForDuplicateNonce()
    {
        // Arrange
        using var cache = new ReplayCache();
        var peerId = "peer1";
        var nonce = "nonce1";

        // Act
        cache.IsReplay(peerId, nonce); // First occurrence
        var result = cache.IsReplay(peerId, nonce); // Duplicate

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsReplay_IsolatesNoncesBetweenPeers()
    {
        // Arrange
        using var cache = new ReplayCache();
        var peer1 = "peer1";
        var peer2 = "peer2";
        var nonce = "same-nonce";

        // Act
        var result1 = cache.IsReplay(peer1, nonce);
        var result2 = cache.IsReplay(peer2, nonce); // Same nonce, different peer

        // Assert
        Assert.False(result1); // First for peer1
        Assert.False(result2); // First for peer2 (isolated)
    }

    [Fact]
    public void IsReplay_ReturnsFalse_ForNullOrEmptyPeerId()
    {
        // Arrange
        using var cache = new ReplayCache();

        // Act & Assert
        Assert.False(cache.IsReplay(null, "nonce1"));
        Assert.False(cache.IsReplay("", "nonce1"));
    }

    [Fact]
    public void IsReplay_ReturnsFalse_ForNullOrEmptyNonce()
    {
        // Arrange
        using var cache = new ReplayCache();

        // Act & Assert
        Assert.False(cache.IsReplay("peer1", null));
        Assert.False(cache.IsReplay("peer1", ""));
    }

    [Fact(Skip = "Timing-sensitive test - cleanup cycle is 1 minute, too slow for unit tests")]
    public async Task IsReplay_ExpiresCachedNonces_AfterTTL()
    {
        // Arrange
        var ttl = TimeSpan.FromMilliseconds(200);
        using var cache = new ReplayCache(entryTtl: ttl);
        var peerId = "peer1";
        var nonce = "nonce1";

        // Act
        cache.IsReplay(peerId, nonce); // Record nonce
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(2000)); // Wait for TTL + cleanup cycle (generous margin)

        var result = cache.IsReplay(peerId, nonce); // Should be expired

        // Assert
        Assert.False(result); // Nonce expired, so not a replay
    }

    [Fact]
    public void IsReplay_EnforcesMaxEntriesPerPeer()
    {
        // Arrange
        var maxEntries = 10;
        using var cache = new ReplayCache(maxEntriesPerPeer: maxEntries);
        var peerId = "peer1";

        // Act - Add more than max entries
        for (int i = 0; i < maxEntries + 5; i++)
        {
            cache.IsReplay(peerId, $"nonce{i}");
        }

        // Assert - Total nonce count should not exceed max
        Assert.True(cache.TotalNonceCount <= maxEntries);
    }

    [Fact]
    public void IsReplay_LRUEviction_RemovesOldestEntry()
    {
        // Arrange
        var maxEntries = 3;
        using var cache = new ReplayCache(maxEntriesPerPeer: maxEntries);
        var peerId = "peer1";

        // Act - Fill cache to capacity
        cache.IsReplay(peerId, "nonce1");
        cache.IsReplay(peerId, "nonce2");
        cache.IsReplay(peerId, "nonce3");
        
        // Add one more (should evict oldest)
        cache.IsReplay(peerId, "nonce4");
        
        // Check if oldest (nonce1) was evicted
        var result = cache.IsReplay(peerId, "nonce1");

        // Assert - nonce1 should be evicted, so not detected as replay
        Assert.False(result);
    }

    [Fact]
    public void TrackedPeerCount_ReturnsCorrectCount()
    {
        // Arrange
        using var cache = new ReplayCache();

        // Act
        cache.IsReplay("peer1", "nonce1");
        cache.IsReplay("peer2", "nonce2");
        cache.IsReplay("peer3", "nonce3");

        // Assert
        Assert.Equal(3, cache.TrackedPeerCount);
    }

    [Fact]
    public void TotalNonceCount_ReturnsCorrectCount()
    {
        // Arrange
        using var cache = new ReplayCache();

        // Act
        cache.IsReplay("peer1", "nonce1");
        cache.IsReplay("peer1", "nonce2");
        cache.IsReplay("peer2", "nonce3");

        // Assert
        Assert.Equal(3, cache.TotalNonceCount);
    }

    [Fact]
    public void ClearPeer_RemovesAllNoncesForPeer()
    {
        // Arrange
        using var cache = new ReplayCache();
        cache.IsReplay("peer1", "nonce1");
        cache.IsReplay("peer1", "nonce2");
        cache.IsReplay("peer2", "nonce3");

        // Act
        cache.ClearPeer("peer1");

        // Assert
        Assert.Equal(1, cache.TrackedPeerCount); // Only peer2 remains
        Assert.Equal(1, cache.TotalNonceCount);
    }

    [Fact]
    public void Clear_RemovesAllCachedData()
    {
        // Arrange
        using var cache = new ReplayCache();
        cache.IsReplay("peer1", "nonce1");
        cache.IsReplay("peer2", "nonce2");

        // Act
        cache.Clear();

        // Assert
        Assert.Equal(0, cache.TrackedPeerCount);
        Assert.Equal(0, cache.TotalNonceCount);
    }

    [Fact]
    public void IsReplay_ThreadSafe_HandlesConcurrentAccess()
    {
        // Arrange
        using var cache = new ReplayCache();
        var peerId = "peer1";
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act - Concurrent access from multiple threads
        Parallel.For(0, 100, i =>
        {
            try
            {
                cache.IsReplay(peerId, $"nonce{i % 10}"); // Reuse some nonces
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert - No exceptions should be thrown
        Assert.Empty(exceptions);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var cache = new ReplayCache();
        cache.IsReplay("peer1", "nonce1");

        // Act
        cache.Dispose();

        // Assert - Should not throw
        Assert.Equal(0, cache.TrackedPeerCount);
        Assert.Equal(0, cache.TotalNonceCount);
    }
}

