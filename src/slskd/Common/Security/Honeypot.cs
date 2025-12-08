// <copyright file="Honeypot.cs" company="slskdN">
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
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements honeypot decoy services to detect and trap malicious actors.
/// SECURITY: Gathers intelligence on attack patterns and identifies threats.
/// </summary>
public sealed class Honeypot : IDisposable
{
    private readonly ILogger<Honeypot> _logger;
    private readonly ConcurrentDictionary<string, HoneypotInteraction> _interactions = new();
    private readonly ConcurrentDictionary<IPAddress, ThreatProfile> _threatProfiles = new();
    private readonly ConcurrentQueue<HoneypotEvent> _events = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Maximum events to track.
    /// </summary>
    public const int MaxEvents = 10000;

    /// <summary>
    /// Maximum threat profiles to track.
    /// </summary>
    public const int MaxProfiles = 1000;

    /// <summary>
    /// Fake files that honeypots advertise.
    /// </summary>
    public static readonly HoneypotFile[] DecoyFiles = new[]
    {
        new HoneypotFile("slskd_config_backup.zip", DecoyType.ConfigFile, "CONFIG"),
        new HoneypotFile("admin_credentials.txt", DecoyType.CredentialFile, "CREDS"),
        new HoneypotFile("database_dump.sql", DecoyType.DatabaseDump, "DB"),
        new HoneypotFile("private_keys.pem", DecoyType.PrivateKey, "KEYS"),
        new HoneypotFile("user_data_export.json", DecoyType.UserData, "DATA"),
    };

    /// <summary>
    /// Event raised when honeypot is triggered.
    /// </summary>
    public event EventHandler<HoneypotEventArgs>? HoneypotTriggered;

    /// <summary>
    /// Initializes a new instance of the <see cref="Honeypot"/> class.
    /// </summary>
    public Honeypot(ILogger<Honeypot> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupOld, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Check if a requested file is a honeypot.
    /// </summary>
    /// <param name="filename">The requested filename.</param>
    /// <returns>True if this is a honeypot file.</returns>
    public bool IsHoneypotFile(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }

        var lowerName = filename.ToLowerInvariant();
        return DecoyFiles.Any(d => lowerName.Contains(d.Filename.ToLowerInvariant()));
    }

    /// <summary>
    /// Record a honeypot interaction.
    /// </summary>
    /// <param name="ip">Remote IP address.</param>
    /// <param name="username">Username if known.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="filename">File being accessed.</param>
    /// <param name="details">Additional details.</param>
    /// <returns>The recorded interaction.</returns>
    public HoneypotInteraction RecordInteraction(
        IPAddress ip,
        string? username,
        HoneypotAction action,
        string filename,
        string? details = null)
    {
        var decoyFile = DecoyFiles.FirstOrDefault(d =>
            filename.ToLowerInvariant().Contains(d.Filename.ToLowerInvariant()));

        var interactionId = Guid.NewGuid().ToString("N")[..16];

        var interaction = new HoneypotInteraction
        {
            Id = interactionId,
            IpAddress = ip.ToString(),
            Username = username,
            Action = action,
            Filename = filename,
            DecoyType = decoyFile?.Type ?? DecoyType.Unknown,
            Timestamp = DateTimeOffset.UtcNow,
            Details = details,
        };

        _interactions[interactionId] = interaction;

        // Update threat profile
        UpdateThreatProfile(ip, interaction);

        // Record event
        var evt = new HoneypotEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Interaction = interaction,
            ThreatLevel = CalculateThreatLevel(ip),
        };

        _events.Enqueue(evt);
        while (_events.Count > MaxEvents)
        {
            _events.TryDequeue(out _);
        }

        // Log and alert
        _logger.LogWarning(
            "HONEYPOT TRIGGERED: {Action} on {File} from {Ip} ({Username})",
            action, filename, ip, username ?? "(unknown)");

        HoneypotTriggered?.Invoke(this, new HoneypotEventArgs(evt));

