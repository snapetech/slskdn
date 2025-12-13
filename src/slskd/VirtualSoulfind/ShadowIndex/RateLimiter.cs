namespace slskd.VirtualSoulfind.ShadowIndex;

using System.Collections.Concurrent;

/// <summary>
/// Rate limiter for DHT write operations.
/// </summary>
public interface IDhtRateLimiter
{
    Task<bool> TryAcquireAsync(CancellationToken ct = default);
    void Release();
}

/// <summary>
/// Token bucket rate limiter for DHT operations.
/// </summary>
public class DhtRateLimiter : IDhtRateLimiter
{
    private readonly ILogger<DhtRateLimiter> logger;
    private readonly SemaphoreSlim semaphore;
    private readonly int maxOperationsPerMinute;
    private readonly ConcurrentQueue<DateTimeOffset> recentOperations = new();

    public DhtRateLimiter(
        ILogger<DhtRateLimiter> logger,
        int maxOperationsPerMinute = 60)
    {
        this.logger = logger;
        this.maxOperationsPerMinute = maxOperationsPerMinute;
        this.semaphore = new SemaphoreSlim(maxOperationsPerMinute, maxOperationsPerMinute);
    }

    public async Task<bool> TryAcquireAsync(CancellationToken ct)
    {
        // Clean up old operations (older than 1 minute)
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
        while (recentOperations.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            recentOperations.TryDequeue(out _);
            semaphore.Release();
        }

        // Try to acquire token
        var acquired = await semaphore.WaitAsync(0, ct);
        if (acquired)
        {
            recentOperations.Enqueue(DateTimeOffset.UtcNow);
            return true;
        }

        logger.LogWarning("[VSF-RATE-LIMIT] DHT operation rate limit exceeded ({MaxOps}/min)",
            maxOperationsPerMinute);
        return false;
    }

    public void Release()
    {
        // Note: Tokens are automatically released after 1 minute via cleanup
    }
}

/// <summary>
/// Configuration for shadow index operations.
/// </summary>
public class ShadowIndexOptions
{
    /// <summary>
    /// Enable shadow index publishing.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Publish interval in minutes.
    /// </summary>
    public int PublishIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Shard TTL in hours.
    /// </summary>
    public int ShardTTLHours { get; set; } = 1;

    /// <summary>
    /// Maximum shards to publish per interval.
    /// </summary>
    public int MaxShardsPerPublish { get; set; } = 100;

    /// <summary>
    /// Maximum DHT operations per minute.
    /// </summary>
    public int MaxDhtOperationsPerMinute { get; set; } = 60;

    /// <summary>
    /// Enable shard caching (reduce DHT queries).
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// Shard cache TTL in minutes.
    /// </summary>
    public int CacheTTLMinutes { get; set; } = 10;
}















