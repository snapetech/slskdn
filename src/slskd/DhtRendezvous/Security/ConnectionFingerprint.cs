// <copyright file="ConnectionFingerprint.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

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
    private readonly ConcurrentDictionary<string, ConnectionFingerprint> _recentFingerprints = new();
    private readonly ConcurrentQueue<ConnectionEvent> _eventLog = new();
    
    /// <summary>
    /// Maximum events to keep in memory.
    /// </summary>
    public const int MaxEventLogSize = 10000;
    
    /// <summary>
    /// Maximum fingerprints to track.
    /// </summary>
    public const int MaxFingerprints = 1000;
    
    public ConnectionFingerprintService(ILogger<ConnectionFingerprintService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Record a new connection attempt.
    /// </summary>
    public ConnectionFingerprint RecordConnection(
        IPAddress ip,
        int port,
        string? username,
        string? certificateThumbprint,
        IReadOnlyList<string>? features,
        string? clientVersion)
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
        
        // Store fingerprint
        if (_recentFingerprints.Count >= MaxFingerprints)
        {
            // Remove oldest
            var oldest = _recentFingerprints.Values
                .OrderBy(f => f.Timestamp)
                .FirstOrDefault();
            if (oldest != null)
            {
                _recentFingerprints.TryRemove(oldest.Id, out _);
            }
        }
        
        _recentFingerprints[fingerprint.Id] = fingerprint;
        
        // Log the connection event
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
            certificateThumbprint?[..Math.Min(16, certificateThumbprint.Length)] ?? "(none)",
            string.Join(",", fingerprint.Features.Take(5)));
        
        return fingerprint;
    }
    
    /// <summary>
    /// Record a disconnection.
    /// </summary>
    public void RecordDisconnection(string fingerprintId, string? reason)
    {
        if (_recentFingerprints.TryGetValue(fingerprintId, out var fingerprint))
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
        }
    }
    
    /// <summary>
    /// Record a security event for a connection.
    /// </summary>
    public void RecordSecurityEvent(
        string fingerprintId,
        string eventType,
        string details)
    {
        if (_recentFingerprints.TryGetValue(fingerprintId, out var fingerprint))
        {
            fingerprint.SecurityEvents.Add(new SecurityEvent
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
    /// Get fingerprint by ID.
    /// </summary>
    public ConnectionFingerprint? GetFingerprint(string id)
    {
        return _recentFingerprints.TryGetValue(id, out var fp) ? fp : null;
    }
    
    /// <summary>
    /// Find fingerprints matching criteria.
    /// </summary>
    public IReadOnlyList<ConnectionFingerprint> FindFingerprints(
        string? ipHash = null,
        string? username = null,
        string? certThumbprint = null,
        DateTimeOffset? since = null)
    {
        return _recentFingerprints.Values
            .Where(f =>
                (ipHash == null || f.IpHash == ipHash) &&
                (username == null || f.Username?.Equals(username, StringComparison.OrdinalIgnoreCase) == true) &&
                (certThumbprint == null || f.CertificateThumbprint == certThumbprint) &&
                (since == null || f.Timestamp >= since))
            .OrderByDescending(f => f.Timestamp)
            .ToList();
    }
    
    /// <summary>
    /// Get recent events.
    /// </summary>
    public IReadOnlyList<ConnectionEvent> GetRecentEvents(int count = 100)
    {
        return _eventLog
            .Reverse()
            .Take(count)
            .ToList();
    }
    
    /// <summary>
    /// Get statistics about connections.
    /// </summary>
    public FingerprintStats GetStats()
    {
        var fingerprints = _recentFingerprints.Values.ToList();
        var now = DateTimeOffset.UtcNow;
        var lastHour = now.AddHours(-1);
        
        return new FingerprintStats
        {
            TotalFingerprints = fingerprints.Count,
            ActiveConnections = fingerprints.Count(f => f.DisconnectedAt == null),
            ConnectionsLastHour = fingerprints.Count(f => f.Timestamp >= lastHour),
            UniqueIps = fingerprints.Select(f => f.IpHash).Distinct().Count(),
            UniqueUsernames = fingerprints.Where(f => f.Username != null).Select(f => f.Username).Distinct().Count(),
            TotalSecurityEvents = fingerprints.Sum(f => f.SecurityEvents.Count),
            EventLogSize = _eventLog.Count,
        };
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
}

/// <summary>
/// A connection fingerprint.
/// </summary>
public sealed class ConnectionFingerprint
{
    public required string Id { get; init; }
    public required string RemoteIp { get; init; }
    public required int RemotePort { get; init; }
    public string? Username { get; init; }
    public string? CertificateThumbprint { get; init; }
    public required List<string> Features { get; init; }
    public string? ClientVersion { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string IpHash { get; init; }
    
    public DateTimeOffset? DisconnectedAt { get; set; }
    public string? DisconnectReason { get; set; }
    
    public List<SecurityEvent> SecurityEvents { get; } = new();
    
    public TimeSpan? Duration => DisconnectedAt.HasValue
        ? DisconnectedAt.Value - Timestamp
        : DateTimeOffset.UtcNow - Timestamp;
}

/// <summary>
/// A security event associated with a connection.
/// </summary>
public sealed class SecurityEvent
{
    public required string Type { get; init; }
    public required string Details { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// A connection event for the audit log.
/// </summary>
public sealed class ConnectionEvent
{
    public required ConnectionEventType Type { get; init; }
    public required string FingerprintId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string IpHash { get; init; }
    public string? Username { get; init; }
    public string? Details { get; init; }
}

/// <summary>
/// Types of connection events.
/// </summary>
public enum ConnectionEventType
{
    Connected,
    Disconnected,
    SecurityEvent,
    MessageReceived,
    MessageSent,
}

/// <summary>
/// Fingerprint statistics.
/// </summary>
public sealed class FingerprintStats
{
    public int TotalFingerprints { get; init; }
    public int ActiveConnections { get; init; }
    public int ConnectionsLastHour { get; init; }
    public int UniqueIps { get; init; }
    public int UniqueUsernames { get; init; }
    public int TotalSecurityEvents { get; init; }
    public int EventLogSize { get; init; }
}

