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
}

/// <summary>
/// In-memory cache for shadow index shards.
/// </summary>
public class ShardCache : IShardCache
{
    private readonly IMemoryCache cache;
    private readonly ILogger<ShardCache> logger;

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

        return Task.CompletedTask;
    }

    public void Remove(string mbid)
    {
        cache.Remove(GetCacheKey(mbid));
    }

    private static string GetCacheKey(string mbid) => $"vsf:shard:{mbid}";
}
