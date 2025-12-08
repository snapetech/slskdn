// <copyright file="AsymmetricDisclosure.cs" company="slskdN">
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
/// Implements asymmetric information disclosure based on trust levels.
/// SECURITY: Gradually reveals more information as trust is established.
/// </summary>
public sealed class AsymmetricDisclosure
{
    private readonly ILogger<AsymmetricDisclosure> _logger;
    private readonly ConcurrentDictionary<string, PeerTrustState> _trustStates = new();
    private readonly DisclosurePolicy _policy;

    /// <summary>
    /// Maximum peers to track.
    /// </summary>
    public const int MaxPeers = 10000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsymmetricDisclosure"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="policy">Disclosure policy (uses default if null).</param>
    public AsymmetricDisclosure(ILogger<AsymmetricDisclosure> logger, DisclosurePolicy? policy = null)
    {
        _logger = logger;
        _policy = policy ?? DisclosurePolicy.Default;
    }

    /// <summary>
    /// Get the trust level for a peer.
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <returns>Current trust level.</returns>
    public TrustTier GetTrustLevel(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return TrustTier.Unknown;
        }

        var state = GetOrCreateState(username);
        return state.CurrentTier;
    }

    /// <summary>
    /// Determine what to disclose to a peer based on their trust level.
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <returns>Disclosure permissions for this peer.</returns>
    public DisclosurePermissions GetDisclosurePermissions(string username)
    {
        var tier = GetTrustLevel(username);
        return _policy.GetPermissions(tier);
    }

    /// <summary>
    /// Record a positive interaction with a peer (increases trust).
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <param name="interactionType">Type of interaction.</param>
    /// <param name="value">Value/weight of interaction.</param>
    public void RecordPositiveInteraction(string username, InteractionType interactionType, int value = 1)
    {
        var state = GetOrCreateState(username);

        lock (state)
        {
            state.PositiveInteractions += value;
            state.LastInteraction = DateTimeOffset.UtcNow;
            state.InteractionHistory.Add(new TrustInteraction
            {
                Type = interactionType,
                Value = value,
                IsPositive = true,
                Timestamp = DateTimeOffset.UtcNow,
            });

            // Trim history
            while (state.InteractionHistory.Count > 100)
            {
                state.InteractionHistory.RemoveAt(0);
            }

            // Recalculate tier
            var oldTier = state.CurrentTier;
            state.CurrentTier = CalculateTier(state);

            if (state.CurrentTier != oldTier)
            {
                _logger.LogInformation(
                    "Trust upgraded for {Username}: {OldTier} -> {NewTier}",
                    username, oldTier, state.CurrentTier);
            }
        }
    }

    /// <summary>
    /// Record a negative interaction with a peer (decreases trust).
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <param name="interactionType">Type of interaction.</param>
    /// <param name="value">Value/weight of interaction.</param>
    public void RecordNegativeInteraction(string username, InteractionType interactionType, int value = 1)
    {
        var state = GetOrCreateState(username);

        lock (state)
        {
            state.NegativeInteractions += value;
            state.LastInteraction = DateTimeOffset.UtcNow;
            state.InteractionHistory.Add(new TrustInteraction
            {
                Type = interactionType,
                Value = value,
                IsPositive = false,
                Timestamp = DateTimeOffset.UtcNow,
            });

            // Trim history
            while (state.InteractionHistory.Count > 100)
            {
                state.InteractionHistory.RemoveAt(0);
            }

            // Recalculate tier
            var oldTier = state.CurrentTier;
            state.CurrentTier = CalculateTier(state);

            if (state.CurrentTier != oldTier)
            {
                _logger.LogWarning(
                    "Trust downgraded for {Username}: {OldTier} -> {NewTier}",
                    username, oldTier, state.CurrentTier);
            }
        }
    }

    /// <summary>
    /// Manually set trust tier for a peer.
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <param name="tier">Trust tier to set.</param>
    /// <param name="reason">Reason for manual override.</param>
    public void SetTrustTier(string username, TrustTier tier, string reason)
    {
        var state = GetOrCreateState(username);

        lock (state)
        {
            var oldTier = state.CurrentTier;
            state.CurrentTier = tier;
            state.ManualOverride = true;
            state.OverrideReason = reason;

            _logger.LogInformation(
                "Manual trust override for {Username}: {OldTier} -> {NewTier} ({Reason})",
                username, oldTier, tier, reason);
        }
    }

    /// <summary>
    /// Filter a file list based on peer trust level.
    /// </summary>
    /// <param name="username">Peer requesting files.</param>
    /// <param name="files">All available files.</param>
    /// <returns>Files the peer is allowed to see.</returns>
    public IReadOnlyList<T> FilterFiles<T>(string username, IEnumerable<T> files, Func<T, DisclosureTier> getFileTier)
    {
        var permissions = GetDisclosurePermissions(username);

        return files
            .Where(f => permissions.CanAccessTier(getFileTier(f)))
            .ToList();
    }

    /// <summary>
    /// Get trust state for a peer.
    /// </summary>
    public PeerTrustState? GetTrustState(string username)
    {
        return _trustStates.TryGetValue(username.ToLowerInvariant(), out var state) ? state : null;
    }

    /// <summary>
    /// Get all peers at a specific trust level.
    /// </summary>
    public IReadOnlyList<PeerTrustState> GetPeersAtTier(TrustTier tier)
    {
        return _trustStates.Values
            .Where(s => s.CurrentTier == tier)
            .OrderByDescending(s => s.PositiveInteractions - s.NegativeInteractions)
            .ToList();
    }

    /// <summary>
    /// Get statistics.
    /// </summary>
    public DisclosureStats GetStats()
    {
        var states = _trustStates.Values.ToList();
        return new DisclosureStats
        {
            TotalPeers = states.Count,
            UnknownPeers = states.Count(s => s.CurrentTier == TrustTier.Unknown),
            NewPeers = states.Count(s => s.CurrentTier == TrustTier.New),
            BasicPeers = states.Count(s => s.CurrentTier == TrustTier.Basic),
            TrustedPeers = states.Count(s => s.CurrentTier == TrustTier.Trusted),
            VettedPeers = states.Count(s => s.CurrentTier == TrustTier.Vetted),
            FriendPeers = states.Count(s => s.CurrentTier == TrustTier.Friend),
            TotalPositiveInteractions = states.Sum(s => s.PositiveInteractions),
            TotalNegativeInteractions = states.Sum(s => s.NegativeInteractions),
        };
    }

    private PeerTrustState GetOrCreateState(string username)
    {
        var key = username.ToLowerInvariant();

        // Enforce max size
        if (_trustStates.Count >= MaxPeers && !_trustStates.ContainsKey(key))
        {
            var oldest = _trustStates.Values
                .Where(s => !s.ManualOverride)
                .OrderBy(s => s.LastInteraction)
                .FirstOrDefault();
            if (oldest != null)
            {
                _trustStates.TryRemove(oldest.Username.ToLowerInvariant(), out _);
            }
        }

        return _trustStates.GetOrAdd(key, _ => new PeerTrustState
        {
            Username = username,
            FirstSeen = DateTimeOffset.UtcNow,
            LastInteraction = DateTimeOffset.UtcNow,
            CurrentTier = TrustTier.Unknown,
        });
    }

    private TrustTier CalculateTier(PeerTrustState state)
    {
        if (state.ManualOverride)
        {
            return state.CurrentTier;
        }

        var netScore = state.PositiveInteractions - (state.NegativeInteractions * 3); // Negatives weighted 3x
        var age = DateTimeOffset.UtcNow - state.FirstSeen;

        // Negative score = Unknown
        if (netScore < 0)
        {
            return TrustTier.Unknown;
        }

        // Brand new
        if (state.PositiveInteractions == 0)
        {
            return TrustTier.Unknown;
        }

        // One interaction = New
        if (state.PositiveInteractions < 3)
        {
            return TrustTier.New;
        }

        // Some interactions = Basic
        if (netScore < 10)
        {
            return TrustTier.Basic;
        }

        // Good history = Trusted
        if (netScore < 50)
        {
            return TrustTier.Trusted;
        }

        // Excellent history and age = Vetted
        if (netScore < 100 || age < TimeSpan.FromDays(30))
        {
            return TrustTier.Vetted;
        }

        // Long-term excellent = Friend
        return TrustTier.Friend;
    }
}

