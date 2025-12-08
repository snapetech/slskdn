// <copyright file="ViolationTracker.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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
/// Tracks security violations and implements auto-escalating bans.
/// SECURITY: Provides graduated response to malicious behavior.
/// </summary>
public sealed class ViolationTracker : IDisposable
{
    private readonly ILogger<ViolationTracker> _logger;
    private readonly ConcurrentDictionary<string, ViolationRecord> _ipViolations = new();
    private readonly ConcurrentDictionary<string, ViolationRecord> _usernameViolations = new();
    private readonly ConcurrentDictionary<string, BanRecord> _bans = new();
    private readonly System.Threading.Timer _cleanupTimer;

    /// <summary>
    /// Time window for counting recent violations (default: 1 hour).
    /// </summary>
    public TimeSpan ViolationWindow { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Number of violations within window before auto-ban (default: 5).
    /// </summary>
    public int ViolationsBeforeAutoBan { get; init; } = 5;

    /// <summary>
    /// Number of auto-bans before permanent ban (default: 3).
    /// </summary>
    public int AutoBansBeforePermanent { get; init; } = 3;

    /// <summary>
    /// Base duration for auto-bans (escalates exponentially).
    /// </summary>
    public TimeSpan BaseBanDuration { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Duration for permanent bans (default: 10 years).
    /// </summary>
    public TimeSpan PermanentBanDuration { get; init; } = TimeSpan.FromDays(3650);

    /// <summary>
    /// Maximum number of bans to track (prevents memory exhaustion).
    /// </summary>
    public int MaxBans { get; init; } = 10000;

    /// <summary>
    /// Maximum tracked IPs/usernames (prevents memory exhaustion).
    /// </summary>
    public int MaxTrackedEntities { get; init; } = 50000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViolationTracker"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ViolationTracker(ILogger<ViolationTracker> logger)
    {
        _logger = logger;
        _cleanupTimer = new System.Threading.Timer(
            CleanupExpired,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Record a violation from an IP address.
    /// </summary>
    /// <param name="ip">The IP address.</param>
    /// <param name="type">Type of violation.</param>
    /// <param name="details">Additional details.</param>
    /// <returns>Action taken in response.</returns>
    public ViolationAction RecordIpViolation(IPAddress ip, ViolationType type, string? details = null)
    {
        return RecordViolation(_ipViolations, ip.ToString(), type, details, $"IP:{ip}");
    }

    /// <summary>
    /// Record a violation from a username.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="type">Type of violation.</param>
    /// <param name="details">Additional details.</param>
    /// <returns>Action taken in response.</returns>
    public ViolationAction RecordUsernameViolation(string username, ViolationType type, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return ViolationAction.None;
        }

        return RecordViolation(_usernameViolations, username.ToLowerInvariant(), type, details, $"User:{username}");
    }

    /// <summary>
    /// Check if an IP is currently banned.
    /// </summary>
    public bool IsIpBanned(IPAddress ip)
    {
        return IsBanned($"IP:{ip}");
    }

    /// <summary>
    /// Check if a username is currently banned.
    /// </summary>
    public bool IsUsernameBanned(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return IsBanned($"User:{username.ToLowerInvariant()}");
    }

    /// <summary>
    /// Get violation record for an IP.
    /// </summary>
    public ViolationRecord? GetIpViolations(IPAddress ip)
    {
        return _ipViolations.TryGetValue(ip.ToString(), out var record) ? record : null;
    }

    /// <summary>
    /// Get violation record for a username.
    /// </summary>
    public ViolationRecord? GetUsernameViolations(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return _usernameViolations.TryGetValue(username.ToLowerInvariant(), out var record) ? record : null;
    }

    /// <summary>
    /// Get ban record for an IP.
    /// </summary>
    public BanRecord? GetIpBan(IPAddress ip)
    {
        return _bans.TryGetValue($"IP:{ip}", out var ban) && ban.ExpiresAt > DateTimeOffset.UtcNow ? ban : null;
    }

    /// <summary>
    /// Get ban record for a username.
    /// </summary>
    public BanRecord? GetUsernameBan(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var key = $"User:{username.ToLowerInvariant()}";
        return _bans.TryGetValue(key, out var ban) && ban.ExpiresAt > DateTimeOffset.UtcNow ? ban : null;
    }

    /// <summary>
    /// Manually ban an IP.
    /// </summary>
    public void BanIp(IPAddress ip, string reason, TimeSpan? duration = null, bool permanent = false)
    {
        Ban($"IP:{ip}", reason, duration, permanent);
    }

    /// <summary>
    /// Manually ban a username.
    /// </summary>
    public void BanUsername(string username, string reason, TimeSpan? duration = null, bool permanent = false)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        Ban($"User:{username.ToLowerInvariant()}", reason, duration, permanent);
    }

    /// <summary>
    /// Unban an IP.
    /// </summary>
    public bool UnbanIp(IPAddress ip)
    {
        var removed = _bans.TryRemove($"IP:{ip}", out _);
        if (removed)
        {
            _logger.LogInformation("Unbanned IP {Ip}", ip);
        }

        return removed;
    }

    /// <summary>
    /// Unban a username.
    /// </summary>
    public bool UnbanUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var key = $"User:{username.ToLowerInvariant()}";
        var removed = _bans.TryRemove(key, out _);
        if (removed)
        {
            _logger.LogInformation("Unbanned username {Username}", username);
        }

        return removed;
    }

    /// <summary>
    /// Get statistics about tracked violations and bans.
    /// </summary>
    public ViolationStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        return new ViolationStats
        {
            TrackedIps = _ipViolations.Count,
            TrackedUsernames = _usernameViolations.Count,
            ActiveBans = _bans.Count(kvp => kvp.Value.ExpiresAt > now),
            PermanentBans = _bans.Count(kvp => kvp.Value.IsPermanent),
            TotalIpViolations = _ipViolations.Values.Sum(v => v.TotalViolations),
            TotalUsernameViolations = _usernameViolations.Values.Sum(v => v.TotalViolations),
        };
    }

