// <copyright file="SecurityEventSink.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for aggregating security events from all security services.
/// </summary>
public interface ISecurityEventSink
{
    /// <summary>
    /// Report a security event.
    /// </summary>
    /// <param name="evt">The security event.</param>
    void Report(SecurityEvent evt);

    /// <summary>
    /// Report a security event asynchronously.
    /// </summary>
    /// <param name="evt">The security event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportAsync(SecurityEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent events.
    /// </summary>
    /// <param name="count">Maximum number to return.</param>
    /// <param name="minSeverity">Minimum severity to include.</param>
    /// <returns>Recent events.</returns>
    IReadOnlyList<SecurityEvent> GetRecentEvents(int count = 100, SecuritySeverity minSeverity = SecuritySeverity.Info);

    /// <summary>
    /// Get events for a specific IP.
    /// </summary>
    IReadOnlyList<SecurityEvent> GetEventsForIp(IPAddress ip, int count = 100);

    /// <summary>
    /// Get events for a specific username.
    /// </summary>
    IReadOnlyList<SecurityEvent> GetEventsForUser(string username, int count = 100);

    /// <summary>
    /// Get aggregated statistics.
    /// </summary>
    SecurityEventStats GetStats();

    /// <summary>
    /// Event raised when a high-severity event occurs.
    /// </summary>
    event EventHandler<SecurityEventArgs>? HighSeverityEvent;
}

/// <summary>
/// Aggregates security events from all security services.
/// </summary>
public sealed class SecurityEventAggregator : ISecurityEventSink, IDisposable
{
    private readonly ILogger<SecurityEventAggregator> _logger;
    private readonly ConcurrentQueue<SecurityEvent> _events = new();
    private readonly ConcurrentDictionary<string, long> _eventCountsByType = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastEventByType = new();
    private readonly Timer _statsTimer;

    /// <summary>
    /// Maximum events to keep in memory.
    /// </summary>
    public const int MaxEvents = 10000;

