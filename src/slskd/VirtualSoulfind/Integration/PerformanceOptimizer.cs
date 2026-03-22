// <copyright file="PerformanceOptimizer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.ShadowIndex;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Performance optimizations for Virtual Soulfind.
/// </summary>
public interface IPerformanceOptimizer
{
    /// <summary>
    /// Prefetch hot MBIDs into cache.
    /// </summary>
    Task PrefetchHotMbidsAsync(List<string> mbids, CancellationToken ct = default);

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    CacheStatistics GetCacheStatistics();
}

/// <summary>
/// Cache statistics.
/// </summary>
public class CacheStatistics
{
    public int CachedShardCount { get; set; }
    public long TotalCacheSizeBytes { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double HitRate => CacheHits + CacheMisses > 0
        ? (double)CacheHits / (CacheHits + CacheMisses)
        : 0;
}

/// <summary>
/// Performance optimizer for DHT and shadow index queries.
/// </summary>
public class PerformanceOptimizer : IPerformanceOptimizer
{
    private readonly ILogger<PerformanceOptimizer> logger;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IShardCache cache;
    private int cacheHits;
    private int cacheMisses;

    public PerformanceOptimizer(
        ILogger<PerformanceOptimizer> logger,
        IShadowIndexQuery shadowIndex,
        IShardCache cache)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
        this.cache = cache;
    }

    public async Task PrefetchHotMbidsAsync(List<string> mbids, CancellationToken ct)
    {
        logger.LogInformation("[VSF-PERF] Prefetching {Count} hot MBIDs", mbids.Count);

        using var semaphore = new SemaphoreSlim(8);
        var prefetchTasks = mbids.Distinct(StringComparer.OrdinalIgnoreCase).Select(async mbid =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var cached = await cache.GetAsync(mbid, ct);
                if (cached != null)
                {
                    Interlocked.Increment(ref cacheHits);
                    return;
                }

                Interlocked.Increment(ref cacheMisses);
                await shadowIndex.QueryAsync(mbid, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-PERF] Failed to prefetch {MBID}", mbid);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(prefetchTasks);

        logger.LogInformation("[VSF-PERF] Prefetch complete: {Count} MBIDs", mbids.Count);
    }

    public CacheStatistics GetCacheStatistics()
    {
        var shardCacheStats = cache.GetStats();
        return new CacheStatistics
        {
            CachedShardCount = shardCacheStats.EntryCount,
            TotalCacheSizeBytes = shardCacheStats.ApproximateTotalSizeBytes,
            CacheHits = cacheHits,
            CacheMisses = cacheMisses
        };
    }
}
