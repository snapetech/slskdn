// <copyright file="PeerReputation.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tracks peer reputation based on behavioral patterns.
/// SECURITY: Identifies and deprioritizes unreliable or malicious peers.
/// </summary>
public sealed class PeerReputation
{
    private readonly ILogger<PeerReputation> _logger;
    private readonly ConcurrentDictionary<string, PeerProfile> _profiles = new();

    /// <summary>
    /// Base score for new peers.
    /// </summary>
    public const int BaseScore = 50;

    /// <summary>
    /// Maximum reputation score.
    /// </summary>
    public const int MaxScore = 100;

    /// <summary>
    /// Minimum reputation score (0 = effectively blocked).
    /// </summary>
    public const int MinScore = 0;

    /// <summary>
    /// Score threshold below which peers are considered untrusted.
    /// </summary>
    public const int UntrustedThreshold = 20;

    /// <summary>
    /// Score threshold above which peers are considered trusted.
    /// </summary>
    public const int TrustedThreshold = 70;

    // Scoring weights
    private const int SuccessfulTransferBonus = 2;
    private const int FailedTransferPenalty = 5;
    private const int AbortedTransferPenalty = 3;
    private const int MalformedMessagePenalty = 10;
    private const int ProtocolViolationPenalty = 15;
    private const int ContentMismatchPenalty = 20;
    private const int FriendlyBehaviorBonus = 1;
    private const int SlotAvailabilityBonus = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeerReputation"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public PeerReputation(ILogger<PeerReputation> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get or create a profile for a peer.
    /// </summary>
    /// <param name="username">The peer's username.</param>
    /// <returns>The peer's profile.</returns>
    public PeerProfile GetOrCreateProfile(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty", nameof(username));
        }

