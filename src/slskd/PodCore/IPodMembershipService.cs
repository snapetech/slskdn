// <copyright file="IPodMembershipService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for managing signed pod membership records in DHT.
/// </summary>
public interface IPodMembershipService
{
    /// <summary>
    /// Publishes a membership record to DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="member">The pod member to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish result.</returns>
    Task<MembershipPublishResult> PublishMembershipAsync(string podId, PodMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing membership record in DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="member">The updated pod member.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    Task<MembershipPublishResult> UpdateMembershipAsync(string podId, PodMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a membership record from DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The removal result.</returns>
    Task<MembershipPublishResult> RemoveMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a membership record from DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The membership record if found.</returns>
    Task<MembershipRetrievalResult> GetMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all membership records for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All membership records for the pod.</returns>
    Task<IReadOnlyList<MembershipRetrievalResult>> ListPodMembershipsAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bans a member from a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID to ban.</param>
    /// <param name="reason">The ban reason (stored locally, not in DHT).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ban result.</returns>
    Task<MembershipPublishResult> BanMemberAsync(string podId, string peerId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unbans a member from a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID to unban.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unban result.</returns>
    Task<MembershipPublishResult> UnbanMemberAsync(string podId, string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a member's role in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="newRole">The new role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role change result.</returns>
    Task<MembershipPublishResult> ChangeRoleAsync(string podId, string peerId, string newRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if a peer has valid membership in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<MembershipVerificationResult> VerifyMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets membership statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Membership statistics.</returns>
    Task<MembershipStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired membership records.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    Task<MembershipCleanupResult> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of retrieving a membership record.
/// </summary>
public record MembershipRetrievalResult(
    bool Found,
    string PodId,
    string PeerId,
    SignedMembershipRecord? SignedRecord,
    DateTimeOffset RetrievedAt,
    DateTimeOffset ExpiresAt,
    bool IsValidSignature,
    string? ErrorMessage = null);

/// <summary>
/// Result of membership verification.
/// </summary>
public record MembershipVerificationResult(
    bool IsValidMember,
    bool IsBanned,
    string? Role,
    string? ErrorMessage = null);

/// <summary>
/// Result of membership publish/unpublish operations.
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
/// Membership statistics.
/// </summary>
public record MembershipStats(
    int TotalMemberships,
    int ActiveMemberships,
    int BannedMemberships,
    int ExpiredMemberships,
    IReadOnlyDictionary<string, int> MembershipsByRole,
    IReadOnlyDictionary<string, int> MembershipsByPod,
    DateTimeOffset LastOperation);

/// <summary>
/// Result of pod discovery search.
/// </summary>
public record PodDiscoveryResult(
    IReadOnlyList<PodMetadata> Pods,
    string SearchType,
    string SearchTerm,
    int TotalFound,
    DateTimeOffset SearchedAt,
    string? ErrorMessage = null);

/// <summary>
/// Pod discovery statistics.
/// </summary>
public record PodDiscoveryStats(
    int TotalRegisteredPods,
    int ActiveDiscoveryEntries,
    int ExpiredEntries,
    IReadOnlyDictionary<string, int> RegistrationsByTag,
    IReadOnlyDictionary<string, int> SearchesByType,
    DateTimeOffset LastDiscoveryOperation,
    TimeSpan AverageDiscoveryTime);

/// <summary>
/// Result of membership cleanup operation.
/// </summary>
public record MembershipCleanupResult(
    int RecordsCleaned,
    int ErrorsEncountered,
    DateTimeOffset CompletedAt);

/// <summary>
/// Service for handling signed pod join and leave operations.
/// </summary>
public interface IPodJoinLeaveService
{
    /// <summary>
    /// Submits a signed join request to a pod.
    /// </summary>
    /// <param name="joinRequest">The signed join request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The join result.</returns>
    Task<PodJoinResult> RequestJoinAsync(PodJoinRequest joinRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts or rejects a pending join request.
    /// </summary>
    /// <param name="acceptance">The signed acceptance (or rejection).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acceptance result.</returns>
    Task<PodMembershipOperationResult> ProcessJoinAcceptanceAsync(PodJoinAcceptance acceptance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a signed leave request from a pod.
    /// </summary>
    /// <param name="leaveRequest">The signed leave request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The leave result.</returns>
    Task<PodLeaveResult> RequestLeaveAsync(PodLeaveRequest leaveRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts or processes a leave request.
    /// </summary>
    /// <param name="acceptance">The signed acceptance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acceptance result.</returns>
    Task<PodMembershipOperationResult> ProcessLeaveAcceptanceAsync(PodLeaveAcceptance acceptance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending join requests for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending join requests.</returns>
    Task<IReadOnlyList<PodJoinRequest>> GetPendingJoinRequestsAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending leave requests for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending leave requests.</returns>
    Task<IReadOnlyList<PodLeaveRequest>> GetPendingLeaveRequestsAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending join request.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancellation result.</returns>
    Task<bool> CancelJoinRequestAsync(string podId, string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending leave request.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancellation result.</returns>
    Task<bool> CancelLeaveRequestAsync(string podId, string peerId, CancellationToken cancellationToken = default);
}