    /// <summary>
    /// Get all active bans.
    /// </summary>
    public IReadOnlyList<BanRecord> GetActiveBans()
    {
        var now = DateTimeOffset.UtcNow;
        return _bans.Values.Where(b => b.ExpiresAt > now).OrderByDescending(b => b.BannedAt).ToList();
    }

    private ViolationAction RecordViolation(
        ConcurrentDictionary<string, ViolationRecord> tracker,
        string key,
        ViolationType type,
        string? details,
        string banKey)
    {
        var now = DateTimeOffset.UtcNow;

        // SECURITY: Check entity limit before adding new entries
        if (tracker.Count >= MaxTrackedEntities && !tracker.ContainsKey(key))
        {
            _logger.LogWarning("Cannot track violations for {Key}: max entities limit ({Max}) reached", key, MaxTrackedEntities);
            // Still try to ban if this looks like a serious violation
            if (type is ViolationType.PathTraversal or ViolationType.DangerousContent or ViolationType.CertificateMismatch)
            {
                Ban(banKey, $"Immediate ban (tracking limit): {type}", permanent: false);
                return ViolationAction.TemporaryBan;
            }

            return ViolationAction.None;
        }

        var record = tracker.GetOrAdd(key, _ => new ViolationRecord());

        lock (record)
        {
            // Clean old violations outside window
            while (record.RecentViolations.Count > 0 &&
                   record.RecentViolations.Peek().Timestamp < now - ViolationWindow)
            {
                record.RecentViolations.Dequeue();
            }

            // Record this violation
            record.RecentViolations.Enqueue(new Violation
            {
                Type = type,
                Timestamp = now,
                Details = details,
            });
            record.TotalViolations++;
            record.LastViolation = now;

            var recentCount = record.RecentViolations.Count;

            _logger.LogDebug(
                "Violation recorded for {Key}: {Type} ({Count} in window, {Total} total)",
                key, type, recentCount, record.TotalViolations);

            // Check if we should escalate
            if (recentCount >= ViolationsBeforeAutoBan)
            {
                record.AutoBanCount++;

                if (record.AutoBanCount >= AutoBansBeforePermanent)
                {
                    // Permanent ban
                    Ban(banKey, $"Auto-permanent: {record.TotalViolations} violations, {record.AutoBanCount} auto-bans", permanent: true);
                    _logger.LogWarning(
                        "PERMANENT BAN for {Key}: {Total} total violations, {AutoBans} auto-bans",
                        key, record.TotalViolations, record.AutoBanCount);
                    return ViolationAction.PermanentBan;
                }
                else
                {
                    // Escalating temporary ban (exponential backoff)
                    var banDuration = TimeSpan.FromTicks(BaseBanDuration.Ticks * (long)Math.Pow(2, record.AutoBanCount - 1));
                    Ban(banKey, $"Auto-ban #{record.AutoBanCount}: {recentCount} violations in {ViolationWindow.TotalHours}h", banDuration);
                    _logger.LogWarning(
                        "Auto-ban #{AutoBan} for {Key}: {Count} violations, banned for {Duration}",
                        record.AutoBanCount, key, recentCount, banDuration);

                    // Clear recent violations after ban
                    record.RecentViolations.Clear();
                    return ViolationAction.TemporaryBan;
                }
            }
            else if (recentCount >= ViolationsBeforeAutoBan / 2)
            {
                return ViolationAction.Warning;
            }

            return ViolationAction.None;
        }
    }

