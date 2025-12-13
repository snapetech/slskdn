// <copyright file="MeshRateLimiter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

/// <summary>
/// Rate limiter for mesh control messages to prevent DoS attacks.
/// Tracks both pre-auth (by IP) and post-auth (by PeerId) rates.
/// </summary>
public interface IMeshRateLimiter
{
    /// <summary>
    /// Checks if a pre-authentication request from an IP is within rate limits.
    /// </summary>
    bool AllowPreAuth(IPAddress ip);

    /// <summary>
    /// Checks if a post-authentication request from a PeerId is within rate limits.
    /// </summary>
    bool AllowPostAuth(string peerId);

    /// <summary>
    /// Purges expired rate limit entries.
    /// </summary>
    void PurgeExpired();
}

public class MeshRateLimiter : IMeshRateLimiter
{
    private readonly ILogger<MeshRateLimiter> logger;
    private readonly ConcurrentDictionary<string, RateLimitBucket> ipBuckets = new();
    private readonly ConcurrentDictionary<string, RateLimitBucket> peerBuckets = new();

    // Configuration: 100 requests per minute for pre-auth (aggressive filtering)
    private const int PreAuthLimit = 100;
    private static readonly TimeSpan PreAuthWindow = TimeSpan.FromMinutes(1);

    // Configuration: 500 requests per minute for post-auth (more lenient)
    private const int PostAuthLimit = 500;
    private static readonly TimeSpan PostAuthWindow = TimeSpan.FromMinutes(1);

    public MeshRateLimiter(ILogger<MeshRateLimiter> logger)
    {
        this.logger = logger;
    }

    public bool AllowPreAuth(IPAddress ip)
    {
        var key = ip.ToString();
        var bucket = ipBuckets.GetOrAdd(key, _ => new RateLimitBucket(PreAuthLimit, PreAuthWindow));

        if (!bucket.TryConsume())
        {
            logger.LogWarning("[MeshRateLimiter] Pre-auth rate limit exceeded for IP: {IP}", ip);
            return false;
        }

        return true;
    }

    public bool AllowPostAuth(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return false;
        }

        var bucket = peerBuckets.GetOrAdd(peerId, _ => new RateLimitBucket(PostAuthLimit, PostAuthWindow));

        if (!bucket.TryConsume())
        {
            logger.LogWarning("[MeshRateLimiter] Post-auth rate limit exceeded for PeerId: {PeerId}", peerId);
            return false;
        }

        return true;
    }

    public void PurgeExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - PreAuthWindow;
        var purgedIp = 0;
        var purgedPeer = 0;

        foreach (var (key, bucket) in ipBuckets)
        {
            if (bucket.LastReset < cutoff)
            {
                ipBuckets.TryRemove(key, out _);
                purgedIp++;
            }
        }

        foreach (var (key, bucket) in peerBuckets)
        {
            if (bucket.LastReset < cutoff)
            {
                peerBuckets.TryRemove(key, out _);
                purgedPeer++;
            }
        }

        if (purgedIp > 0 || purgedPeer > 0)
        {
            logger.LogDebug("[MeshRateLimiter] Purged {IpCount} IP + {PeerCount} peer buckets", purgedIp, purgedPeer);
        }
    }

    private class RateLimitBucket
    {
        private readonly int limit;
        private readonly TimeSpan window;
        private readonly object lockObj = new();
        private int count;
        private DateTimeOffset resetTime;

        public DateTimeOffset LastReset { get; private set; }

        public RateLimitBucket(int limit, TimeSpan window)
        {
            this.limit = limit;
            this.window = window;
            this.resetTime = DateTimeOffset.UtcNow + window;
            this.LastReset = DateTimeOffset.UtcNow;
        }

        public bool TryConsume()
        {
            lock (lockObj)
            {
                var now = DateTimeOffset.UtcNow;

                // Reset bucket if window expired
                if (now >= resetTime)
                {
                    count = 0;
                    resetTime = now + window;
                    LastReset = now;
                }

                // Check limit
                if (count >= limit)
                {
                    return false;
                }

                count++;
                return true;
            }
        }
    }
}