        return interaction;
    }

    /// <summary>
    /// Record a port probe on a honeypot port.
    /// </summary>
    public HoneypotInteraction RecordPortProbe(IPAddress ip, int port)
    {
        return RecordInteraction(
            ip,
            null,
            HoneypotAction.PortProbe,
            $"port:{port}",
            $"Connection attempt to honeypot port {port}");
    }

    /// <summary>
    /// Get threat profile for an IP.
    /// </summary>
    public ThreatProfile? GetThreatProfile(IPAddress ip)
    {
        return _threatProfiles.TryGetValue(ip, out var p) ? p : null;
    }

    /// <summary>
    /// Check if an IP is a known threat.
    /// </summary>
    public bool IsKnownThreat(IPAddress ip)
    {
        if (!_threatProfiles.TryGetValue(ip, out var profile))
        {
            return false;
        }

        return profile.ThreatLevel >= ThreatLevel.High;
    }

    /// <summary>
    /// Get all known threats above a certain level.
    /// </summary>
    public IReadOnlyList<ThreatProfile> GetThreats(ThreatLevel minLevel = ThreatLevel.Medium)
    {
        return _threatProfiles.Values
            .Where(p => p.ThreatLevel >= minLevel)
            .OrderByDescending(p => p.ThreatLevel)
            .ThenByDescending(p => p.InteractionCount)
            .ToList();
    }

    /// <summary>
    /// Get recent events.
    /// </summary>
    public IReadOnlyList<HoneypotEvent> GetRecentEvents(int count = 100)
    {
        return _events.Reverse().Take(count).ToList();
    }

    /// <summary>
    /// Get statistics.
    /// </summary>
    public HoneypotStats GetStats()
    {
        var profiles = _threatProfiles.Values.ToList();
        return new HoneypotStats
        {
            TotalInteractions = _interactions.Count,
            TotalThreats = profiles.Count(p => p.ThreatLevel >= ThreatLevel.Medium),
            HighThreats = profiles.Count(p => p.ThreatLevel >= ThreatLevel.High),
            CriticalThreats = profiles.Count(p => p.ThreatLevel == ThreatLevel.Critical),
            EventCount = _events.Count,
            InteractionsByType = _interactions.Values
                .GroupBy(i => i.DecoyType)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
        };
    }

    /// <summary>
    /// Generate a unique honeypot file path for a session.
    /// </summary>
    /// <param name="decoyType">Type of decoy.</param>
    /// <returns>Unique path that looks legitimate.</returns>
    public static string GenerateHoneypotPath(DecoyType decoyType)
    {
        var random = new Random();
        var datePart = DateTime.Now.AddDays(-random.Next(1, 365)).ToString("yyyy-MM-dd");

        return decoyType switch
        {
            DecoyType.ConfigFile => $"backup/{datePart}/slskd_config_backup.zip",
            DecoyType.CredentialFile => $"config/admin_credentials.txt",
            DecoyType.DatabaseDump => $"backup/{datePart}/database_dump.sql",
            DecoyType.PrivateKey => $".ssh/private_keys.pem",
            DecoyType.UserData => $"export/user_data_export.json",
            _ => $"temp/{Guid.NewGuid():N}.tmp",
        };
    }

    private void UpdateThreatProfile(IPAddress ip, HoneypotInteraction interaction)
    {
        var profile = _threatProfiles.GetOrAdd(ip, _ => new ThreatProfile
        {
            IpAddress = ip.ToString(),
            FirstSeen = DateTimeOffset.UtcNow,
        });

        lock (profile)
        {
            profile.InteractionCount++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            profile.Interactions.Add(interaction.Id);
            profile.DecoyTypesAccessed.Add(interaction.DecoyType);

            if (!string.IsNullOrEmpty(interaction.Username))
            {
                profile.UsernamesSeen.Add(interaction.Username);
            }

            // Update threat level
            profile.ThreatLevel = CalculateThreatLevel(profile);
        }
    }

    private ThreatLevel CalculateThreatLevel(IPAddress ip)
    {
        if (!_threatProfiles.TryGetValue(ip, out var profile))
        {
            return ThreatLevel.Low;
        }

        return CalculateThreatLevel(profile);
    }

    private static ThreatLevel CalculateThreatLevel(ThreatProfile profile)
    {
        // Multiple decoy types accessed = high threat
        if (profile.DecoyTypesAccessed.Count >= 3)
        {
            return ThreatLevel.Critical;
        }

        // Credential or key file accessed = high threat
        if (profile.DecoyTypesAccessed.Contains(DecoyType.CredentialFile) ||
            profile.DecoyTypesAccessed.Contains(DecoyType.PrivateKey))
        {
            return ThreatLevel.High;
        }

        // Multiple interactions = medium threat
        if (profile.InteractionCount >= 5)
        {
            return ThreatLevel.High;
        }

        if (profile.InteractionCount >= 2)
        {
            return ThreatLevel.Medium;
        }

        return ThreatLevel.Low;
    }

    private void CleanupOld(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);

        // Remove old interactions (but keep threat profiles longer)
        var toRemove = _interactions
            .Where(kvp => kvp.Value.Timestamp < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _interactions.TryRemove(id, out _);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// A honeypot decoy file definition.
/// </summary>
public sealed class HoneypotFile
{
    /// <summary>Gets the filename.</summary>
    public string Filename { get; }

    /// <summary>Gets the decoy type.</summary>
    public DecoyType Type { get; }

    /// <summary>Gets the tag for tracking.</summary>
    public string Tag { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HoneypotFile"/> class.
    /// </summary>
    public HoneypotFile(string filename, DecoyType type, string tag)
    {
        Filename = filename;
        Type = type;
        Tag = tag;
    }
}

/// <summary>
/// Types of decoy files.
/// </summary>
public enum DecoyType
{
    /// <summary>Unknown type.</summary>
    Unknown,

    /// <summary>Configuration file.</summary>
    ConfigFile,

    /// <summary>Credential file.</summary>
    CredentialFile,

    /// <summary>Database dump.</summary>
    DatabaseDump,

    /// <summary>Private key file.</summary>
    PrivateKey,

    /// <summary>User data export.</summary>
    UserData,

    /// <summary>Port probe.</summary>
    PortProbe,
}

/// <summary>
/// Actions that can trigger honeypots.
/// </summary>
public enum HoneypotAction
{
    /// <summary>File was searched for.</summary>
    Search,

    /// <summary>File listing was requested.</summary>
    Browse,

    /// <summary>File was requested for download.</summary>
    Download,

    /// <summary>Port was probed.</summary>
    PortProbe,
}

/// <summary>
/// A recorded honeypot interaction.
/// </summary>
public sealed class HoneypotInteraction
{
    /// <summary>Gets the interaction ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the IP address.</summary>
    public required string IpAddress { get; init; }

    /// <summary>Gets the username if known.</summary>
    public string? Username { get; init; }

    /// <summary>Gets the action taken.</summary>
    public required HoneypotAction Action { get; init; }

    /// <summary>Gets the filename accessed.</summary>
    public required string Filename { get; init; }

    /// <summary>Gets the decoy type.</summary>
    public required DecoyType DecoyType { get; init; }

    /// <summary>Gets when occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets additional details.</summary>
    public string? Details { get; init; }
}

/// <summary>
/// Threat level classification.
/// </summary>
public enum ThreatLevel
{
    /// <summary>Low threat.</summary>
    Low,

    /// <summary>Medium threat.</summary>
    Medium,

    /// <summary>High threat.</summary>
    High,

    /// <summary>Critical threat.</summary>
    Critical,
}

/// <summary>
/// Threat profile for an IP.
/// </summary>
public sealed class ThreatProfile
{
    /// <summary>Gets the IP address.</summary>
    public required string IpAddress { get; init; }

    /// <summary>Gets when first seen.</summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Gets or sets when last seen.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets interaction count.</summary>
    public int InteractionCount { get; set; }

    /// <summary>Gets or sets threat level.</summary>
    public ThreatLevel ThreatLevel { get; set; }

    /// <summary>Gets interaction IDs.</summary>
    public HashSet<string> Interactions { get; } = new();

    /// <summary>Gets decoy types accessed.</summary>
    public HashSet<DecoyType> DecoyTypesAccessed { get; } = new();

    /// <summary>Gets usernames seen.</summary>
    public HashSet<string> UsernamesSeen { get; } = new();
}

/// <summary>
/// A honeypot event.
/// </summary>
public sealed class HoneypotEvent
{
    /// <summary>Gets when occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the interaction.</summary>
    public required HoneypotInteraction Interaction { get; init; }

    /// <summary>Gets the threat level at time of event.</summary>
    public required ThreatLevel ThreatLevel { get; init; }
}

/// <summary>
/// Event args for honeypot triggers.
/// </summary>
public sealed class HoneypotEventArgs : EventArgs
{
    /// <summary>Gets the event.</summary>
    public HoneypotEvent Event { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HoneypotEventArgs"/> class.
    /// </summary>
    public HoneypotEventArgs(HoneypotEvent evt)
    {
        Event = evt;
    }
}

/// <summary>
/// Honeypot statistics.
/// </summary>
public sealed class HoneypotStats
{
    /// <summary>Gets total interactions.</summary>
    public int TotalInteractions { get; init; }

    /// <summary>Gets total threats.</summary>
    public int TotalThreats { get; init; }

    /// <summary>Gets high threats.</summary>
    public int HighThreats { get; init; }

    /// <summary>Gets critical threats.</summary>
    public int CriticalThreats { get; init; }

    /// <summary>Gets event count.</summary>
    public int EventCount { get; init; }

    /// <summary>Gets interactions by type.</summary>
    public required Dictionary<string, int> InteractionsByType { get; init; }
}

