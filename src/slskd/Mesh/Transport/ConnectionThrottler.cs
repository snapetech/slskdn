// <copyright file="ConnectionThrottler.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Transport;

/// <summary>
/// Throttles connection attempts to prevent DoS attacks and resource exhaustion.
/// </summary>
public class ConnectionThrottler
{
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<ConnectionThrottler> _logger;

    // Rate limits for different types of connection attempts
    private const int GlobalConnectionCapacity = 1000; // per minute
    private const double GlobalConnectionRefillRate = 16.67; // ~1000/minute

    private const int PerIPEndpointCapacity = 10; // per minute
    private const double PerIPEndpointRefillRate = 0.167; // ~10/minute

    private const int PerTransportCapacity = 100; // per minute
    private const double PerTransportRefillRate = 1.67; // ~100/minute

    public ConnectionThrottler(RateLimiter rateLimiter, ILogger<ConnectionThrottler> logger)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if a connection attempt should be allowed based on rate limiting.
    /// </summary>
    /// <param name="remoteEndpoint">The remote endpoint attempting to connect.</param>
    /// <param name="transportType">The transport type being used.</param>
    /// <returns>True if the connection should be allowed.</returns>
    public bool ShouldAllowConnection(string remoteEndpoint, TransportType transportType)
    {
        // Global connection rate limit
        if (!_rateLimiter.TryConsume("global-connections", capacity: GlobalConnectionCapacity, refillRate: GlobalConnectionRefillRate))
        {
            _logger.LogWarning("Global connection rate limit exceeded, blocking connection from {Endpoint}", remoteEndpoint);
            return false;
        }

        // Per-endpoint rate limit
        var endpointKey = $"endpoint-{remoteEndpoint}";
        if (!_rateLimiter.TryConsume(endpointKey, capacity: PerIPEndpointCapacity, refillRate: PerIPEndpointRefillRate))
        {
            _logger.LogWarning("Per-endpoint connection rate limit exceeded for {Endpoint}, blocking connection", remoteEndpoint);
            return false;
        }

        // Per-transport rate limit
        var transportKey = $"transport-{transportType}";
        if (!_rateLimiter.TryConsume(transportKey, capacity: PerTransportCapacity, refillRate: PerTransportRefillRate))
        {
            _logger.LogWarning("Per-transport connection rate limit exceeded for {Transport}, blocking connection from {Endpoint}",
                transportType, remoteEndpoint);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a DHT descriptor fetch should be allowed.
    /// </summary>
    /// <param name="peerId">The peer ID being queried.</param>
    /// <returns>True if the fetch should be allowed.</returns>
    public bool ShouldAllowDescriptorFetch(string peerId)
    {
        // Rate limit descriptor fetches to prevent DHT abuse
        const int descriptorCapacity = 50; // per minute
        const double descriptorRefillRate = 0.833; // ~50/minute

        var fetchKey = $"descriptor-fetch-{peerId}";
        if (!_rateLimiter.TryConsume(fetchKey, capacity: descriptorCapacity, refillRate: descriptorRefillRate))
        {
            _logger.LogWarning("Descriptor fetch rate limit exceeded for peer {PeerId}", peerId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a control envelope should be processed.
    /// </summary>
    /// <param name="peerId">The peer ID sending the envelope.</param>
    /// <param name="envelopeType">The envelope type.</param>
    /// <returns>True if the envelope should be processed.</returns>
    public bool ShouldAllowEnvelopeProcessing(string peerId, string envelopeType)
    {
        // Rate limit control envelopes to prevent spam
        const int envelopeCapacity = 60; // per minute
        const double envelopeRefillRate = 1.0; // ~60/minute

        var envelopeKey = $"envelope-{peerId}-{envelopeType}";
        if (!_rateLimiter.TryConsume(envelopeKey, capacity: envelopeCapacity, refillRate: envelopeRefillRate))
        {
            _logger.LogWarning("Control envelope rate limit exceeded for peer {PeerId}, type {Type}", peerId, envelopeType);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reports a successful authentication for rate limit adjustment.
    /// </summary>
    /// <param name="peerId">The authenticated peer ID.</param>
    public void ReportSuccessfulAuth(string peerId)
    {
        // Could implement reputation-based rate limit increases for trusted peers
        // For now, just log successful authentications
        _logger.LogDebug("Successful authentication reported for peer {PeerId}", peerId);
    }

    /// <summary>
    /// Reports a failed authentication attempt.
    /// </summary>
    /// <param name="remoteEndpoint">The remote endpoint.</param>
    /// <param name="reason">The failure reason.</param>
    public void ReportFailedAuth(string remoteEndpoint, string reason)
    {
        // Implement progressive backoff for failed auth attempts
        var failureKey = $"auth-failure-{remoteEndpoint}";

        // More aggressive rate limiting for endpoints with repeated failures
        const int failureCapacity = 5; // per minute
        const double failureRefillRate = 0.083; // ~5/minute

        if (!_rateLimiter.TryConsume(failureKey, capacity: failureCapacity, refillRate: failureRefillRate))
        {
            _logger.LogWarning("Excessive authentication failures from {Endpoint}, reason: {Reason}", remoteEndpoint, reason);
        }
    }

    /// <summary>
    /// Gets statistics about connection throttling.
    /// </summary>
    /// <returns>Connection throttling statistics.</returns>
    public ConnectionThrottlerStatistics GetStatistics()
    {
        var rateLimiterStats = _rateLimiter.GetStatistics();

        return new ConnectionThrottlerStatistics
        {
            ActiveBuckets = rateLimiterStats.ActiveBuckets,
            TotalRequestsBlocked = rateLimiterStats.TotalRequestsBlocked,
            GlobalConnectionTokens = _rateLimiter.GetCurrentTokens("global-connections")
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
/// Statistics about connection throttling operations.
/// </summary>
public class ConnectionThrottlerStatistics
{
    /// <summary>
    /// Gets or sets the number of active rate limit buckets.
    /// </summary>
    public int ActiveBuckets { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests blocked by throttling.
    /// </summary>
    public long TotalRequestsBlocked { get; set; }

    /// <summary>
    /// Gets or sets the current global connection tokens available.
    /// </summary>
    public int GlobalConnectionTokens { get; set; }
}
