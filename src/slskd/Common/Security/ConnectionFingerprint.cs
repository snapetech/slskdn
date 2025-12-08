// <copyright file="ConnectionFingerprint.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Captures and logs connection fingerprints for forensic analysis.
/// SECURITY: Helps identify patterns of malicious behavior across sessions.
/// </summary>
public sealed class ConnectionFingerprintService
{
    private readonly ILogger<ConnectionFingerprintService> _logger;
    private readonly ConcurrentDictionary<string, ConnectionFingerprint> _fingerprints = new();
    private readonly ConcurrentQueue<ConnectionEvent> _eventLog = new();

    /// <summary>
    /// Maximum fingerprints to keep in memory.
    /// </summary>
    public const int MaxFingerprints = 1000;

    /// <summary>
    /// Maximum events to keep in the audit log.
    /// </summary>
    public const int MaxEventLogSize = 10000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionFingerprintService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ConnectionFingerprintService(ILogger<ConnectionFingerprintService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Record a new connection attempt.
    /// </summary>
    /// <param name="ip">Remote IP address.</param>
    /// <param name="port">Remote port.</param>
    /// <param name="username">Username if known.</param>
    /// <param name="certificateThumbprint">TLS certificate thumbprint if available.</param>
    /// <param name="features">Features advertised by the client.</param>
    /// <param name="clientVersion">Client version string.</param>
    /// <returns>The recorded fingerprint.</returns>
    public ConnectionFingerprint RecordConnection(
        IPAddress ip,
        int port,
        string? username = null,
        string? certificateThumbprint = null,
        IReadOnlyList<string>? features = null,
        string? clientVersion = null)
    {
        var fingerprint = new ConnectionFingerprint
        {
            Id = GenerateFingerprintId(),
            RemoteIp = ip.ToString(),
            RemotePort = port,
            Username = username,
            CertificateThumbprint = certificateThumbprint,
            Features = features?.ToList() ?? new List<string>(),
            ClientVersion = clientVersion,
            Timestamp = DateTimeOffset.UtcNow,
            IpHash = HashIp(ip),
        };

        // Enforce max size
        if (_fingerprints.Count >= MaxFingerprints)
        {
            var oldest = _fingerprints.Values
                .OrderBy(f => f.Timestamp)
                .FirstOrDefault();
            if (oldest != null)
            {
                _fingerprints.TryRemove(oldest.Id, out _);
            }
        }

        _fingerprints[fingerprint.Id] = fingerprint;

        // Log the event
        RecordEvent(new ConnectionEvent
        {
            Type = ConnectionEventType.Connected,
            FingerprintId = fingerprint.Id,
            Timestamp = fingerprint.Timestamp,
            IpHash = fingerprint.IpHash,
            Username = username,
        });

        _logger.LogInformation(
            "Connection fingerprint {Id}: IP={IpHash}, User={Username}, Cert={Cert}, Features=[{Features}]",
            fingerprint.Id,
            fingerprint.IpHash,
            username ?? "(none)",
            TruncateThumbprint(certificateThumbprint),
            string.Join(",", fingerprint.Features.Take(5)));

        return fingerprint;
    }

    /// <summary>
    /// Record a disconnection.
    /// </summary>
    /// <param name="fingerprintId">The fingerprint ID of the connection.</param>
    /// <param name="reason">Reason for disconnection.</param>
    public void RecordDisconnection(string fingerprintId, string? reason = null)
    {
        if (_fingerprints.TryGetValue(fingerprintId, out var fingerprint))
        {
            fingerprint.DisconnectedAt = DateTimeOffset.UtcNow;
            fingerprint.DisconnectReason = reason;

            RecordEvent(new ConnectionEvent
            {
                Type = ConnectionEventType.Disconnected,
                FingerprintId = fingerprintId,
                Timestamp = DateTimeOffset.UtcNow,
                IpHash = fingerprint.IpHash,
                Username = fingerprint.Username,
                Details = reason,
            });

            _logger.LogDebug(
                "Disconnected {Id} ({Username}): {Reason}",
                fingerprintId,
                fingerprint.Username ?? "(unknown)",
                reason ?? "(no reason)");
        }
    }

    /// <summary>
    /// Record a security event associated with a connection.
    /// </summary>
    /// <param name="fingerprintId">The fingerprint ID.</param>
    /// <param name="eventType">Type of security event.</param>
    /// <param name="details">Event details.</param>
    public void RecordSecurityEvent(string fingerprintId, string eventType, string details)
    {
        if (_fingerprints.TryGetValue(fingerprintId, out var fingerprint))
        {
            fingerprint.SecurityEvents.Add(new ConnectionSecurityEvent
            {
                Type = eventType,
                Details = details,
                Timestamp = DateTimeOffset.UtcNow,
            });

            RecordEvent(new ConnectionEvent
            {
                Type = ConnectionEventType.SecurityEvent,
                FingerprintId = fingerprintId,
                Timestamp = DateTimeOffset.UtcNow,
                IpHash = fingerprint.IpHash,
                Username = fingerprint.Username,
                Details = $"{eventType}: {details}",
            });

            _logger.LogWarning(
                "Security event for {Id} ({Username}): {Type} - {Details}",
                fingerprintId,
                fingerprint.Username ?? "(unknown)",
                eventType,
                details);
        }
    }

    /// <summary>
    /// Update the username for a connection after authentication.
    /// </summary>
    /// <param name="fingerprintId">The fingerprint ID.</param>
    /// <param name="username">The authenticated username.</param>
    public void SetUsername(string fingerprintId, string username)
    {
        if (_fingerprints.TryGetValue(fingerprintId, out var fingerprint))
        {
            fingerprint.Username = username;

            RecordEvent(new ConnectionEvent
            {
                Type = ConnectionEventType.Authenticated,
                FingerprintId = fingerprintId,
                Timestamp = DateTimeOffset.UtcNow,
                IpHash = fingerprint.IpHash,
                Username = username,
            });
        }
    }

    /// <summary>
    /// Get a fingerprint by ID.
    /// </summary>
    public ConnectionFingerprint? GetFingerprint(string id)
    {
        return _fingerprints.TryGetValue(id, out var fp) ? fp : null;
    }

    /// <summary>
    /// Find fingerprints matching criteria.
    /// </summary>
    /// <param name="ipHash">IP hash to match.</param>
    /// <param name="username">Username to match.</param>
    /// <param name="certThumbprint">Certificate thumbprint to match.</param>
    /// <param name="since">Only include fingerprints since this time.</param>
    /// <returns>Matching fingerprints.</returns>
    public IReadOnlyList<ConnectionFingerprint> FindFingerprints(
        string? ipHash = null,
        string? username = null,
        string? certThumbprint = null,
        DateTimeOffset? since = null)
    {
        return _fingerprints.Values
            .Where(f =>
                (ipHash == null || f.IpHash == ipHash) &&
                (username == null || f.Username?.Equals(username, StringComparison.OrdinalIgnoreCase) == true) &&
                (certThumbprint == null || f.CertificateThumbprint == certThumbprint) &&
                (since == null || f.Timestamp >= since))
            .OrderByDescending(f => f.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Get recent audit events.
    /// </summary>
    /// <param name="count">Maximum number of events to return.</param>
    /// <returns>Recent events in reverse chronological order.</returns>
    public IReadOnlyList<ConnectionEvent> GetRecentEvents(int count = 100)
    {
        return _eventLog
            .Reverse()
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get events for a specific fingerprint.
    /// </summary>
    /// <param name="fingerprintId">The fingerprint ID.</param>
    /// <returns>Events associated with the fingerprint.</returns>
    public IReadOnlyList<ConnectionEvent> GetEventsForFingerprint(string fingerprintId)
    {
        return _eventLog
            .Where(e => e.FingerprintId == fingerprintId)
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Get statistics about tracked connections.
    /// </summary>
    public FingerprintStats GetStats()
    {
        var fingerprints = _fingerprints.Values.ToList();
        var now = DateTimeOffset.UtcNow;
        var lastHour = now.AddHours(-1);

        return new FingerprintStats
        {
            TotalFingerprints = fingerprints.Count,
            ActiveConnections = fingerprints.Count(f => f.DisconnectedAt == null),
            ConnectionsLastHour = fingerprints.Count(f => f.Timestamp >= lastHour),
            UniqueIpHashes = fingerprints.Select(f => f.IpHash).Distinct().Count(),
            UniqueUsernames = fingerprints.Where(f => f.Username != null).Select(f => f.Username).Distinct().Count(),
            TotalSecurityEvents = fingerprints.Sum(f => f.SecurityEvents.Count),
            EventLogSize = _eventLog.Count,
        };
    }

    /// <summary>
    /// Clear all fingerprints and events (for testing).
    /// </summary>
    public void Clear()
    {
        _fingerprints.Clear();
        while (_eventLog.TryDequeue(out _))
        {
            // Drain the queue
        }
    }

    private void RecordEvent(ConnectionEvent evt)
    {
        _eventLog.Enqueue(evt);

        // Trim if too large
        while (_eventLog.Count > MaxEventLogSize)
        {
            _eventLog.TryDequeue(out _);
        }
    }

    private static string GenerateFingerprintId()
    {
        return Guid.NewGuid().ToString("N")[..12];
    }

    /// <summary>
    /// Hash an IP address for privacy-preserving logging.
    /// </summary>
    private static string HashIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string TruncateThumbprint(string? thumbprint)
    {
        if (string.IsNullOrEmpty(thumbprint))
        {
            return "(none)";
        }

        return thumbprint.Length > 16 ? thumbprint[..16] + "..." : thumbprint;
    }
}

/// <summary>
/// A connection fingerprint.
/// </summary>
public sealed class ConnectionFingerprint
{
    /// <summary>Gets or sets the unique fingerprint ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets or sets the remote IP address (full).</summary>
    public required string RemoteIp { get; init; }

    /// <summary>Gets or sets the remote port.</summary>
    public required int RemotePort { get; init; }

    /// <summary>Gets or sets the username (may be set after auth).</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the TLS certificate thumbprint.</summary>
    public string? CertificateThumbprint { get; init; }

    /// <summary>Gets or sets features advertised by the client.</summary>
    public required List<string> Features { get; init; }

    /// <summary>Gets or sets the client version string.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>Gets or sets when the connection was established.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets or sets the hashed IP for privacy-preserving logging.</summary>
    public required string IpHash { get; init; }

    /// <summary>Gets or sets when the connection was closed.</summary>
    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>Gets or sets the reason for disconnection.</summary>
    public string? DisconnectReason { get; set; }

    /// <summary>Gets security events associated with this connection.</summary>
    public List<ConnectionSecurityEvent> SecurityEvents { get; } = new();

    /// <summary>Gets the connection duration.</summary>
    public TimeSpan? Duration => DisconnectedAt.HasValue
        ? DisconnectedAt.Value - Timestamp
        : DateTimeOffset.UtcNow - Timestamp;
}

/// <summary>
/// A security event associated with a connection.
/// </summary>
public sealed class ConnectionSecurityEvent
{
    /// <summary>Gets or sets the event type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets the event details.</summary>
    public required string Details { get; init; }

    /// <summary>Gets or sets when the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// An event in the connection audit log.
/// </summary>
public sealed class ConnectionEvent
{
    /// <summary>Gets or sets the event type.</summary>
    public required ConnectionEventType Type { get; init; }

    /// <summary>Gets or sets the fingerprint ID.</summary>
    public required string FingerprintId { get; init; }

    /// <summary>Gets or sets when the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets or sets the hashed IP.</summary>
    public required string IpHash { get; init; }

    /// <summary>Gets or sets the username if known.</summary>
    public string? Username { get; init; }

    /// <summary>Gets or sets additional details.</summary>
    public string? Details { get; init; }
}

/// <summary>
/// Types of connection events.
/// </summary>
public enum ConnectionEventType
{
    /// <summary>Connection established.</summary>
    Connected,

    /// <summary>Connection closed.</summary>
    Disconnected,

    /// <summary>User authenticated.</summary>
    Authenticated,

    /// <summary>Security event occurred.</summary>
    SecurityEvent,

    /// <summary>Message received.</summary>
    MessageReceived,

    /// <summary>Message sent.</summary>
    MessageSent,
}

/// <summary>
/// Statistics about tracked connections.
/// </summary>
public sealed class FingerprintStats
{
    /// <summary>Gets or sets total fingerprints tracked.</summary>
    public int TotalFingerprints { get; init; }

    /// <summary>Gets or sets currently active connections.</summary>
    public int ActiveConnections { get; init; }

    /// <summary>Gets or sets connections in the last hour.</summary>
    public int ConnectionsLastHour { get; init; }

    /// <summary>Gets or sets unique IP hashes seen.</summary>
    public int UniqueIpHashes { get; init; }

    /// <summary>Gets or sets unique usernames seen.</summary>
    public int UniqueUsernames { get; init; }

    /// <summary>Gets or sets total security events recorded.</summary>
    public int TotalSecurityEvents { get; init; }

    /// <summary>Gets or sets current size of the event log.</summary>
    public int EventLogSize { get; init; }
}

