// <copyright file="PodMembershipRecord.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

/// <summary>
/// A signed pod membership record that can be published to DHT.
/// </summary>
public record PodMembershipRecord(
    string PodId,
    string PeerId,
    string Role,
    bool IsBanned,
    string? PublicKey,
    DateTimeOffset JoinedAt,
    DateTimeOffset? BannedAt,
    string? BanReason);

/// <summary>
/// A signed membership record for DHT storage.
/// </summary>
public record SignedMembershipRecord(
    PodMembershipRecord Membership,
    DateTimeOffset SignedAt,
    string Signature);

/// <summary>
/// Membership roles within a pod.
/// </summary>
public static class PodRoles
{
    public const string Owner = "owner";
    public const string Moderator = "mod";
    public const string Member = "member";
}

/// <summary>
/// Result of a membership record publish operation.
/// </summary>
public record MembershipPublishResult(
    bool Success,
    string PodId,
    string PeerId,
    string DhtKey,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt,
    string? ErrorMessage = null);

/// <summary>
/// Result of retrieving a membership record.
/// </summary>
public record MembershipRetrievalResult(
    bool Found,
    string PodId,
    string PeerId,
    SignedMembershipRecord? SignedRecord,
    DateTimeOffset RetrievedAt,
    bool IsValidSignature,
    string? ErrorMessage = null);

/// <summary>
/// Membership management statistics.
/// </summary>
public record MembershipStats(
    int TotalMemberships,
    int ActiveMemberships,
    int BannedMemberships,
    int ExpiredMemberships,
    IReadOnlyDictionary<string, int> MembershipsByRole,
    IReadOnlyDictionary<string, int> MembershipsByPod,
    DateTimeOffset LastOperation);
