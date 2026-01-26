// <copyright file="PodJoinLeave.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

/// <summary>
/// A signed request to join a pod.
/// </summary>
public record PodJoinRequest(
    string PodId,
    string PeerId,
    string RequestedRole,
    string PublicKey,
    long TimestampUnixMs,
    string Signature,
    string? Message = null,
    string? Nonce = null);

/// <summary>
/// A signed acceptance of a join request.
/// </summary>
public record PodJoinAcceptance(
    string PodId,
    string PeerId,
    string AcceptedRole,
    string AcceptorPeerId,
    string AcceptorPublicKey,
    long TimestampUnixMs,
    string Signature,
    string? Message = null);

/// <summary>
/// A signed request to leave a pod.
/// </summary>
public record PodLeaveRequest(
    string PodId,
    string PeerId,
    string PublicKey,
    long TimestampUnixMs,
    string Signature,
    string? Message = null);

/// <summary>
/// A signed acceptance of a leave request.
/// </summary>
public record PodLeaveAcceptance(
    string PodId,
    string PeerId,
    string AcceptorPeerId,
    string AcceptorPublicKey,
    long TimestampUnixMs,
    string Signature,
    string? Message = null);

/// <summary>
/// Result of a join request operation.
/// </summary>
public record PodJoinResult(
    bool Success,
    string PodId,
    string PeerId,
    string? ErrorMessage = null,
    PodJoinRequest? JoinRequest = null,
    PodJoinAcceptance? Acceptance = null);

/// <summary>
/// Result of a leave request operation.
/// </summary>
public record PodLeaveResult(
    bool Success,
    string PodId,
    string PeerId,
    string? ErrorMessage = null,
    PodLeaveRequest? LeaveRequest = null,
    PodLeaveAcceptance? Acceptance = null);

/// <summary>
/// Result of a join/leave operation.
/// </summary>
public record PodMembershipOperationResult(
    bool Success,
    string PodId,
    string PeerId,
    string Operation,
    string? ErrorMessage = null,
    object? Request = null,
    object? Response = null);

