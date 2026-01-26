// <copyright file="OverlayRateLimiter.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

/// <summary>
/// Rate limiter for overlay connections to prevent DoS attacks.
/// Tracks connection and message rates per IP and globally.
/// </summary>
public sealed class OverlayRateLimiter : IDisposable
{
    // Connection rate limits
    /// <summary>Maximum simultaneous connections from a single IP.</summary>
    public const int MaxConnectionsPerIp = 3;
    
    /// <summary>Maximum new connections per minute globally.</summary>
    public const int MaxConnectionsPerMinute = 30;
    
    /// <summary>Maximum total overlay connections.</summary>
    public const int MaxTotalConnections = 100;
    
    // Message rate limits
    /// <summary>Maximum messages per second per connection.</summary>
    public const int MaxMessagesPerSecond = 10;
    
    /// <summary>Maximum delta sync requests per hour per peer.</summary>
    public const int MaxDeltaRequestsPerHour = 60;

    /// <summary>Maximum mesh search requests per minute per peer.</summary>
    public const int MaxMeshSearchRequestsPerMinute = 30;
    
    // Violation handling
    /// <summary>Seconds to block after violations.</summary>
    public const int ViolationBackoffSeconds = 300; // 5 minutes
    
    /// <summary>Violations before permanent ban.</summary>
    public const int MaxViolationsBeforeBan = 3;
    
    private readonly ConcurrentDictionary<IPAddress, IpRateLimitState> _ipStates = new();
    private readonly ConcurrentDictionary<string, ConnectionRateLimitState> _connectionStates = new();
    private readonly object _globalLock = new();
    
    private int _totalConnections;
    private readonly Queue<DateTimeOffset> _recentConnections = new();
    private readonly Timer _cleanupTimer;
    
