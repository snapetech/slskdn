// <copyright file="SecurityOptions.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration options for security features.
/// Can be bound to appsettings.json "Security" section.
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string Section = "Security";

    /// <summary>
    /// Gets or sets whether security features are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the security profile to use.
    /// </summary>
    public SecurityProfile Profile { get; set; } = SecurityProfile.Standard;

    /// <summary>
    /// Gets or sets network guard options.
    /// </summary>
    public NetworkGuardOptions NetworkGuard { get; set; } = new();

    /// <summary>
    /// Gets or sets path guard options.
    /// </summary>
    public PathGuardOptions PathGuard { get; set; } = new();

    /// <summary>
    /// Gets or sets content safety options.
    /// </summary>
    public ContentSafetyOptions ContentSafety { get; set; } = new();

    /// <summary>
    /// Gets or sets peer reputation options.
    /// </summary>
    public PeerReputationOptions PeerReputation { get; set; } = new();

    /// <summary>
    /// Gets or sets violation tracker options.
    /// </summary>
    public ViolationTrackerOptions ViolationTracker { get; set; } = new();

    /// <summary>
    /// Gets or sets paranoid mode options.
    /// </summary>
    public ParanoidModeOptions ParanoidMode { get; set; } = new();

    /// <summary>
    /// Gets or sets privacy mode options.
    /// </summary>
    public PrivacyModeOptions PrivacyMode { get; set; } = new();

    /// <summary>
    /// Gets or sets honeypot options.
    /// </summary>
    public HoneypotOptions Honeypot { get; set; } = new();

    /// <summary>
    /// Gets or sets event logging options.
    /// </summary>
    public SecurityEventOptions Events { get; set; } = new();
}

/// <summary>
/// Security profile presets.
/// </summary>
public enum SecurityProfile
{
    /// <summary>
    /// Minimal security - only critical protections.
    /// </summary>
    Minimal,

    /// <summary>
    /// Standard security - balanced protection.
    /// </summary>
    Standard,

    /// <summary>
    /// Maximum security - all features enabled.
    /// </summary>
    Maximum,

    /// <summary>
    /// Custom - use individual settings.
    /// </summary>
    Custom,
}

/// <summary>
/// Network guard configuration.
/// </summary>
public sealed class NetworkGuardOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets max connections per IP.
    /// </summary>
    [Range(1, 100)]
    public int MaxConnectionsPerIp { get; set; } = 3;

    /// <summary>
    /// Gets or sets max global connections.
    /// </summary>
    [Range(1, 10000)]
    public int MaxGlobalConnections { get; set; } = 100;

    /// <summary>
    /// Gets or sets max messages per IP per minute.
    /// </summary>
    [Range(1, 1000)]
    public int MaxMessagesPerMinute { get; set; } = 60;

    /// <summary>
    /// Gets or sets max message size in bytes.
    /// </summary>
    [Range(1024, 10 * 1024 * 1024)]
    public int MaxMessageSize { get; set; } = 65536;

    /// <summary>
    /// Gets or sets max pending requests per IP.
    /// </summary>
    [Range(1, 100)]
    public int MaxPendingRequestsPerIp { get; set; } = 10;
}

/// <summary>
/// Path guard configuration.
/// </summary>
public sealed class PathGuardOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the download root directory.
    /// </summary>
    public string DownloadRoot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the share root directory.
    /// </summary>
    public string ShareRoot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets max path length.
    /// </summary>
    [Range(1, 4096)]
    public int MaxPathLength { get; set; } = 512;

    /// <summary>
    /// Gets or sets max path depth (segments).
    /// </summary>
    [Range(1, 100)]
    public int MaxPathDepth { get; set; } = 20;

    /// <summary>
    /// Gets or sets blocked extensions.
    /// </summary>
    public List<string> BlockedExtensions { get; set; } = new()
    {
        ".exe", ".bat", ".cmd", ".com", ".pif", ".scr",
        ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf",
        ".msi", ".msp", ".hta", ".cpl", ".jar", ".app",
    };
}

/// <summary>
/// Content safety configuration.
/// </summary>
public sealed class ContentSafetyOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to verify magic bytes.
    /// </summary>
    public bool VerifyMagicBytes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to quarantine suspicious files.
    /// </summary>
    public bool QuarantineSuspicious { get; set; } = true;

    /// <summary>
    /// Gets or sets the quarantine directory.
    /// </summary>
    public string QuarantineDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to block executables.
    /// </summary>
    public bool BlockExecutables { get; set; } = true;
}

/// <summary>
/// Peer reputation configuration.
/// </summary>
public sealed class PeerReputationOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the trusted threshold.
    /// </summary>
    [Range(0, 100)]
    public int TrustedThreshold { get; set; } = 70;

    /// <summary>
    /// Gets or sets the untrusted threshold.
    /// </summary>
    [Range(0, 100)]
    public int UntrustedThreshold { get; set; } = 20;

    /// <summary>
    /// Gets or sets whether to persist reputation.
    /// </summary>
    public bool PersistReputation { get; set; } = true;
}

/// <summary>
/// Violation tracker configuration.
/// </summary>
public sealed class ViolationTrackerOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the violation window in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int ViolationWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets violations before auto-ban.
    /// </summary>
    [Range(1, 100)]
    public int ViolationsBeforeAutoBan { get; set; } = 5;

    /// <summary>
    /// Gets or sets auto-bans before permanent.
    /// </summary>
    [Range(1, 10)]
    public int AutoBansBeforePermanent { get; set; } = 3;

    /// <summary>
    /// Gets or sets base ban duration in minutes.
    /// </summary>
    [Range(1, 10080)]
    public int BaseBanDurationMinutes { get; set; } = 60;
}

/// <summary>
/// Paranoid mode configuration.
/// </summary>
public sealed class ParanoidModeOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to log all server messages.
    /// </summary>
    public bool LogServerMessages { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to validate server responses.
    /// </summary>
    public bool ValidateServerResponses { get; set; } = true;

    /// <summary>
    /// Gets or sets max search results to accept.
    /// </summary>
    [Range(1, 10000)]
    public int MaxSearchResults { get; set; } = 1000;

    /// <summary>
    /// Gets or sets blocked IP ranges.
    /// </summary>
    public List<string> BlockedIpRanges { get; set; } = new();
}

/// <summary>
/// Privacy mode configuration.
/// </summary>
public sealed class PrivacyModeOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to minimize metadata.
    /// </summary>
    public bool MinimizeMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use generic client ID.
    /// </summary>
    public bool UseGenericClientId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to avoid public rooms.
    /// </summary>
    public bool AvoidPublicRooms { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to strip share paths.
    /// </summary>
    public bool StripSharePaths { get; set; } = false;
}

/// <summary>
/// Honeypot configuration.
/// </summary>
public sealed class HoneypotOptions
{
    /// <summary>
    /// Gets or sets whether enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets honeypot file paths.
    /// </summary>
    public List<string> HoneypotFiles { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to log interactions.
    /// </summary>
    public bool LogInteractions { get; set; } = true;
}

/// <summary>
/// Security event configuration.
/// </summary>
public sealed class SecurityEventOptions
{
    /// <summary>
    /// Gets or sets max events to keep in memory.
    /// </summary>
    [Range(100, 100000)]
    public int MaxEventsInMemory { get; set; } = 10000;

    /// <summary>
    /// Gets or sets min severity to log.
    /// </summary>
    public SecuritySeverity MinLogSeverity { get; set; } = SecuritySeverity.Medium;

    /// <summary>
    /// Gets or sets whether to persist events.
    /// </summary>
    public bool PersistEvents { get; set; } = false;
}

