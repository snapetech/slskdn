// <copyright file="ShardCache.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.ShadowIndex;

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Shard cache to reduce DHT queries.
/// </summary>
public interface IShardCache
{
    Task<ShadowIndexShard?> GetAsync(string mbid, CancellationToken ct = default);
    Task SetAsync(string mbid, ShadowIndexShard shard, TimeSpan ttl, CancellationToken ct = default);
    void Remove(string mbid);
    ShardCacheStats GetStats();
}

public class ShardCacheStats
{
    public int EntryCount { get; set; }
    public long ApproximateTotalSizeBytes { get; set; }
}

/// <summary>
/// In-memory cache for shadow index shards.
/// </summary>
public class ShardCache : IShardCache
{
    private readonly IMemoryCache cache;
    private readonly ILogger<ShardCache> logger;
    private readonly ConcurrentDictionary<string, CacheEntryMetadata> entries = new();

    public ShardCache(
        IMemoryCache cache,
        ILogger<ShardCache> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    public Task<ShadowIndexShard?> GetAsync(string mbid, CancellationToken ct)
    {
        if (cache.TryGetValue(GetCacheKey(mbid), out ShadowIndexShard? shard))
        {
            // Check if cached shard is expired
            if (!ShardEvictionPolicy.IsExpired(shard!))
            {
                logger.LogDebug("[VSF-CACHE] Cache hit for {MBID}", mbid);
                return Task.FromResult<ShadowIndexShard?>(shard);
            }

            // Expired, remove from cache
            cache.Remove(GetCacheKey(mbid));
            entries.TryRemove(mbid, out _);
        }

        logger.LogDebug("[VSF-CACHE] Cache miss for {MBID}", mbid);
        return Task.FromResult<ShadowIndexShard?>(null);
    }

    public Task SetAsync(string mbid, ShadowIndexShard shard, TimeSpan ttl, CancellationToken ct)
    {
        logger.LogDebug("[VSF-CACHE] Caching shard for {MBID} (TTL: {TTL})", mbid, ttl);

        cache.Set(GetCacheKey(mbid), shard, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });
        entries[mbid] = new CacheEntryMetadata
        {
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            ApproximateSizeBytes = ShardSerializer.EstimateSize(shard)
        };

        return Task.CompletedTask;
    }

    public void Remove(string mbid)
    {
        cache.Remove(GetCacheKey(mbid));
        entries.TryRemove(mbid, out _);
    }

    public ShardCacheStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (mbid, metadata) in entries)
        {
            if (metadata.ExpiresAt <= now)
            {
                entries.TryRemove(mbid, out _);
            }
        }

        return new ShardCacheStats
        {
            EntryCount = entries.Count,
            ApproximateTotalSizeBytes = entries.Values.Sum(value => value.ApproximateSizeBytes)
        };
    }

    private static string GetCacheKey(string mbid) => $"vsf:shard:{mbid}";

    private sealed class CacheEntryMetadata
    {
        public DateTimeOffset ExpiresAt { get; init; }
        public long ApproximateSizeBytes { get; init; }
    }
}