    public OverlayRateLimiter()
    {
        // Cleanup stale entries every minute
        _cleanupTimer = new Timer(CleanupStaleEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
    
    /// <summary>
    /// Check if a new connection from an IP is allowed.
    /// </summary>
    /// <param name="ip">The connecting IP address.</param>
    /// <returns>True if connection is allowed, false if rate limited.</returns>
    public RateLimitResult CheckConnection(IPAddress ip)
    {
        var state = _ipStates.GetOrAdd(ip, _ => new IpRateLimitState());
        var now = DateTimeOffset.UtcNow;
        
        lock (state.Lock)
        {
            // Check if IP is in backoff period
            if (state.BackoffUntil > now)
            {
                return RateLimitResult.Blocked($"IP in backoff until {state.BackoffUntil}");
            }
            
            // Check per-IP connection limit
            if (state.ActiveConnections >= MaxConnectionsPerIp)
            {
                state.Violations++;
                if (state.Violations >= MaxViolationsBeforeBan)
                {
                    state.BackoffUntil = now.AddSeconds(ViolationBackoffSeconds);
                }
                
                return RateLimitResult.RateLimited("Too many connections from this IP");
            }
        }
        
        // Check global limits
        lock (_globalLock)
        {
            // Total connection limit
            if (_totalConnections >= MaxTotalConnections)
            {
                return RateLimitResult.RateLimited("Server at maximum connections");
            }
            
            // Connections per minute limit
            while (_recentConnections.Count > 0 && _recentConnections.Peek() < now.AddMinutes(-1))
            {
                _recentConnections.Dequeue();
            }
            
            if (_recentConnections.Count >= MaxConnectionsPerMinute)
            {
                return RateLimitResult.RateLimited("Too many connections per minute");
            }
            
            // All checks passed - record connection
            _recentConnections.Enqueue(now);
            _totalConnections++;
        }
        
        lock (state.Lock)
        {
            state.ActiveConnections++;
            state.LastConnectionTime = now;
        }
        
        return RateLimitResult.Allowed();
    }
    
    /// <summary>
    /// Record that a connection from an IP has closed.
    /// </summary>
    public void RecordDisconnection(IPAddress ip)
    {
        if (_ipStates.TryGetValue(ip, out var state))
        {
            lock (state.Lock)
            {
                state.ActiveConnections = Math.Max(0, state.ActiveConnections - 1);
            }
        }
        
        lock (_globalLock)
        {
            _totalConnections = Math.Max(0, _totalConnections - 1);
        }
    }
    
    /// <summary>
    /// Check if a message is allowed for a connection.
    /// </summary>
    /// <param name="connectionId">Unique connection identifier.</param>
    /// <returns>True if message is allowed.</returns>
    public RateLimitResult CheckMessage(string connectionId)
    {
        var state = _connectionStates.GetOrAdd(connectionId, _ => new ConnectionRateLimitState());
        var now = DateTimeOffset.UtcNow;
        
        lock (state.Lock)
        {
            // Clean old entries
            while (state.MessageTimes.Count > 0 && state.MessageTimes.Peek() < now.AddSeconds(-1))
            {
                state.MessageTimes.Dequeue();
            }
            
            if (state.MessageTimes.Count >= MaxMessagesPerSecond)
            {
                return RateLimitResult.RateLimited("Message rate limit exceeded");
            }
            
            state.MessageTimes.Enqueue(now);
            return RateLimitResult.Allowed();
        }
    }
    
    /// <summary>
    /// Check if a delta sync request is allowed.
    /// </summary>
    /// <param name="peerId">The peer requesting sync.</param>
    public RateLimitResult CheckDeltaRequest(string peerId)
    {
        var state = _connectionStates.GetOrAdd(peerId, _ => new ConnectionRateLimitState());
        var now = DateTimeOffset.UtcNow;
        
        lock (state.Lock)
        {
            // Clean old entries
            while (state.DeltaRequestTimes.Count > 0 && state.DeltaRequestTimes.Peek() < now.AddHours(-1))
            {
                state.DeltaRequestTimes.Dequeue();
            }
            
            if (state.DeltaRequestTimes.Count >= MaxDeltaRequestsPerHour)
            {
                return RateLimitResult.RateLimited("Delta sync rate limit exceeded");
            }
            
            state.DeltaRequestTimes.Enqueue(now);
            return RateLimitResult.Allowed();
        }
    }

    /// <summary>
    /// Check if a mesh search request is allowed.
    /// </summary>
    /// <param name="connectionId">The connection/peer sending the request.</param>
    public RateLimitResult CheckMeshSearchRequest(string connectionId)
    {
        var state = _connectionStates.GetOrAdd(connectionId, _ => new ConnectionRateLimitState());
        var now = DateTimeOffset.UtcNow;

        lock (state.Lock)
        {
            while (state.MeshSearchRequestTimes.Count > 0 && state.MeshSearchRequestTimes.Peek() < now.AddMinutes(-1))
            {
                state.MeshSearchRequestTimes.Dequeue();
            }

            if (state.MeshSearchRequestTimes.Count >= MaxMeshSearchRequestsPerMinute)
            {
                return RateLimitResult.RateLimited("Mesh search rate limit exceeded");
            }

            state.MeshSearchRequestTimes.Enqueue(now);
            return RateLimitResult.Allowed();
        }
    }
    
    /// <summary>
    /// Record a protocol violation from an IP.
    /// </summary>
    public void RecordViolation(IPAddress ip)
    {
        var state = _ipStates.GetOrAdd(ip, _ => new IpRateLimitState());
        var now = DateTimeOffset.UtcNow;
        
        lock (state.Lock)
        {
            state.Violations++;
            
            if (state.Violations >= MaxViolationsBeforeBan)
            {
                state.BackoffUntil = now.AddSeconds(ViolationBackoffSeconds);
            }
        }
    }
    
    /// <summary>
    /// Remove connection state when connection is closed.
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        _connectionStates.TryRemove(connectionId, out _);
    }
    
    /// <summary>
    /// Get current statistics.
    /// </summary>
    public RateLimiterStats GetStats()
    {
        lock (_globalLock)
        {
            return new RateLimiterStats
            {
                TotalConnections = _totalConnections,
                ConnectionsLastMinute = _recentConnections.Count,
                TrackedIps = _ipStates.Count,
                TrackedConnections = _connectionStates.Count,
                BlockedIps = _ipStates.Count(kvp =>
                {
                    lock (kvp.Value.Lock)
                    {
                        return kvp.Value.BackoffUntil > DateTimeOffset.UtcNow;
                    }
                }),
            };
        }
    }
    
    private void CleanupStaleEntries(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var staleThreshold = now.AddMinutes(-10);
        
        // Clean up IP states with no recent activity
        foreach (var kvp in _ipStates)
        {
            lock (kvp.Value.Lock)
            {
                if (kvp.Value.ActiveConnections == 0 &&
                    kvp.Value.LastConnectionTime < staleThreshold &&
                    kvp.Value.BackoffUntil < now)
                {
                    _ipStates.TryRemove(kvp.Key, out _);
                }
            }
        }
        
        // Clean up connection states with no recent activity
        foreach (var kvp in _connectionStates)
        {
            lock (kvp.Value.Lock)
            {
                if (kvp.Value.MessageTimes.Count == 0 && kvp.Value.DeltaRequestTimes.Count == 0 && kvp.Value.MeshSearchRequestTimes.Count == 0)
                {
                    _connectionStates.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
    
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
    
    private sealed class IpRateLimitState
    {
        public object Lock { get; } = new();
        public int ActiveConnections { get; set; }
        public DateTimeOffset LastConnectionTime { get; set; }
        public int Violations { get; set; }
        public DateTimeOffset BackoffUntil { get; set; }
    }
    
    private sealed class ConnectionRateLimitState
    {
        public object Lock { get; } = new();
        public Queue<DateTimeOffset> MessageTimes { get; } = new();
        public Queue<DateTimeOffset> DeltaRequestTimes { get; } = new();
        public Queue<DateTimeOffset> MeshSearchRequestTimes { get; } = new();
    }
}

/// <summary>
/// Result of a rate limit check.
/// </summary>
public readonly struct RateLimitResult
{
    public bool IsAllowed { get; }
    public string? Reason { get; }
    
    private RateLimitResult(bool isAllowed, string? reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }
    
    public static RateLimitResult Allowed() => new(true, null);
    public static RateLimitResult RateLimited(string reason) => new(false, reason);
    public static RateLimitResult Blocked(string reason) => new(false, reason);
    
    public static implicit operator bool(RateLimitResult result) => result.IsAllowed;
}

/// <summary>
/// Rate limiter statistics.
/// </summary>
public sealed class RateLimiterStats
{
    public int TotalConnections { get; init; }
    public int ConnectionsLastMinute { get; init; }
    public int TrackedIps { get; init; }
    public int TrackedConnections { get; init; }
    public int BlockedIps { get; init; }
}

