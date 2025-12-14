// <copyright file="RateLimiter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace slskd.Mesh.Transport;

/// <summary>
/// Token bucket rate limiter for preventing DoS attacks and resource exhaustion.
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly ILogger<RateLimiter> _logger;

    public RateLimiter(ILogger<RateLimiter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts to consume tokens from the specified bucket.
    /// </summary>
    /// <param name="bucketKey">The bucket identifier.</param>
    /// <param name="tokens">The number of tokens to consume.</param>
    /// <param name="capacity">The bucket capacity (tokens).</param>
    /// <param name="refillRate">The refill rate (tokens per second).</param>
    /// <returns>True if tokens were successfully consumed.</returns>
    public bool TryConsume(string bucketKey, int tokens = 1, int capacity = 100, double refillRate = 10.0)
    {
        var bucket = _buckets.GetOrAdd(bucketKey, _ => new TokenBucket(capacity, refillRate));
        var success = bucket.TryConsume(tokens);

        if (!success)
        {
            _logger.LogWarning("Rate limit exceeded for bucket {Bucket}: capacity={Capacity}, refillRate={RefillRate}",
                bucketKey, capacity, refillRate);
        }

        return success;
    }

    /// <summary>
    /// Gets the current token count for a bucket.
    /// </summary>
    /// <param name="bucketKey">The bucket identifier.</param>
    /// <returns>The current token count.</returns>
    public int GetCurrentTokens(string bucketKey)
    {
        if (_buckets.TryGetValue(bucketKey, out var bucket))
        {
            return bucket.GetCurrentTokens();
        }
        return 0;
    }

    /// <summary>
    /// Resets a bucket to full capacity.
    /// </summary>
    /// <param name="bucketKey">The bucket identifier.</param>
    public void ResetBucket(string bucketKey)
    {
        if (_buckets.TryGetValue(bucketKey, out var bucket))
        {
            bucket.Reset();
        }
    }

    /// <summary>
    /// Cleans up expired buckets to prevent memory leaks.
    /// </summary>
    /// <param name="maxAge">Maximum age for buckets to keep.</param>
    public void CleanupExpiredBuckets(TimeSpan maxAge = default)
    {
        if (maxAge == default)
        {
            maxAge = TimeSpan.FromHours(1);
        }

        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var expiredKeys = new List<string>();

        foreach (var kvp in _buckets)
        {
            if (kvp.Value.LastAccess < cutoff)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _buckets.TryRemove(key, out _);
        }

        if (expiredKeys.Any())
        {
            _logger.LogDebug("Cleaned up {Count} expired rate limit buckets", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Gets statistics about rate limiting.
    /// </summary>
    /// <returns>Rate limiting statistics.</returns>
    public RateLimiterStatistics GetStatistics()
    {
        var activeBuckets = _buckets.Count;
        var totalTokensConsumed = _buckets.Sum(b => b.Value.TokensConsumed);
        var totalRequestsBlocked = _buckets.Sum(b => b.Value.RequestsBlocked);

        return new RateLimiterStatistics
        {
            ActiveBuckets = activeBuckets,
            TotalTokensConsumed = totalTokensConsumed,
            TotalRequestsBlocked = totalRequestsBlocked
        };
    }

    /// <summary>
    /// Token bucket implementation with thread-safe operations.
    /// </summary>
    private class TokenBucket
    {
        private readonly int _capacity;
        private readonly double _refillRate; // tokens per second
        private double _tokens;
        private DateTimeOffset _lastRefill;
        private readonly object _lock = new();

        public DateTimeOffset LastAccess { get; private set; }
        public long TokensConsumed { get; private set; }
        public long RequestsBlocked { get; private set; }

        public TokenBucket(int capacity, double refillRate)
        {
            _capacity = capacity;
            _refillRate = refillRate;
            _tokens = capacity;
            _lastRefill = DateTimeOffset.UtcNow;
            LastAccess = DateTimeOffset.UtcNow;
        }

        public bool TryConsume(int tokens)
        {
            lock (_lock)
            {
                LastAccess = DateTimeOffset.UtcNow;
                RefillTokens();

                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    TokensConsumed += tokens;
                    return true;
                }
                else
                {
                    RequestsBlocked++;
                    return false;
                }
            }
        }

        public int GetCurrentTokens()
        {
            lock (_lock)
            {
                RefillTokens();
                return (int)_tokens;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _tokens = _capacity;
                _lastRefill = DateTimeOffset.UtcNow;
                TokensConsumed = 0;
                RequestsBlocked = 0;
                LastAccess = DateTimeOffset.UtcNow;
            }
        }

        private void RefillTokens()
        {
            var now = DateTimeOffset.UtcNow;
            var timePassed = (now - _lastRefill).TotalSeconds;

            if (timePassed > 0)
            {
                var tokensToAdd = timePassed * _refillRate;
                _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                _lastRefill = now;
            }
        }
    }
}

/// <summary>
/// Statistics about rate limiting operations.
/// </summary>
public class RateLimiterStatistics
{
    /// <summary>
    /// Gets or sets the number of active buckets.
    /// </summary>
    public int ActiveBuckets { get; set; }

    /// <summary>
    /// Gets or sets the total number of tokens consumed.
    /// </summary>
    public long TotalTokensConsumed { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests blocked.
    /// </summary>
    public long TotalRequestsBlocked { get; set; }
}
