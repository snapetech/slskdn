// <copyright file="NetworkGuard.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides network-level security guards for incoming connections and messages.
/// SECURITY: Rate limiting, connection caps, and abuse prevention.
/// </summary>
public sealed class NetworkGuard : IDisposable
{
    private readonly ILogger<NetworkGuard> _logger;
    private readonly ConcurrentDictionary<IPAddress, ConnectionTracker> _connectionTrackers = new();
    private readonly ConcurrentDictionary<IPAddress, MessageRateTracker> _messageRateTrackers = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Maximum concurrent connections per IP.
    /// </summary>
    public int MaxConnectionsPerIp { get; init; } = 3;

    /// <summary>
    /// Maximum global concurrent connections.
    /// </summary>
    public int MaxGlobalConnections { get; init; } = 100;

    /// <summary>
    /// Maximum messages per IP per minute.
    /// </summary>
    public int MaxMessagesPerMinute { get; init; } = 60;

    /// <summary>
    /// Maximum message size in bytes.
    /// </summary>
    public int MaxMessageSize { get; init; } = 65536; // 64KB

    /// <summary>
    /// Maximum pending requests per IP.
    /// </summary>
    public int MaxPendingRequestsPerIp { get; init; } = 10;

    /// <summary>
    /// Time window for rate limiting.
    /// </summary>
    public TimeSpan RateWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Current global connection count.
    /// </summary>
    private int _globalConnectionCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkGuard"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public NetworkGuard(ILogger<NetworkGuard> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Check if a new connection should be allowed.
    /// </summary>
    /// <param name="remoteIp">Remote IP address.</param>
    /// <returns>True if connection is allowed.</returns>
    public bool AllowConnection(IPAddress remoteIp)
    {
        // Check global limit
        if (_globalConnectionCount >= MaxGlobalConnections)
        {
            _logger.LogWarning("Connection rejected: global limit reached ({Count}/{Max})", _globalConnectionCount, MaxGlobalConnections);
            return false;
        }

        // Check per-IP limit
        var tracker = _connectionTrackers.GetOrAdd(remoteIp, _ => new ConnectionTracker());
        if (tracker.ActiveConnections >= MaxConnectionsPerIp)
        {
            _logger.LogWarning("Connection rejected from {Ip}: per-IP limit reached ({Count}/{Max})", remoteIp, tracker.ActiveConnections, MaxConnectionsPerIp);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Register a new connection.
    /// </summary>
    /// <param name="remoteIp">Remote IP address.</param>
    /// <returns>Connection ID for tracking.</returns>
    public string RegisterConnection(IPAddress remoteIp)
    {
        var connectionId = Guid.NewGuid().ToString("N")[..12];

        var tracker = _connectionTrackers.GetOrAdd(remoteIp, _ => new ConnectionTracker());
        lock (tracker)
        {
            tracker.ActiveConnections++;
            tracker.TotalConnections++;
            tracker.ConnectionIds.Add(connectionId);
        }

        Interlocked.Increment(ref _globalConnectionCount);

        _logger.LogDebug("Connection registered: {Id} from {Ip} (now {Count} from this IP)", connectionId, remoteIp, tracker.ActiveConnections);

        return connectionId;
    }

    /// <summary>
    /// Unregister a connection.
    /// </summary>
    /// <param name="remoteIp">Remote IP address.</param>
    /// <param name="connectionId">Connection ID.</param>
    public void UnregisterConnection(IPAddress remoteIp, string connectionId)
    {
        var wasRemoved = false;

        if (_connectionTrackers.TryGetValue(remoteIp, out var tracker))
        {
            lock (tracker)
            {
                if (tracker.ConnectionIds.Remove(connectionId))
                {
                    tracker.ActiveConnections = Math.Max(0, tracker.ActiveConnections - 1);
                    wasRemoved = true;
                }
            }
        }

        // BUG FIX: Only decrement global count if we actually removed a tracked connection
        // This prevents counter going negative from duplicate/untracked unregister calls
        if (wasRemoved)
        {
            // Use CompareExchange to prevent going below 0
            int current, newValue;
            do
            {
                current = _globalConnectionCount;
                newValue = Math.Max(0, current - 1);
            }
            while (Interlocked.CompareExchange(ref _globalConnectionCount, newValue, current) != current);
        }

        _logger.LogDebug("Connection unregistered: {Id} from {Ip} (removed: {Removed})", connectionId, remoteIp, wasRemoved);
    }

    /// <summary>
    /// Check if a message should be allowed (rate limiting).
    /// </summary>
    /// <param name="remoteIp">Remote IP address.</param>
    /// <param name="messageSize">Size of the message in bytes.</param>
    /// <returns>True if message is allowed.</returns>
    public bool AllowMessage(IPAddress remoteIp, int messageSize)
    {
        // Check message size
        if (messageSize > MaxMessageSize)
        {
            _logger.LogWarning("Message rejected from {Ip}: size {Size} exceeds max {Max}", remoteIp, messageSize, MaxMessageSize);
            return false;
        }

        // Check rate limit
        var tracker = _messageRateTrackers.GetOrAdd(remoteIp, _ => new MessageRateTracker());
        var now = DateTimeOffset.UtcNow;

        lock (tracker)
        {
            // Clean old entries
            while (tracker.MessageTimestamps.Count > 0 &&
                   tracker.MessageTimestamps.Peek() < now - RateWindow)
            {
                tracker.MessageTimestamps.Dequeue();
            }

            // Check rate
            if (tracker.MessageTimestamps.Count >= MaxMessagesPerMinute)
            {
                _logger.LogWarning("Message rejected from {Ip}: rate limit exceeded ({Count}/{Max} per minute)", remoteIp, tracker.MessageTimestamps.Count, MaxMessagesPerMinute);
                tracker.RateLimitHits++;
                return false;
            }

            // Record this message
            tracker.MessageTimestamps.Enqueue(now);
            tracker.TotalMessages++;
        }

        return true;
    }

    /// <summary>
    /// Check if a request should be allowed (pending request limiting).
    /// </summary>
    /// <param name="remoteIp">Remote IP address.</param>
    /// <returns>True if request is allowed.</returns>
    public bool AllowRequest(IPAddress remoteIp)
    {
        var tracker = _connectionTrackers.GetOrAdd(remoteIp, _ => new ConnectionTracker());

        lock (tracker)
        {
            if (tracker.PendingRequests >= MaxPendingRequestsPerIp)
            {
                _logger.LogWarning("Request rejected from {Ip}: too many pending requests ({Count}/{Max})", remoteIp, tracker.PendingRequests, MaxPendingRequestsPerIp);
                return false;
            }

            tracker.PendingRequests++;
        }

        return true;
    }

    /// <summary>
    /// Complete a pending request.
    /// </summary>
    /// <param name="remoteIp">Remote IP address.</param>
    public void CompleteRequest(IPAddress remoteIp)
    {
        if (_connectionTrackers.TryGetValue(remoteIp, out var tracker))
        {
            lock (tracker)
            {
                tracker.PendingRequests = Math.Max(0, tracker.PendingRequests - 1);
            }
        }
    }

    /// <summary>
    /// Get statistics about network guards.
    /// </summary>
    public NetworkGuardStats GetStats()
    {
        return new NetworkGuardStats
        {
            GlobalConnections = _globalConnectionCount,
            TrackedIps = _connectionTrackers.Count,
            TotalConnections = _connectionTrackers.Values.Sum(t => t.TotalConnections),
            TotalMessages = _messageRateTrackers.Values.Sum(t => t.TotalMessages),
            RateLimitHits = _messageRateTrackers.Values.Sum(t => t.RateLimitHits),
            MaxConnectionsPerIp = MaxConnectionsPerIp,
            MaxGlobalConnections = MaxGlobalConnections,
            MaxMessagesPerMinute = MaxMessagesPerMinute,
        };
    }

    /// <summary>
    /// Get connection info for an IP.
    /// </summary>
    public ConnectionInfo? GetConnectionInfo(IPAddress ip)
    {
        if (!_connectionTrackers.TryGetValue(ip, out var tracker))
        {
            return null;
        }

        return new ConnectionInfo
        {
            Ip = ip,
            ActiveConnections = tracker.ActiveConnections,
            TotalConnections = tracker.TotalConnections,
            PendingRequests = tracker.PendingRequests,
        };
    }

    /// <summary>
    /// Get IPs with most connections.
    /// </summary>
    public IReadOnlyList<ConnectionInfo> GetTopConnectors(int limit = 10)
    {
        return _connectionTrackers
            .Select(kvp => new ConnectionInfo
            {
                Ip = kvp.Key,
                ActiveConnections = kvp.Value.ActiveConnections,
                TotalConnections = kvp.Value.TotalConnections,
                PendingRequests = kvp.Value.PendingRequests,
            })
            .OrderByDescending(c => c.ActiveConnections)
            .ThenByDescending(c => c.TotalConnections)
            .Take(limit)
            .ToList();
    }

    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;

        // Clean up connection trackers with no active connections
        var toRemove = _connectionTrackers
            .Where(kvp => kvp.Value.ActiveConnections == 0 && kvp.Value.LastActivity < now.AddMinutes(-5))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var ip in toRemove)
        {
            _connectionTrackers.TryRemove(ip, out _);
        }

        // Clean up message rate trackers
        var rateToRemove = _messageRateTrackers
            .Where(kvp => kvp.Value.MessageTimestamps.Count == 0)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var ip in rateToRemove)
        {
            _messageRateTrackers.TryRemove(ip, out _);
        }