    private bool IsBanned(string key)
    {
        if (_bans.TryGetValue(key, out var ban))
        {
            if (ban.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            // Ban expired, remove it
            _bans.TryRemove(key, out _);
        }

        return false;
    }

    private void Ban(string key, string reason, TimeSpan? duration = null, bool permanent = false)
    {
        // SECURITY: Enforce max bans limit to prevent memory exhaustion
        if (_bans.Count >= MaxBans && !_bans.ContainsKey(key))
        {
            _logger.LogWarning("Cannot add ban for {Key}: max bans limit ({Max}) reached", key, MaxBans);
            return;
        }

        var effectiveDuration = permanent ? PermanentBanDuration : (duration ?? BaseBanDuration);
        var ban = new BanRecord
        {
            Key = key,
            Reason = reason,
            BannedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(effectiveDuration),
            IsPermanent = permanent,
        };

        _bans[key] = ban;

        _logger.LogWarning(
            "Banned {Key} for {Duration}: {Reason}",
            key, effectiveDuration, reason);
    }

    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredBans = 0;

        foreach (var kvp in _bans)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                if (_bans.TryRemove(kvp.Key, out _))
                {
                    expiredBans++;
                }
            }
        }

        if (expiredBans > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired bans", expiredBans);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// Tracks violations for a specific entity (IP or username).
/// </summary>
public sealed class ViolationRecord
{
    /// <summary>
    /// Gets recent violations within the time window.
    /// </summary>
    public Queue<Violation> RecentViolations { get; } = new();

    /// <summary>
    /// Gets or sets total violations ever recorded.
    /// </summary>
    public long TotalViolations { get; set; }

    /// <summary>
    /// Gets or sets number of automatic bans triggered.
    /// </summary>
    public int AutoBanCount { get; set; }

    /// <summary>
    /// Gets or sets when the last violation occurred.
    /// </summary>
    public DateTimeOffset? LastViolation { get; set; }

    /// <summary>
    /// Gets when the first violation was recorded.
    /// </summary>
    public DateTimeOffset FirstViolation { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A single recorded violation.
/// </summary>
public sealed class Violation
{
    /// <summary>
    /// Gets or sets the type of violation.
    /// </summary>
    public required ViolationType Type { get; init; }

    /// <summary>
    /// Gets or sets when the violation occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets additional details about the violation.
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// Types of security violations.
/// </summary>
public enum ViolationType
{
    /// <summary>Invalid or malformed message.</summary>
    InvalidMessage,

    /// <summary>Protocol violation (bad magic, wrong version).</summary>
    ProtocolViolation,

    /// <summary>Rate limit exceeded.</summary>
    RateLimitExceeded,

    /// <summary>Failed authentication attempt.</summary>
    AuthenticationFailure,

    /// <summary>Certificate pin mismatch (potential MITM).</summary>
    CertificateMismatch,

    /// <summary>Path traversal attempt.</summary>
    PathTraversal,

    /// <summary>Dangerous content detected.</summary>
    DangerousContent,

    /// <summary>Suspicious behavior pattern.</summary>
    SuspiciousBehavior,

    /// <summary>Abuse or harassment.</summary>
    Abuse,

    /// <summary>Other violation.</summary>
    Other,
}

/// <summary>
/// Action taken in response to a violation.
/// </summary>
public enum ViolationAction
{
    /// <summary>No action taken (under threshold).</summary>
    None,

    /// <summary>Warning threshold reached.</summary>
    Warning,

    /// <summary>Temporary ban applied.</summary>
    TemporaryBan,

    /// <summary>Permanent ban applied.</summary>
    PermanentBan,
}

/// <summary>
/// Record of a ban.
/// </summary>
public sealed class BanRecord
{
    /// <summary>
    /// Gets or sets the key identifying the banned entity.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets the reason for the ban.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets or sets when the ban was applied.
    /// </summary>
    public required DateTimeOffset BannedAt { get; init; }

    /// <summary>
    /// Gets or sets when the ban expires.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a permanent ban.
    /// </summary>
    public required bool IsPermanent { get; init; }

    /// <summary>
    /// Gets the time remaining until the ban expires.
    /// </summary>
    public TimeSpan TimeRemaining => ExpiresAt > DateTimeOffset.UtcNow
        ? ExpiresAt - DateTimeOffset.UtcNow
        : TimeSpan.Zero;
}

/// <summary>
/// Statistics about tracked violations and bans.
/// </summary>
public sealed class ViolationStats
{
    /// <summary>Gets or sets number of IPs being tracked.</summary>
    public int TrackedIps { get; init; }

    /// <summary>Gets or sets number of usernames being tracked.</summary>
    public int TrackedUsernames { get; init; }

    /// <summary>Gets or sets number of currently active bans.</summary>
    public int ActiveBans { get; init; }

    /// <summary>Gets or sets number of permanent bans.</summary>
    public int PermanentBans { get; init; }

    /// <summary>Gets or sets total IP violations ever recorded.</summary>
    public long TotalIpViolations { get; init; }

    /// <summary>Gets or sets total username violations ever recorded.</summary>
    public long TotalUsernameViolations { get; init; }
}

