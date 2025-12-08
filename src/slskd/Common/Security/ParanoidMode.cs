// <copyright file="ParanoidMode.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides paranoid mode features for validating server behavior.
/// SECURITY: Detects anomalous or suspicious server responses.
/// </summary>
public sealed class ParanoidMode
{
    private readonly ILogger<ParanoidMode> _logger;
    private readonly ConcurrentQueue<ServerAnomaly> _anomalies = new();
    private readonly ConcurrentDictionary<string, long> _anomalyCounts = new();

    /// <summary>
    /// Maximum anomalies to track in memory.
    /// </summary>
    public const int MaxAnomalies = 1000;

    /// <summary>
    /// Maximum search results to accept per query (prevent memory exhaustion).
    /// </summary>
    public const int MaxSearchResults = 10000;

    /// <summary>
    /// Maximum peers to accept for a single file.
    /// </summary>
    public const int MaxPeersPerFile = 500;

    /// <summary>
    /// Minimum valid port number.
    /// </summary>
    public const int MinPort = 1;

    /// <summary>
    /// Maximum valid port number.
    /// </summary>
    public const int MaxPort = 65535;

    /// <summary>
    /// Gets or sets the enforcement level.
    /// </summary>
    public ParanoidLevel Level { get; set; } = ParanoidLevel.Log;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParanoidMode"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ParanoidMode(ILogger<ParanoidMode> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate a peer endpoint from the server.
    /// </summary>
    /// <param name="ip">The IP address.</param>
    /// <param name="port">The port number.</param>
    /// <param name="context">Context for logging.</param>
    /// <returns>True if the endpoint appears valid.</returns>
    public bool ValidateEndpoint(IPAddress ip, int port, string context = "")
    {
        var issues = new List<string>();

        // Check for private/reserved IP ranges
        if (IsPrivateOrReserved(ip))
        {
            issues.Add($"Private/reserved IP: {ip}");
        }

        // Check port range
        if (port < MinPort || port > MaxPort)
        {
            issues.Add($"Invalid port: {port}");
        }

        // Check for suspicious well-known ports
        if (IsSuspiciousPort(port))
        {
            issues.Add($"Suspicious port: {port}");
        }

        if (issues.Count > 0)
        {
            RecordAnomaly(AnomalyType.SuspiciousEndpoint, $"{context}: {string.Join(", ", issues)}");
            return Level != ParanoidLevel.Enforce;
        }

        return true;
    }

    /// <summary>
    /// Validate search result count from server.
    /// </summary>
    /// <param name="count">Number of results.</param>
    /// <param name="query">The search query.</param>
    /// <returns>True if count is acceptable.</returns>
    public bool ValidateSearchResultCount(int count, string query)
    {
        if (count > MaxSearchResults)
        {
            RecordAnomaly(AnomalyType.ExcessiveResults, $"Search '{query}' returned {count} results (max: {MaxSearchResults})");
            return Level != ParanoidLevel.Enforce;
        }

        return true;
    }

    /// <summary>
    /// Validate peer count for a file.
    /// </summary>
    /// <param name="count">Number of peers.</param>
    /// <param name="filename">The filename.</param>
    /// <returns>True if count is acceptable.</returns>
    public bool ValidatePeerCount(int count, string filename)
    {
        if (count > MaxPeersPerFile)
        {
            RecordAnomaly(AnomalyType.ExcessivePeers, $"File '{filename}' has {count} peers (max: {MaxPeersPerFile})");
            return Level != ParanoidLevel.Enforce;
        }

        return true;
    }

    /// <summary>
    /// Validate a server message size.
    /// </summary>
    /// <param name="size">Message size in bytes.</param>
    /// <param name="messageType">Type of message.</param>
    /// <param name="maxExpected">Maximum expected size.</param>
    /// <returns>True if size is acceptable.</returns>
    public bool ValidateMessageSize(long size, string messageType, long maxExpected)
    {
        if (size > maxExpected)
        {
            RecordAnomaly(AnomalyType.OversizedMessage, $"{messageType}: {size} bytes (max expected: {maxExpected})");
            return Level != ParanoidLevel.Enforce;
        }

        return true;
    }