    /// <summary>
    /// Event raised when a high-severity event occurs.
    /// </summary>
    public event EventHandler<SecurityEventArgs>? HighSeverityEvent;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityEventAggregator"/> class.
    /// </summary>
    public SecurityEventAggregator(ILogger<SecurityEventAggregator> logger)
    {
        _logger = logger;
        _statsTimer = new Timer(LogStats, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc/>
    public void Report(SecurityEvent evt)
    {
        // Enqueue event
        _events.Enqueue(evt);
        while (_events.Count > MaxEvents)
        {
            _events.TryDequeue(out _);
        }

        // Update counters
        var typeKey = evt.Type.ToString();
        _eventCountsByType.AddOrUpdate(typeKey, 1, (_, count) => count + 1);
        _lastEventByType[typeKey] = evt.Timestamp;

        // Log based on severity
        switch (evt.Severity)
        {
            case SecuritySeverity.Critical:
                _logger.LogCritical(
                    "[SECURITY:{Type}] {Message} - IP:{Ip} User:{User}",
                    evt.Type, evt.Message, evt.IpAddress, evt.Username ?? "(none)");
                HighSeverityEvent?.Invoke(this, new SecurityEventArgs(evt));
                break;
            case SecuritySeverity.High:
                _logger.LogError(
                    "[SECURITY:{Type}] {Message} - IP:{Ip} User:{User}",
                    evt.Type, evt.Message, evt.IpAddress, evt.Username ?? "(none)");
                HighSeverityEvent?.Invoke(this, new SecurityEventArgs(evt));
                break;
            case SecuritySeverity.Medium:
                _logger.LogWarning(
                    "[SECURITY:{Type}] {Message} - IP:{Ip} User:{User}",
                    evt.Type, evt.Message, evt.IpAddress, evt.Username ?? "(none)");
                break;
            case SecuritySeverity.Low:
                _logger.LogInformation(
                    "[SECURITY:{Type}] {Message}",
                    evt.Type, evt.Message);
                break;
            default:
                _logger.LogDebug(
                    "[SECURITY:{Type}] {Message}",
                    evt.Type, evt.Message);
                break;
        }
    }

    /// <inheritdoc/>
    public Task ReportAsync(SecurityEvent evt, CancellationToken cancellationToken = default)
    {
        Report(evt);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SecurityEvent> GetRecentEvents(int count = 100, SecuritySeverity minSeverity = SecuritySeverity.Info)
    {
        return _events
            .Where(e => e.Severity >= minSeverity)
            .Reverse()
            .Take(count)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<SecurityEvent> GetEventsForIp(IPAddress ip, int count = 100)
    {
        var ipStr = ip.ToString();
        return _events
            .Where(e => e.IpAddress == ipStr)
            .Reverse()
            .Take(count)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<SecurityEvent> GetEventsForUser(string username, int count = 100)
    {
        return _events
            .Where(e => e.Username?.Equals(username, StringComparison.OrdinalIgnoreCase) == true)
            .Reverse()
            .Take(count)
            .ToList();
    }

    /// <inheritdoc/>
    public SecurityEventStats GetStats()
    {
        var events = _events.ToList();
        var now = DateTimeOffset.UtcNow;
        var lastHour = events.Where(e => e.Timestamp > now.AddHours(-1)).ToList();

        return new SecurityEventStats
        {
            TotalEvents = events.Count,
            EventsLastHour = lastHour.Count,
            CriticalEvents = events.Count(e => e.Severity == SecuritySeverity.Critical),
            HighEvents = events.Count(e => e.Severity == SecuritySeverity.High),
            MediumEvents = events.Count(e => e.Severity == SecuritySeverity.Medium),
            LowEvents = events.Count(e => e.Severity == SecuritySeverity.Low),
            UniqueIps = events.Where(e => e.IpAddress != null).Select(e => e.IpAddress).Distinct().Count(),
            UniqueUsers = events.Where(e => e.Username != null).Select(e => e.Username).Distinct().Count(),
            EventCountsByType = _eventCountsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LastEventByType = _lastEventByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
    }

    private void LogStats(object? state)
    {
        var stats = GetStats();
        if (stats.EventsLastHour > 0)
        {
            _logger.LogInformation(
                "Security stats: {EventsLastHour} events in last hour ({Critical} critical, {High} high)",
                stats.EventsLastHour, stats.CriticalEvents, stats.HighEvents);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _statsTimer.Dispose();
    }
}

/// <summary>
/// A security event.
/// </summary>
public sealed class SecurityEvent
{
    /// <summary>Gets the event ID.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];

    /// <summary>Gets or sets the event type.</summary>
    public required SecurityEventType Type { get; init; }

    /// <summary>Gets or sets the severity.</summary>
    public required SecuritySeverity Severity { get; init; }

    /// <summary>Gets or sets the message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets or sets when occurred.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the source IP address.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Gets or sets the username.</summary>
    public string? Username { get; init; }

    /// <summary>Gets or sets the source component.</summary>
    public string? Source { get; init; }

    /// <summary>Gets or sets additional details.</summary>
    public Dictionary<string, object>? Details { get; init; }

    /// <summary>
    /// Create a new security event.
    /// </summary>
    public static SecurityEvent Create(
        SecurityEventType type,
        SecuritySeverity severity,
        string message,
        string? ipAddress = null,
        string? username = null,
        string? source = null,
        Dictionary<string, object>? details = null)
    {
        return new SecurityEvent
        {
            Type = type,
            Severity = severity,
            Message = message,
            IpAddress = ipAddress,
            Username = username,
            Source = source,
            Details = details,
        };
    }
}

/// <summary>
/// Types of security events.
/// </summary>
public enum SecurityEventType
{
    /// <summary>Connection event.</summary>
    Connection,

    /// <summary>Authentication event.</summary>
    Authentication,

    /// <summary>Rate limit event.</summary>
    RateLimit,

    /// <summary>Violation event.</summary>
    Violation,

    /// <summary>Ban event.</summary>
    Ban,

    /// <summary>Path traversal attempt.</summary>
    PathTraversal,

    /// <summary>Content safety event.</summary>
    ContentSafety,

    /// <summary>Reconnaissance detected.</summary>
    Reconnaissance,

    /// <summary>Honeypot triggered.</summary>
    Honeypot,

    /// <summary>Entropy issue.</summary>
    Entropy,

    /// <summary>Consensus failure.</summary>
    Consensus,

    /// <summary>Verification failure.</summary>
    Verification,

    /// <summary>Trust change.</summary>
    TrustChange,

    /// <summary>Canary sighting.</summary>
    CanarySighting,

    /// <summary>Server anomaly.</summary>
    ServerAnomaly,

    /// <summary>Other event.</summary>
    Other,
}

/// <summary>
/// Security event severity.
/// </summary>
public enum SecuritySeverity
{
    /// <summary>Informational.</summary>
    Info = 0,

    /// <summary>Low severity.</summary>
    Low = 1,

    /// <summary>Medium severity.</summary>
    Medium = 2,

    /// <summary>High severity.</summary>
    High = 3,

    /// <summary>Critical severity.</summary>
    Critical = 4,
}

/// <summary>
/// Event args for security events.
/// </summary>
public sealed class SecurityEventArgs : EventArgs
{
    /// <summary>Gets the event.</summary>
    public SecurityEvent Event { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityEventArgs"/> class.
    /// </summary>
    public SecurityEventArgs(SecurityEvent evt)
    {
        Event = evt;
    }
}

/// <summary>
/// Statistics about security events.
/// </summary>
public sealed class SecurityEventStats
{
    /// <summary>Gets total events.</summary>
    public int TotalEvents { get; init; }

    /// <summary>Gets events in last hour.</summary>
    public int EventsLastHour { get; init; }

    /// <summary>Gets critical events.</summary>
    public int CriticalEvents { get; init; }

    /// <summary>Gets high events.</summary>
    public int HighEvents { get; init; }

    /// <summary>Gets medium events.</summary>
    public int MediumEvents { get; init; }

    /// <summary>Gets low events.</summary>
    public int LowEvents { get; init; }

    /// <summary>Gets unique IPs.</summary>
    public int UniqueIps { get; init; }

    /// <summary>Gets unique users.</summary>
    public int UniqueUsers { get; init; }

    /// <summary>Gets event counts by type.</summary>
    public required Dictionary<string, long> EventCountsByType { get; init; }

    /// <summary>Gets last event time by type.</summary>
    public required Dictionary<string, DateTimeOffset> LastEventByType { get; init; }
}

