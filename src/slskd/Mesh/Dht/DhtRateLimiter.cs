// <copyright file="DhtRateLimiter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Dht;

/// <summary>
/// Rate limiter specifically for DHT operations to prevent abuse and resource exhaustion.
/// </summary>
public class DhtRateLimiter
{
    private readonly Transport.RateLimiter _rateLimiter;
    private readonly ILogger<DhtRateLimiter> _logger;

    // DHT-specific rate limits
    private const int DescriptorFetchCapacity = 100; // per minute
    private const double DescriptorFetchRefillRate = 1.67; // ~100/minute

    private const int PublishCapacity = 20; // per minute
    private const double PublishRefillRate = 0.333; // ~20/minute

    private const int QueryCapacity = 200; // per minute
    private const double QueryRefillRate = 3.33; // ~200/minute

    public DhtRateLimiter(Transport.RateLimiter rateLimiter, ILogger<DhtRateLimiter> logger)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if a descriptor fetch should be allowed.
    /// </summary>
    /// <param name="peerId">The peer ID being queried.</param>
    /// <returns>True if the fetch should be allowed.</returns>
    public bool ShouldAllowDescriptorFetch(string peerId)
    {
        var fetchKey = $"dht-descriptor-fetch-{peerId}";
        if (!_rateLimiter.TryConsume(fetchKey, capacity: DescriptorFetchCapacity, refillRate: DescriptorFetchRefillRate))
        {
            _logger.LogWarning("DHT descriptor fetch rate limit exceeded for peer {PeerId}", peerId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a descriptor publish should be allowed.
    /// </summary>
    /// <param name="peerId">The peer ID publishing.</param>
    /// <returns>True if the publish should be allowed.</returns>
    public bool ShouldAllowDescriptorPublish(string peerId)
    {
        var publishKey = $"dht-descriptor-publish-{peerId}";
        if (!_rateLimiter.TryConsume(publishKey, capacity: PublishCapacity, refillRate: PublishRefillRate))
        {
            _logger.LogWarning("DHT descriptor publish rate limit exceeded for peer {PeerId}", peerId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a DHT query should be allowed.
    /// </summary>
    /// <param name="queryType">The type of query.</param>
    /// <param name="requesterId">The requester ID.</param>
    /// <returns>True if the query should be allowed.</returns>
    public bool ShouldAllowQuery(string queryType, string requesterId)
    {
        var queryKey = $"dht-query-{queryType}-{requesterId}";
        if (!_rateLimiter.TryConsume(queryKey, capacity: QueryCapacity, refillRate: QueryRefillRate))
        {
            _logger.LogWarning("DHT query rate limit exceeded for {QueryType} by {RequesterId}", queryType, requesterId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reports a successful DHT operation for potential reputation adjustments.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="peerId">The peer ID.</param>
    public void ReportSuccessfulOperation(string operationType, string peerId)
    {
        // Could implement reputation-based rate limit increases
        _logger.LogDebug("Successful DHT {Operation} reported for peer {PeerId}", operationType, peerId);
    }

    /// <summary>
    /// Reports a failed DHT operation.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="reason">The failure reason.</param>
    public void ReportFailedOperation(string operationType, string peerId, string reason)
    {
        // Implement progressive backoff for failed operations
        var failureKey = $"dht-failure-{operationType}-{peerId}";

        const int failureCapacity = 10; // per minute
        const double failureRefillRate = 0.167; // ~10/minute

        if (!_rateLimiter.TryConsume(failureKey, capacity: failureCapacity, refillRate: failureRefillRate))
        {
            _logger.LogWarning("Excessive DHT {Operation} failures for peer {PeerId}: {Reason}", operationType, peerId, reason);
        }
    }

    /// <summary>
    /// Gets statistics about DHT rate limiting.
    /// </summary>
    /// <returns>DHT rate limiting statistics.</returns>
    public DhtRateLimiterStatistics GetStatistics()
    {
        var rateLimiterStats = _rateLimiter.GetStatistics();

        return new DhtRateLimiterStatistics
        {
            ActiveBuckets = rateLimiterStats.ActiveBuckets,
            TotalRequestsBlocked = rateLimiterStats.TotalRequestsBlocked,
            DescriptorFetchTokens = _rateLimiter.GetCurrentTokens("dht-descriptor-fetch-global")
        };
    }

    /// <summary>
    /// Cleans up expired rate limit buckets.
    /// </summary>
    public void CleanupExpiredBuckets()
    {
        _rateLimiter.CleanupExpiredBuckets();
    }
}

/// <summary>
/// Statistics about DHT rate limiting operations.
/// </summary>
public class DhtRateLimiterStatistics
{
    /// <summary>
    /// Gets or sets the number of active rate limit buckets.
    /// </summary>
    public int ActiveBuckets { get; set; }

    /// <summary>
    /// Gets or sets the total number of DHT requests blocked.
    /// </summary>
    public long TotalRequestsBlocked { get; set; }

    /// <summary>
    /// Gets or sets the current descriptor fetch tokens available.
    /// </summary>
    public int DescriptorFetchTokens { get; set; }
}