/// <summary>
/// Trust tiers for peers.
/// </summary>
public enum TrustTier
{
    /// <summary>Unknown peer - minimal disclosure.</summary>
    Unknown = 0,

    /// <summary>New peer - basic disclosure.</summary>
    New = 1,

    /// <summary>Basic trust established.</summary>
    Basic = 2,

    /// <summary>Trusted peer.</summary>
    Trusted = 3,

    /// <summary>Vetted peer with excellent history.</summary>
    Vetted = 4,

    /// <summary>Friend - full disclosure.</summary>
    Friend = 5,
}

/// <summary>
/// Disclosure tiers for files/information.
/// </summary>
public enum DisclosureTier
{
    /// <summary>Public - everyone can see.</summary>
    Public = 0,

    /// <summary>Standard - most users can see.</summary>
    Standard = 1,

    /// <summary>Limited - trusted users only.</summary>
    Limited = 2,

    /// <summary>Restricted - vetted users only.</summary>
    Restricted = 3,

    /// <summary>Private - friends only.</summary>
    Private = 4,
}

/// <summary>
/// Types of interactions that affect trust.
/// </summary>
public enum InteractionType
{
    /// <summary>Successful file transfer.</summary>
    SuccessfulTransfer,

    /// <summary>Failed file transfer.</summary>
    FailedTransfer,

