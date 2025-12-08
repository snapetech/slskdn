// <copyright file="OverlayBlocklist.cs" company="slskdn Team">
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
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages a blocklist of IPs and usernames that are banned from overlay connections.
/// </summary>
public sealed class OverlayBlocklist : IDisposable
{
    private readonly ILogger<OverlayBlocklist> _logger;
    private readonly ConcurrentDictionary<IPAddress, BlocklistEntry> _ipBlocklist = new();
    private readonly ConcurrentDictionary<string, BlocklistEntry> _usernameBlocklist = new();
    private readonly Timer _cleanupTimer;
    
    /// <summary>
    /// Default ban duration for temporary bans.
    /// </summary>
    public static readonly TimeSpan DefaultBanDuration = TimeSpan.FromHours(24);
    
    /// <summary>
    /// Duration for permanent bans (10 years).
    /// </summary>
    public static readonly TimeSpan PermanentBanDuration = TimeSpan.FromDays(3650);
    
    public OverlayBlocklist(ILogger<OverlayBlocklist> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
    /// <summary>
    /// Check if an IP address is blocked.
    /// </summary>
    public bool IsBlocked(IPAddress ip)
    {
        if (_ipBlocklist.TryGetValue(ip, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }
            
            // Entry expired, remove it
            _ipBlocklist.TryRemove(ip, out _);
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if a username is blocked.
    /// </summary>
    public bool IsBlocked(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }
        
        var normalizedUsername = username.ToLowerInvariant();
        
        if (_usernameBlocklist.TryGetValue(normalizedUsername, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }
            
            _usernameBlocklist.TryRemove(normalizedUsername, out _);
        }
        
        return false;
    }
    
    /// <summary>
    /// Block an IP address.
    /// </summary>
    /// <param name="ip">IP to block.</param>
    /// <param name="reason">Reason for blocking.</param>
    /// <param name="duration">How long to block (null = default 24h).</param>
    /// <param name="permanent">If true, block permanently.</param>
    public void BlockIp(IPAddress ip, string reason, TimeSpan? duration = null, bool permanent = false)
    {
        var effectiveDuration = permanent ? PermanentBanDuration : (duration ?? DefaultBanDuration);
        var entry = new BlocklistEntry
        {
            Reason = reason,
            BlockedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(effectiveDuration),
            IsPermanent = permanent,
        };
        
        _ipBlocklist[ip] = entry;
        
        _logger.LogWarning(
            "Blocked IP {Ip} for {Duration}: {Reason}",
            ip,
            effectiveDuration,
            reason);
    }
    
    /// <summary>
    /// Block a username.
    /// </summary>
    public void BlockUsername(string username, string reason, TimeSpan? duration = null, bool permanent = false)
    {
        if (string.IsNullOrEmpty(username))
        {
            return;
        }
        
        var normalizedUsername = username.ToLowerInvariant();
        var effectiveDuration = permanent ? PermanentBanDuration : (duration ?? DefaultBanDuration);
        var entry = new BlocklistEntry
        {
            Reason = reason,
            BlockedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(effectiveDuration),
            IsPermanent = permanent,
        };
        
        _usernameBlocklist[normalizedUsername] = entry;
        
        _logger.LogWarning(
            "Blocked username {Username} for {Duration}: {Reason}",
            username,
            effectiveDuration,
            reason);
    }
    
    /// <summary>
    /// Unblock an IP address.
    /// </summary>
    public bool UnblockIp(IPAddress ip)
    {
        var removed = _ipBlocklist.TryRemove(ip, out _);
        if (removed)
        {
            _logger.LogInformation("Unblocked IP {Ip}", ip);
        }
        
        return removed;
    }
    
    /// <summary>
    /// Unblock a username.
    /// </summary>
    public bool UnblockUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }
        
        var normalizedUsername = username.ToLowerInvariant();
        var removed = _usernameBlocklist.TryRemove(normalizedUsername, out _);
        if (removed)
        {
            _logger.LogInformation("Unblocked username {Username}", username);
        }
        
        return removed;
    }
    