    /// <summary>
    /// Validate a username from the server.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>True if username appears valid.</returns>
    public bool ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            RecordAnomaly(AnomalyType.InvalidData, "Empty username received from server");
            return Level != ParanoidLevel.Enforce;
        }

        if (username.Length > 64)
        {
            RecordAnomaly(AnomalyType.InvalidData, $"Username too long: {username.Length} chars");
            return Level != ParanoidLevel.Enforce;
        }

        // Check for control characters
        if (username.Any(c => char.IsControl(c)))
        {
            RecordAnomaly(AnomalyType.InvalidData, "Username contains control characters");
            return Level != ParanoidLevel.Enforce;
        }

        return true;
    }

    /// <summary>
    /// Validate a file path from the server.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>True if path appears valid.</returns>
    public bool ValidateFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            RecordAnomaly(AnomalyType.InvalidData, "Empty file path received from server");
            return Level != ParanoidLevel.Enforce;
        }

        if (path.Length > 4096)
        {
            RecordAnomaly(AnomalyType.InvalidData, $"File path too long: {path.Length} chars");
            return Level != ParanoidLevel.Enforce;
        }

        // Check for null bytes
        if (path.Contains('\0'))
        {
            RecordAnomaly(AnomalyType.SuspiciousData, "File path contains null bytes");
            return Level != ParanoidLevel.Enforce;
        }

        return true;
    }

    /// <summary>
    /// Track server disconnect and check for patterns.
    /// </summary>
    /// <param name="reason">Disconnect reason if available.</param>
    public void TrackDisconnect(string? reason)
    {
        RecordAnomaly(AnomalyType.ServerDisconnect, reason ?? "Unknown reason");
    }

    /// <summary>
    /// Get recent anomalies.
    /// </summary>
    /// <param name="count">Maximum number to return.</param>
    /// <returns>Recent anomalies.</returns>
    public IReadOnlyList<ServerAnomaly> GetRecentAnomalies(int count = 100)
    {
        return _anomalies
            .Reverse()
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get anomaly counts by type.
    /// </summary>
    public IReadOnlyDictionary<string, long> GetAnomalyCounts()
    {
        return _anomalyCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Get statistics about detected anomalies.
    /// </summary>
    public ParanoidStats GetStats()
    {
        return new ParanoidStats
        {
            TotalAnomalies = _anomalies.Count,
            AnomaliesByType = _anomalyCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Level = Level,
        };
    }

    /// <summary>
    /// Clear all tracked anomalies.
    /// </summary>
    public void Clear()
    {
        while (_anomalies.TryDequeue(out _))
        {
            // Drain queue
        }

        _anomalyCounts.Clear();
    }

    private void RecordAnomaly(AnomalyType type, string details)
    {
        var anomaly = new ServerAnomaly
        {
            Type = type,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow,
        };

        _anomalies.Enqueue(anomaly);

        // Trim if too large
        while (_anomalies.Count > MaxAnomalies)
        {
            _anomalies.TryDequeue(out _);
        }

        // Increment counter
        var typeKey = type.ToString();
        _anomalyCounts.AddOrUpdate(typeKey, 1, (_, count) => count + 1);

        // Log based on level
        switch (Level)
        {
            case ParanoidLevel.Log:
            case ParanoidLevel.Enforce:
                _logger.LogWarning("Server anomaly [{Type}]: {Details}", type, details);
                break;
            case ParanoidLevel.Off:
            default:
                _logger.LogDebug("Server anomaly (ignored) [{Type}]: {Details}", type, details);
                break;
        }
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        if (bytes.Length == 4) // IPv4
        {
            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127)
            {
                return true;
            }

            // 0.0.0.0/8 (this network)
            if (bytes[0] == 0)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            // 224.0.0.0/4 (multicast)
            if (bytes[0] >= 224 && bytes[0] <= 239)
            {
                return true;
            }

            // 240.0.0.0/4 (reserved)
            if (bytes[0] >= 240)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSuspiciousPort(int port)
    {
        // Ports that shouldn't normally be used for Soulseek
        var suspicious = new HashSet<int>
        {
            20, 21, // FTP
            22, // SSH
            23, // Telnet
            25, // SMTP
            53, // DNS
            80, // HTTP
            110, // POP3
            143, // IMAP
            443, // HTTPS
            445, // SMB
            993, // IMAPS
            995, // POP3S
            3306, // MySQL
            3389, // RDP
            5432, // PostgreSQL
        };

        return suspicious.Contains(port);
    }
}

/// <summary>
/// Types of server anomalies that can be detected.
/// </summary>
public enum AnomalyType
{
    /// <summary>Endpoint with suspicious IP or port.</summary>
    SuspiciousEndpoint,

    /// <summary>Excessive number of search results.</summary>
    ExcessiveResults,

    /// <summary>Excessive number of peers for a file.</summary>
    ExcessivePeers,

    /// <summary>Oversized message from server.</summary>
    OversizedMessage,

    /// <summary>Invalid data in server response.</summary>
    InvalidData,

    /// <summary>Suspicious data patterns.</summary>
    SuspiciousData,

    /// <summary>Server disconnected unexpectedly.</summary>
    ServerDisconnect,

    /// <summary>Rate of messages is unusual.</summary>
    UnusualRate,

    /// <summary>Other anomaly.</summary>
    Other,
}

/// <summary>
/// Paranoid mode enforcement level.
/// </summary>
public enum ParanoidLevel
{
    /// <summary>Disabled - anomalies are not tracked.</summary>
    Off,

    /// <summary>Log only - anomalies are logged but not enforced.</summary>
    Log,

    /// <summary>Enforce - anomalous data is rejected.</summary>
    Enforce,
}

/// <summary>
/// A detected server anomaly.
/// </summary>
public sealed class ServerAnomaly
{
    /// <summary>Gets or sets the anomaly type.</summary>
    public required AnomalyType Type { get; init; }

    /// <summary>Gets or sets the anomaly details.</summary>
    public required string Details { get; init; }

    /// <summary>Gets or sets when the anomaly was detected.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Statistics about paranoid mode detections.
/// </summary>
public sealed class ParanoidStats
{
    /// <summary>Gets or sets total anomalies detected.</summary>
    public int TotalAnomalies { get; init; }

    /// <summary>Gets or sets anomaly counts by type.</summary>
    public required Dictionary<string, long> AnomaliesByType { get; init; }

    /// <summary>Gets or sets current enforcement level.</summary>
    public ParanoidLevel Level { get; init; }
}