    /// <summary>Aborted transfer.</summary>
    AbortedTransfer,

    /// <summary>Valid message received.</summary>
    ValidMessage,

    /// <summary>Invalid/malformed message.</summary>
    InvalidMessage,

    /// <summary>Helpful behavior.</summary>
    Helpful,

    /// <summary>Abusive behavior.</summary>
    Abusive,

    /// <summary>Spam detected.</summary>
    Spam,

    /// <summary>Protocol violation.</summary>
    ProtocolViolation,
}

/// <summary>
/// Trust state for a peer.
/// </summary>
public sealed class PeerTrustState
{
    /// <summary>Gets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets when first seen.</summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Gets or sets when last interacted.</summary>
    public DateTimeOffset LastInteraction { get; set; }

    /// <summary>Gets or sets current trust tier.</summary>
    public TrustTier CurrentTier { get; set; }

    /// <summary>Gets or sets positive interactions count.</summary>
    public int PositiveInteractions { get; set; }

    /// <summary>Gets or sets negative interactions count.</summary>
    public int NegativeInteractions { get; set; }

    /// <summary>Gets or sets whether manually overridden.</summary>
    public bool ManualOverride { get; set; }

    /// <summary>Gets or sets override reason.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>Gets interaction history.</summary>
    public List<TrustInteraction> InteractionHistory { get; } = new();
}

/// <summary>
/// A recorded trust interaction.
/// </summary>
public sealed class TrustInteraction
{
    /// <summary>Gets the interaction type.</summary>
    public required InteractionType Type { get; init; }

    /// <summary>Gets the value/weight.</summary>
    public required int Value { get; init; }

    /// <summary>Gets whether positive.</summary>
    public required bool IsPositive { get; init; }

    /// <summary>Gets when occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Disclosure permissions for a peer.
/// </summary>
public sealed class DisclosurePermissions
{
    /// <summary>Gets the peer's trust tier.</summary>
    public required TrustTier PeerTier { get; init; }

    /// <summary>Gets the maximum disclosure tier the peer can access.</summary>
    public required DisclosureTier MaxDisclosureTier { get; init; }

    /// <summary>Gets whether peer can see file counts.</summary>
    public bool CanSeeFileCounts { get; init; }

    /// <summary>Gets whether peer can see folder structure.</summary>
    public bool CanSeeFolderStructure { get; init; }

    /// <summary>Gets whether peer can see full paths.</summary>
    public bool CanSeeFullPaths { get; init; }