    /// <summary>
    /// Get all blocked IPs.
    /// </summary>
    public IReadOnlyDictionary<IPAddress, BlocklistEntry> GetBlockedIps()
    {
        var now = DateTimeOffset.UtcNow;
        return _ipBlocklist
            .Where(kvp => kvp.Value.ExpiresAt > now)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    /// <summary>
    /// Get all blocked usernames.
    /// </summary>
    public IReadOnlyDictionary<string, BlocklistEntry> GetBlockedUsernames()
    {
        var now = DateTimeOffset.UtcNow;
        return _usernameBlocklist
            .Where(kvp => kvp.Value.ExpiresAt > now)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    /// <summary>
    /// Get blocklist statistics.
    /// </summary>
    public BlocklistStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        return new BlocklistStats
        {
            BlockedIpCount = _ipBlocklist.Count(kvp => kvp.Value.ExpiresAt > now),
            BlockedUsernameCount = _usernameBlocklist.Count(kvp => kvp.Value.ExpiresAt > now),
            PermanentIpBans = _ipBlocklist.Count(kvp => kvp.Value.IsPermanent),
            PermanentUsernameBans = _usernameBlocklist.Count(kvp => kvp.Value.IsPermanent),
        };
    }
    
    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var removedIps = 0;
        var removedUsernames = 0;
        
        foreach (var kvp in _ipBlocklist)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                if (_ipBlocklist.TryRemove(kvp.Key, out _))
                {
                    removedIps++;
                }
            }
        }
        
        foreach (var kvp in _usernameBlocklist)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                if (_usernameBlocklist.TryRemove(kvp.Key, out _))
                {
                    removedUsernames++;
                }
            }
        }
        
        if (removedIps > 0 || removedUsernames > 0)
        {
            _logger.LogDebug(
                "Cleaned up expired blocklist entries: {IpCount} IPs, {UsernameCount} usernames",
                removedIps,
                removedUsernames);
        }
    }
    
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// Entry in the blocklist.
/// </summary>
public sealed class BlocklistEntry
{
    /// <summary>
    /// Reason for blocking.
    /// </summary>
    public required string Reason { get; init; }
    
    /// <summary>
    /// When the block was applied.
    /// </summary>
    public required DateTimeOffset BlockedAt { get; init; }
    
    /// <summary>
    /// When the block expires.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }
    
    /// <summary>
    /// Whether this is a permanent ban.
    /// </summary>
    public required bool IsPermanent { get; init; }
    
    /// <summary>
    /// Time remaining until unblock.
    /// </summary>
    public TimeSpan TimeRemaining => ExpiresAt > DateTimeOffset.UtcNow
        ? ExpiresAt - DateTimeOffset.UtcNow
        : TimeSpan.Zero;
}

/// <summary>
/// Blocklist statistics.
/// </summary>
public sealed class BlocklistStats
{
    public int BlockedIpCount { get; init; }
    public int BlockedUsernameCount { get; init; }
    public int PermanentIpBans { get; init; }
    public int PermanentUsernameBans { get; init; }
    public int TotalBlocked => BlockedIpCount + BlockedUsernameCount;
}


/// <summary>
/// Tracks violations for a specific IP address.
/// </summary>
public sealed class ViolationRecord
{
    /// <summary>
    /// Recent violations within the time window.
    /// </summary>
    public Queue<Violation> RecentViolations { get; } = new();
    
    /// <summary>
    /// Total violations ever recorded.
    /// </summary>
    public long TotalViolations { get; set; }
    
    /// <summary>
    /// Number of automatic bans triggered.
    /// </summary>
    public int AutoBanCount { get; set; }
    
    /// <summary>
    /// When the last violation occurred.
    /// </summary>
    public DateTimeOffset? LastViolation { get; set; }
    
    /// <summary>
    /// First violation timestamp.
    /// </summary>
    public DateTimeOffset FirstViolation { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A single recorded violation.
/// </summary>
public sealed class Violation
{
    public required ViolationType Type { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
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