        var normalizedUsername = username.ToLowerInvariant();
        return _profiles.GetOrAdd(normalizedUsername, _ => new PeerProfile
        {
            Username = username,
            Score = BaseScore,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>
    /// Get a peer's current reputation score.
    /// </summary>
    /// <param name="username">The peer's username.</param>
    /// <returns>Score (0-100) or null if unknown.</returns>
    public int? GetScore(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalizedUsername = username.ToLowerInvariant();
        return _profiles.TryGetValue(normalizedUsername, out var profile) ? profile.Score : null;
    }

    /// <summary>
    /// Check if a peer is trusted (high reputation).
    /// </summary>
    public bool IsTrusted(string username)
    {
        var score = GetScore(username);
        return score.HasValue && score.Value >= TrustedThreshold;
    }

    /// <summary>
    /// Check if a peer is untrusted (low reputation).
    /// </summary>
    public bool IsUntrusted(string username)
    {
        var score = GetScore(username);
        return score.HasValue && score.Value <= UntrustedThreshold;
    }

    /// <summary>
    /// Record a successful transfer from this peer.
    /// </summary>
    public void RecordSuccessfulTransfer(string username, long bytesTransferred)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.SuccessfulTransfers++;
            profile.TotalBytesTransferred += bytesTransferred;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, SuccessfulTransferBonus, "Successful transfer");
        }
    }

    /// <summary>
    /// Record a failed transfer from this peer.
    /// </summary>
    public void RecordFailedTransfer(string username, string? reason = null)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.FailedTransfers++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, -FailedTransferPenalty, $"Failed transfer: {reason ?? "unknown"}");
        }
    }

    /// <summary>
    /// Record an aborted transfer (user cancelled, timeout, etc.).
    /// </summary>
    public void RecordAbortedTransfer(string username, string? reason = null)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.AbortedTransfers++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, -AbortedTransferPenalty, $"Aborted transfer: {reason ?? "unknown"}");
        }
    }

    /// <summary>
    /// Record a malformed message from this peer.
    /// </summary>
    public void RecordMalformedMessage(string username)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.MalformedMessages++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, -MalformedMessagePenalty, "Malformed message");
        }
    }

    /// <summary>
    /// Record a protocol violation from this peer.
    /// </summary>
    public void RecordProtocolViolation(string username, string violation)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.ProtocolViolations++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, -ProtocolViolationPenalty, $"Protocol violation: {violation}");
        }
    }

    /// <summary>
    /// Record a content mismatch (file doesn't match claimed hash/type).
    /// </summary>
    public void RecordContentMismatch(string username, string details)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.ContentMismatches++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, -ContentMismatchPenalty, $"Content mismatch: {details}");
        }
    }

    /// <summary>
    /// Record friendly behavior (chat, sharing, etc.).
    /// </summary>
    public void RecordFriendlyBehavior(string username)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, FriendlyBehaviorBonus, "Friendly behavior");
        }
    }

    /// <summary>
    /// Record that peer had slots available (good uploader).
    /// </summary>
    public void RecordSlotAvailability(string username)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            profile.SlotsAvailableCount++;
            profile.LastSeen = DateTimeOffset.UtcNow;
            AdjustScore(profile, SlotAvailabilityBonus, "Slot available");
        }
    }

    /// <summary>
    /// Manually adjust a peer's reputation.
    /// </summary>
    public void SetScore(string username, int score, string reason)
    {
        var profile = GetOrCreateProfile(username);
        lock (profile)
        {
            var oldScore = profile.Score;
            profile.Score = Math.Clamp(score, MinScore, MaxScore);
            _logger.LogInformation(
                "Manually set {Username} reputation: {OldScore} -> {NewScore} ({Reason})",
                username, oldScore, profile.Score, reason);
        }
    }

    /// <summary>
    /// Get all profiles sorted by score (lowest first - most suspicious).
    /// </summary>
    public IReadOnlyList<PeerProfile> GetSuspiciousPeers(int limit = 50)
    {
        return _profiles.Values
            .Where(p => p.Score < BaseScore)
            .OrderBy(p => p.Score)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get all profiles sorted by score (highest first - most trusted).
    /// </summary>
    public IReadOnlyList<PeerProfile> GetTrustedPeers(int limit = 50)
    {
        return _profiles.Values
            .Where(p => p.Score >= TrustedThreshold)
            .OrderByDescending(p => p.Score)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get statistics about peer reputations.
    /// </summary>
    public ReputationStats GetStats()
    {
        var profiles = _profiles.Values.ToList();
        return new ReputationStats
        {
            TotalPeers = profiles.Count,
            TrustedPeers = profiles.Count(p => p.Score >= TrustedThreshold),
            UntrustedPeers = profiles.Count(p => p.Score <= UntrustedThreshold),
            AverageScore = profiles.Count > 0 ? profiles.Average(p => p.Score) : BaseScore,
            TotalSuccessfulTransfers = profiles.Sum(p => p.SuccessfulTransfers),
            TotalFailedTransfers = profiles.Sum(p => p.FailedTransfers),
            TotalProtocolViolations = profiles.Sum(p => p.ProtocolViolations),
        };
    }

    /// <summary>
    /// Rank peers by reputation for source selection.
    /// Higher score = better rank = preferred source.
    /// </summary>
    /// <param name="usernames">Usernames to rank.</param>
    /// <returns>Usernames sorted by reputation (best first).</returns>
    public IReadOnlyList<string> RankByReputation(IEnumerable<string> usernames)
    {
        return usernames
            .Select(u => (Username: u, Score: GetScore(u) ?? BaseScore))
            .OrderByDescending(x => x.Score)
            .Select(x => x.Username)
            .ToList();
    }

    private void AdjustScore(PeerProfile profile, int adjustment, string reason)
    {
        var oldScore = profile.Score;
        profile.Score = Math.Clamp(profile.Score + adjustment, MinScore, MaxScore);

        if (profile.Score != oldScore)
        {
            _logger.LogDebug(
                "{Username} reputation: {OldScore} -> {NewScore} ({Adjustment:+#;-#;0}) - {Reason}",
                profile.Username, oldScore, profile.Score, adjustment, reason);

            // Log warnings for significant changes
            if (oldScore > UntrustedThreshold && profile.Score <= UntrustedThreshold)
            {
                _logger.LogWarning(
                    "Peer {Username} dropped below untrusted threshold: score={Score}",
                    profile.Username, profile.Score);
            }
        }
    }
}

/// <summary>
/// Profile tracking a peer's behavior and reputation.
/// </summary>
public sealed class PeerProfile
{
    /// <summary>Gets or sets the peer's username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets the current reputation score (0-100).</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets when this peer was first seen.</summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Gets or sets when this peer was last seen.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets count of successful transfers.</summary>
    public long SuccessfulTransfers { get; set; }

    /// <summary>Gets or sets count of failed transfers.</summary>
    public long FailedTransfers { get; set; }

    /// <summary>Gets or sets count of aborted transfers.</summary>
    public long AbortedTransfers { get; set; }

    /// <summary>Gets or sets total bytes transferred.</summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>Gets or sets count of malformed messages received.</summary>
    public long MalformedMessages { get; set; }

    /// <summary>Gets or sets count of protocol violations.</summary>
    public long ProtocolViolations { get; set; }

    /// <summary>Gets or sets count of content mismatches.</summary>
    public long ContentMismatches { get; set; }

    /// <summary>Gets or sets count of times slots were available.</summary>
    public long SlotsAvailableCount { get; set; }

    /// <summary>Gets the success rate (0.0-1.0).</summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessfulTransfers + FailedTransfers + AbortedTransfers;
            return total > 0 ? (double)SuccessfulTransfers / total : 0.5;
        }
    }

    /// <summary>Gets the trust level based on score.</summary>
    public TrustLevel TrustLevel => Score switch
    {
        >= PeerReputation.TrustedThreshold => TrustLevel.Trusted,
        <= PeerReputation.UntrustedThreshold => TrustLevel.Untrusted,
        _ => TrustLevel.Neutral,
    };
}

/// <summary>
/// Trust level categorization.
/// </summary>
public enum TrustLevel
{
    /// <summary>Peer has low reputation and should be avoided.</summary>
    Untrusted,

    /// <summary>Peer has neutral reputation (unknown or average).</summary>
    Neutral,

    /// <summary>Peer has high reputation and is preferred.</summary>
    Trusted,
}

/// <summary>
/// Statistics about peer reputations.
/// </summary>
public sealed class ReputationStats
{
    /// <summary>Gets or sets total peers tracked.</summary>
    public int TotalPeers { get; init; }

    /// <summary>Gets or sets count of trusted peers.</summary>
    public int TrustedPeers { get; init; }

    /// <summary>Gets or sets count of untrusted peers.</summary>
    public int UntrustedPeers { get; init; }

    /// <summary>Gets or sets average reputation score.</summary>
    public double AverageScore { get; init; }

    /// <summary>Gets or sets total successful transfers across all peers.</summary>
    public long TotalSuccessfulTransfers { get; init; }

    /// <summary>Gets or sets total failed transfers across all peers.</summary>
    public long TotalFailedTransfers { get; init; }

    /// <summary>Gets or sets total protocol violations across all peers.</summary>
    public long TotalProtocolViolations { get; init; }
}