    /// <summary>Gets whether peer can see audio metadata.</summary>
    public bool CanSeeAudioMetadata { get; init; }

    /// <summary>Gets whether peer can browse.</summary>
    public bool CanBrowse { get; init; }

    /// <summary>Gets whether peer can download.</summary>
    public bool CanDownload { get; init; }

    /// <summary>Check if peer can access a disclosure tier.</summary>
    public bool CanAccessTier(DisclosureTier tier) => tier <= MaxDisclosureTier;
}

/// <summary>
/// Disclosure policy configuration.
/// </summary>
public sealed class DisclosurePolicy
{
    /// <summary>
    /// Gets the default policy.
    /// </summary>
    public static DisclosurePolicy Default { get; } = new();

    /// <summary>
    /// Get permissions for a trust tier.
    /// </summary>
    public DisclosurePermissions GetPermissions(TrustTier tier)
    {
        return tier switch
        {
            TrustTier.Unknown => new DisclosurePermissions
            {
                PeerTier = tier,
                MaxDisclosureTier = DisclosureTier.Public,
                CanSeeFileCounts = false,
                CanSeeFolderStructure = false,
                CanSeeFullPaths = false,
                CanSeeAudioMetadata = false,
                CanBrowse = false,
                CanDownload = false,
            },
            TrustTier.New => new DisclosurePermissions
            {
                PeerTier = tier,
                MaxDisclosureTier = DisclosureTier.Public,
                CanSeeFileCounts = true,
                CanSeeFolderStructure = false,
                CanSeeFullPaths = false,
                CanSeeAudioMetadata = false,
                CanBrowse = false,
                CanDownload = true,
            },
            TrustTier.Basic => new DisclosurePermissions
            {
                PeerTier = tier,
                MaxDisclosureTier = DisclosureTier.Standard,
                CanSeeFileCounts = true,
                CanSeeFolderStructure = true,
                CanSeeFullPaths = false,
                CanSeeAudioMetadata = true,
                CanBrowse = true,
                CanDownload = true,
            },
            TrustTier.Trusted => new DisclosurePermissions
            {
                PeerTier = tier,
                MaxDisclosureTier = DisclosureTier.Limited,
                CanSeeFileCounts = true,
                CanSeeFolderStructure = true,
                CanSeeFullPaths = false,
                CanSeeAudioMetadata = true,
                CanBrowse = true,
                CanDownload = true,
            },
            TrustTier.Vetted => new DisclosurePermissions
            {
                PeerTier = tier,
                MaxDisclosureTier = DisclosureTier.Restricted,
                CanSeeFileCounts = true,
                CanSeeFolderStructure = true,
                CanSeeFullPaths = true,
                CanSeeAudioMetadata = true,
                CanBrowse = true,
                CanDownload = true,
            },
            TrustTier.Friend => new DisclosurePermissions
            {
                PeerTier = tier,
                MaxDisclosureTier = DisclosureTier.Private,
                CanSeeFileCounts = true,
                CanSeeFolderStructure = true,
                CanSeeFullPaths = true,
                CanSeeAudioMetadata = true,
                CanBrowse = true,
                CanDownload = true,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };
    }
}

/// <summary>
/// Disclosure statistics.
/// </summary>
public sealed class DisclosureStats
{
    /// <summary>Gets total peers tracked.</summary>
    public int TotalPeers { get; init; }

    /// <summary>Gets unknown tier count.</summary>
    public int UnknownPeers { get; init; }

    /// <summary>Gets new tier count.</summary>
    public int NewPeers { get; init; }

    /// <summary>Gets basic tier count.</summary>
    public int BasicPeers { get; init; }

    /// <summary>Gets trusted tier count.</summary>
    public int TrustedPeers { get; init; }

    /// <summary>Gets vetted tier count.</summary>
    public int VettedPeers { get; init; }

    /// <summary>Gets friend tier count.</summary>
    public int FriendPeers { get; init; }

    /// <summary>Gets total positive interactions.</summary>
    public long TotalPositiveInteractions { get; init; }

    /// <summary>Gets total negative interactions.</summary>
    public long TotalNegativeInteractions { get; init; }
}