        if (toRemove.Count > 0 || rateToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Conn} connection trackers, {Rate} rate trackers", toRemove.Count, rateToRemove.Count);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private sealed class ConnectionTracker
    {
        public int ActiveConnections { get; set; }
        public long TotalConnections { get; set; }
        public int PendingRequests { get; set; }
        public HashSet<string> ConnectionIds { get; } = new();
        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class MessageRateTracker
    {
        public Queue<DateTimeOffset> MessageTimestamps { get; } = new();
        public long TotalMessages { get; set; }
        public long RateLimitHits { get; set; }
    }
}

/// <summary>
/// Statistics about network guards.
/// </summary>
public sealed class NetworkGuardStats
{
    /// <summary>Gets or sets current global connection count.</summary>
    public int GlobalConnections { get; init; }

    /// <summary>Gets or sets number of IPs being tracked.</summary>
    public int TrackedIps { get; init; }

    /// <summary>Gets or sets total connections ever made.</summary>
    public long TotalConnections { get; init; }

    /// <summary>Gets or sets total messages ever processed.</summary>
    public long TotalMessages { get; init; }

    /// <summary>Gets or sets total rate limit hits.</summary>
    public long RateLimitHits { get; init; }

    /// <summary>Gets or sets max connections per IP setting.</summary>
    public int MaxConnectionsPerIp { get; init; }

    /// <summary>Gets or sets max global connections setting.</summary>
    public int MaxGlobalConnections { get; init; }

    /// <summary>Gets or sets max messages per minute setting.</summary>
    public int MaxMessagesPerMinute { get; init; }
}

/// <summary>
/// Connection information for an IP.
/// </summary>
public sealed class ConnectionInfo
{
    /// <summary>Gets or sets the IP address.</summary>
    public required IPAddress Ip { get; init; }

    /// <summary>Gets or sets active connection count.</summary>
    public int ActiveConnections { get; init; }

    /// <summary>Gets or sets total historical connections.</summary>
    public long TotalConnections { get; init; }

    /// <summary>Gets or sets pending request count.</summary>
    public int PendingRequests { get; init; }
}

