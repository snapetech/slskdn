// <copyright file="IPodMembershipVerifier.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for verifying pod membership and message authenticity.
/// </summary>
public interface IPodMembershipVerifier
{
    /// <summary>
    /// Verifies that a peer is a valid, non-banned member of a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<MembershipVerificationResult> VerifyMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that a pod message is authentic and from a valid member.
    /// </summary>
    /// <param name="message">The pod message to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message verification result.</returns>
    Task<MessageVerificationResult> VerifyMessageAsync(PodMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a peer has the required role or higher in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="requiredRole">The minimum required role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the peer has sufficient permissions.</returns>
    Task<bool> HasRoleAsync(string podId, string peerId, string requiredRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets membership verification statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification statistics.</returns>
    Task<VerificationStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of message verification.
/// </summary>
public record MessageVerificationResult(
    bool IsValid,
    bool IsFromValidMember,
    bool HasValidSignature,
    bool IsNotBanned,
    string? ErrorMessage = null);

/// <summary>
/// Membership verification statistics.
/// </summary>
public record VerificationStats(
    int TotalVerifications,
    int SuccessfulVerifications,
    int FailedMembershipChecks,
    int FailedSignatureChecks,
    int BannedMemberRejections,
    double AverageVerificationTimeMs,
    DateTimeOffset LastVerification);

