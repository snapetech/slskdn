// <copyright file="PodJoinLeaveService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Service for handling signed pod join and leave operations.
/// </summary>
public class PodJoinLeaveService : IPodJoinLeaveService
{
    private static readonly TimeSpan ReplayCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ILogger<PodJoinLeaveService> _logger;
    private readonly IPodService _podService;
    private readonly IPodMembershipService _membershipService;
    private readonly IPodMembershipVerifier _membershipVerifier;
    private readonly IOptionsMonitor<PodJoinOptions> _joinOptions;

    // In-memory storage for pending requests (in production, this would be persisted)
    private readonly ConcurrentDictionary<string, ConcurrentBag<PodJoinRequest>> _pendingJoinRequests = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<PodLeaveRequest>> _pendingLeaveRequests = new();
    // 6.4: replay protection when SignatureMode is Enforce. Key: PodId:PeerId:Nonce, Value: expiry.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _joinReplayCache = new();

    public PodJoinLeaveService(
        ILogger<PodJoinLeaveService> logger,
        IPodService podService,
        IPodMembershipService membershipService,
        IPodMembershipVerifier membershipVerifier,
        IOptionsMonitor<PodJoinOptions> joinOptions)
    {
        _logger = logger;
        _podService = podService;
        _membershipService = membershipService;
        _membershipVerifier = membershipVerifier;
        _joinOptions = joinOptions;
    }

    /// <inheritdoc/>
    public async Task<PodJoinResult> RequestJoinAsync(PodJoinRequest joinRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[PodJoinLeave] Processing join request for {PeerId} to join {PodId}", joinRequest.PeerId, joinRequest.PodId);

            // 0. 6.4: When SignatureMode is Enforce, require Nonce and enforce replay protection
            var mode = _joinOptions.CurrentValue.SignatureMode;
            if (mode == SignatureMode.Enforce)
            {
                if (string.IsNullOrWhiteSpace(joinRequest.Nonce))
                {
                    return new PodJoinResult(
                        Success: false,
                        PodId: joinRequest.PodId,
                        PeerId: joinRequest.PeerId,
                        ErrorMessage: "Nonce is required when PodCore.Join.SignatureMode is Enforce");
                }
                var replayKey = $"{joinRequest.PodId}:{joinRequest.PeerId}:{joinRequest.Nonce}";
                EvictExpiredJoinReplayEntries();
                if (!_joinReplayCache.TryAdd(replayKey, DateTimeOffset.UtcNow + ReplayCacheTtl))
                {
                    _logger.LogWarning("[PodJoinLeave] Replay rejected: duplicate Nonce for {PeerId} in {PodId}", joinRequest.PeerId, joinRequest.PodId);
                    return new PodJoinResult(
                        Success: false,
                        PodId: joinRequest.PodId,
                        PeerId: joinRequest.PeerId,
                        ErrorMessage: "Replay detected: nonce already used");
                }
            }

            // 1. Verify the join request signature
            if (!await VerifyJoinRequestSignatureAsync(joinRequest, cancellationToken))
            {
                _logger.LogWarning("[PodJoinLeave] Invalid signature on join request from {PeerId}", joinRequest.PeerId);
                return new PodJoinResult(
                    Success: false,
                    PodId: joinRequest.PodId,
                    PeerId: joinRequest.PeerId,
                    ErrorMessage: "Invalid join request signature");
            }

            // 2. Check if pod exists
            var pod = await _podService.GetPodAsync(joinRequest.PodId, cancellationToken);
            if (pod == null)
            {
                return new PodJoinResult(
                    Success: false,
                    PodId: joinRequest.PodId,
                    PeerId: joinRequest.PeerId,
                    ErrorMessage: "Pod not found");
            }

            // 3. Check if already a member
            var members = await _podService.GetMembersAsync(joinRequest.PodId, cancellationToken);
            if (members.Any(m => m.PeerId == joinRequest.PeerId))
            {
                return new PodJoinResult(
                    Success: false,
                    PodId: joinRequest.PodId,
                    PeerId: joinRequest.PeerId,
                    ErrorMessage: "Already a member of this pod");
            }

            // 4. Check if already has pending request
            var pendingRequests = _pendingJoinRequests.GetOrAdd(joinRequest.PodId, _ => new ConcurrentBag<PodJoinRequest>());
            if (pendingRequests.Any(r => r.PeerId == joinRequest.PeerId))
            {
                return new PodJoinResult(
                    Success: false,
                    PodId: joinRequest.PodId,
                    PeerId: joinRequest.PeerId,
                    ErrorMessage: "Pending join request already exists");
            }

            // 5. Store the pending request
            pendingRequests.Add(joinRequest);

            _logger.LogInformation("[PodJoinLeave] Join request from {PeerId} to {PodId} is now pending", joinRequest.PeerId, joinRequest.PodId);

            return new PodJoinResult(
                Success: true,
                PodId: joinRequest.PodId,
                PeerId: joinRequest.PeerId,
                JoinRequest: joinRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing join request from {PeerId} to {PodId}", joinRequest.PeerId, joinRequest.PodId);
            return new PodJoinResult(
                Success: false,
                PodId: joinRequest.PodId,
                PeerId: joinRequest.PeerId,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodMembershipOperationResult> ProcessJoinAcceptanceAsync(PodJoinAcceptance acceptance, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[PodJoinLeave] Processing join acceptance for {PeerId} in {PodId} by {Acceptor}", acceptance.PeerId, acceptance.PodId, acceptance.AcceptorPeerId);

            // 1. Verify the acceptance signature
            if (!await VerifyAcceptanceSignatureAsync(acceptance, cancellationToken))
            {
                _logger.LogWarning("[PodJoinLeave] Invalid signature on join acceptance from {Acceptor}", acceptance.AcceptorPeerId);
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "join_acceptance",
                    ErrorMessage: "Invalid acceptance signature");
            }

            // 2. Verify acceptor has permission (owner or mod)
            var hasPermission = await _membershipVerifier.HasRoleAsync(acceptance.PodId, acceptance.AcceptorPeerId, PodRoles.Owner, cancellationToken) ||
                               await _membershipVerifier.HasRoleAsync(acceptance.PodId, acceptance.AcceptorPeerId, PodRoles.Moderator, cancellationToken);
            if (!hasPermission)
            {
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "join_acceptance",
                    ErrorMessage: "Acceptor does not have permission to accept join requests");
            }

            // 3. Find and remove the pending request
            var pendingRequests = _pendingJoinRequests.GetOrAdd(acceptance.PodId, _ => new ConcurrentBag<PodJoinRequest>());
            var request = pendingRequests.FirstOrDefault(r => r.PeerId == acceptance.PeerId);
            if (request == null)
            {
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "join_acceptance",
                    ErrorMessage: "No pending join request found");
            }

            // Remove from pending (simplified - in production would be more robust)
            var newRequests = new ConcurrentBag<PodJoinRequest>();
            foreach (var r in pendingRequests.Where(r => r.PeerId != acceptance.PeerId))
            {
                newRequests.Add(r);
            }
            _pendingJoinRequests[acceptance.PodId] = newRequests;

            // 4. Add member to pod
            var newMember = new PodMember
            {
                PeerId = acceptance.PeerId,
                Role = acceptance.AcceptedRole,
                IsBanned = false,
                PublicKey = request.PublicKey
            };

            var joinResult = await _podService.JoinAsync(acceptance.PodId, newMember, cancellationToken);
            if (!joinResult)
            {
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "join_acceptance",
                    ErrorMessage: "Failed to add member to pod");
            }

            // 5. Publish signed membership record
            var membershipResult = await _membershipService.PublishMembershipAsync(acceptance.PodId, newMember, cancellationToken);
            if (!membershipResult.Success)
            {
                _logger.LogWarning("[PodJoinLeave] Failed to publish membership record for {PeerId} in {PodId}", acceptance.PeerId, acceptance.PodId);
                // Note: Member was added but DHT publication failed - this is not a fatal error
            }

            _logger.LogInformation("[PodJoinLeave] Successfully accepted {PeerId} as {Role} in {PodId}", acceptance.PeerId, acceptance.AcceptedRole, acceptance.PodId);

            return new PodMembershipOperationResult(
                Success: true,
                PodId: acceptance.PodId,
                PeerId: acceptance.PeerId,
                Operation: "join_acceptance",
                Request: request,
                Response: acceptance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing join acceptance for {PeerId} in {PodId}", acceptance.PeerId, acceptance.PodId);
            return new PodMembershipOperationResult(
                Success: false,
                PodId: acceptance.PodId,
                PeerId: acceptance.PeerId,
                Operation: "join_acceptance",
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodLeaveResult> RequestLeaveAsync(PodLeaveRequest leaveRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[PodJoinLeave] Processing leave request from {PeerId} leaving {PodId}", leaveRequest.PeerId, leaveRequest.PodId);

            // 1. Verify the leave request signature
            if (!await VerifyLeaveRequestSignatureAsync(leaveRequest, cancellationToken))
            {
                _logger.LogWarning("[PodJoinLeave] Invalid signature on leave request from {PeerId}", leaveRequest.PeerId);
                return new PodLeaveResult(
                    Success: false,
                    PodId: leaveRequest.PodId,
                    PeerId: leaveRequest.PeerId,
                    ErrorMessage: "Invalid leave request signature");
            }

            // 2. Check if actually a member
            var members = await _podService.GetMembersAsync(leaveRequest.PodId, cancellationToken);
            if (!members.Any(m => m.PeerId == leaveRequest.PeerId))
            {
                return new PodLeaveResult(
                    Success: false,
                    PodId: leaveRequest.PodId,
                    PeerId: leaveRequest.PeerId,
                    ErrorMessage: "Not a member of this pod");
            }

            // 3. For regular members, process immediately
            // For owners/mods, require acceptance
            var member = members.First(m => m.PeerId == leaveRequest.PeerId);
            if (member.Role == PodRoles.Member)
            {
                // Regular member can leave immediately
                var leaveResult = await _podService.LeaveAsync(leaveRequest.PodId, leaveRequest.PeerId, cancellationToken);
                if (!leaveResult)
                {
                    return new PodLeaveResult(
                        Success: false,
                        PodId: leaveRequest.PodId,
                        PeerId: leaveRequest.PeerId,
                        ErrorMessage: "Failed to remove member from pod");
                }

                // Remove membership record from DHT
                await _membershipService.RemoveMembershipAsync(leaveRequest.PodId, leaveRequest.PeerId, cancellationToken);

                _logger.LogInformation("[PodJoinLeave] {PeerId} left {PodId} successfully", leaveRequest.PeerId, leaveRequest.PodId);

                return new PodLeaveResult(
                    Success: true,
                    PodId: leaveRequest.PodId,
                    PeerId: leaveRequest.PeerId,
                    LeaveRequest: leaveRequest);
            }
            else
            {
                // Owner/mod leave requires acceptance - store as pending
                var pendingRequests = _pendingLeaveRequests.GetOrAdd(leaveRequest.PodId, _ => new ConcurrentBag<PodLeaveRequest>());
                if (pendingRequests.Any(r => r.PeerId == leaveRequest.PeerId))
                {
                    return new PodLeaveResult(
                        Success: false,
                        PodId: leaveRequest.PodId,
                        PeerId: leaveRequest.PeerId,
                        ErrorMessage: "Pending leave request already exists");
                }

                pendingRequests.Add(leaveRequest);

                _logger.LogInformation("[PodJoinLeave] Leave request from {Role} {PeerId} in {PodId} is now pending", member.Role, leaveRequest.PeerId, leaveRequest.PodId);

                return new PodLeaveResult(
                    Success: true,
                    PodId: leaveRequest.PodId,
                    PeerId: leaveRequest.PeerId,
                    LeaveRequest: leaveRequest);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing leave request from {PeerId} in {PodId}", leaveRequest.PeerId, leaveRequest.PodId);
            return new PodLeaveResult(
                Success: false,
                PodId: leaveRequest.PodId,
                PeerId: leaveRequest.PeerId,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodMembershipOperationResult> ProcessLeaveAcceptanceAsync(PodLeaveAcceptance acceptance, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[PodJoinLeave] Processing leave acceptance for {PeerId} in {PodId} by {Acceptor}", acceptance.PeerId, acceptance.PodId, acceptance.AcceptorPeerId);

            // 1. Verify the acceptance signature
            if (!await VerifyLeaveAcceptanceSignatureAsync(acceptance, cancellationToken))
            {
                _logger.LogWarning("[PodJoinLeave] Invalid signature on leave acceptance from {Acceptor}", acceptance.AcceptorPeerId);
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "leave_acceptance",
                    ErrorMessage: "Invalid acceptance signature");
            }

            // 2. Verify acceptor has permission
            var hasPermission = await _membershipVerifier.HasRoleAsync(acceptance.PodId, acceptance.AcceptorPeerId, PodRoles.Owner, cancellationToken) ||
                               await _membershipVerifier.HasRoleAsync(acceptance.PodId, acceptance.AcceptorPeerId, PodRoles.Moderator, cancellationToken);
            if (!hasPermission)
            {
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "leave_acceptance",
                    ErrorMessage: "Acceptor does not have permission to accept leave requests");
            }

            // 3. Find and remove the pending request
            var pendingRequests = _pendingLeaveRequests.GetOrAdd(acceptance.PodId, _ => new ConcurrentBag<PodLeaveRequest>());
            var request = pendingRequests.FirstOrDefault(r => r.PeerId == acceptance.PeerId);
            if (request == null)
            {
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "leave_acceptance",
                    ErrorMessage: "No pending leave request found");
            }

            // Remove from pending (simplified)
            var newRequests = new ConcurrentBag<PodLeaveRequest>();
            foreach (var r in pendingRequests.Where(r => r.PeerId != acceptance.PeerId))
            {
                newRequests.Add(r);
            }
            _pendingLeaveRequests[acceptance.PodId] = newRequests;

            // 4. Remove member from pod
            var leaveResult = await _podService.LeaveAsync(acceptance.PodId, acceptance.PeerId, cancellationToken);
            if (!leaveResult)
            {
                return new PodMembershipOperationResult(
                    Success: false,
                    PodId: acceptance.PodId,
                    PeerId: acceptance.PeerId,
                    Operation: "leave_acceptance",
                    ErrorMessage: "Failed to remove member from pod");
            }

            // 5. Remove membership record from DHT
            await _membershipService.RemoveMembershipAsync(acceptance.PodId, acceptance.PeerId, cancellationToken);

            _logger.LogInformation("[PodJoinLeave] Successfully processed leave of {PeerId} from {PodId}", acceptance.PeerId, acceptance.PodId);

            return new PodMembershipOperationResult(
                Success: true,
                PodId: acceptance.PodId,
                PeerId: acceptance.PeerId,
                Operation: "leave_acceptance",
                Request: request,
                Response: acceptance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing leave acceptance for {PeerId} in {PodId}", acceptance.PeerId, acceptance.PodId);
            return new PodMembershipOperationResult(
                Success: false,
                PodId: acceptance.PodId,
                PeerId: acceptance.PeerId,
                Operation: "leave_acceptance",
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PodJoinRequest>> GetPendingJoinRequestsAsync(string podId, CancellationToken cancellationToken = default)
    {
        var pendingRequests = _pendingJoinRequests.GetOrAdd(podId, _ => new ConcurrentBag<PodJoinRequest>());
        return Task.FromResult<IReadOnlyList<PodJoinRequest>>(pendingRequests.ToList());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PodLeaveRequest>> GetPendingLeaveRequestsAsync(string podId, CancellationToken cancellationToken = default)
    {
        var pendingRequests = _pendingLeaveRequests.GetOrAdd(podId, _ => new ConcurrentBag<PodLeaveRequest>());
        return Task.FromResult<IReadOnlyList<PodLeaveRequest>>(pendingRequests.ToList());
    }

    /// <inheritdoc/>
    public Task<bool> CancelJoinRequestAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        var pendingRequests = _pendingJoinRequests.GetOrAdd(podId, _ => new ConcurrentBag<PodJoinRequest>());
        var newRequests = new ConcurrentBag<PodJoinRequest>();
        var found = false;

        foreach (var request in pendingRequests)
        {
            if (request.PeerId != peerId)
            {
                newRequests.Add(request);
            }
            else
            {
                found = true;
            }
        }

        if (found)
        {
            _pendingJoinRequests[podId] = newRequests;
            _logger.LogInformation("[PodJoinLeave] Cancelled join request from {PeerId} for {PodId}", peerId, podId);
        }

        return Task.FromResult(found);
    }

    /// <inheritdoc/>
    public Task<bool> CancelLeaveRequestAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        var pendingRequests = _pendingLeaveRequests.GetOrAdd(podId, _ => new ConcurrentBag<PodLeaveRequest>());
        var newRequests = new ConcurrentBag<PodLeaveRequest>();
        var found = false;

        foreach (var request in pendingRequests)
        {
            if (request.PeerId != peerId)
            {
                newRequests.Add(request);
            }
            else
            {
                found = true;
            }
        }

        if (found)
        {
            _pendingLeaveRequests[podId] = newRequests;
            _logger.LogInformation("[PodJoinLeave] Cancelled leave request from {PeerId} for {PodId}", peerId, podId);
        }

        return Task.FromResult(found);
    }

    private void EvictExpiredJoinReplayEntries()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var k in _joinReplayCache.Where(e => e.Value < now).Select(e => e.Key).ToList())
            _joinReplayCache.TryRemove(k, out _);
    }

    // Helper methods for signature verification
    private async Task<bool> VerifyJoinRequestSignatureAsync(PodJoinRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Create the message to verify (without signature)
            var messageData = $"{request.PodId}:{request.PeerId}:{request.RequestedRole}:{request.TimestampUnixMs}";
            if (!string.IsNullOrEmpty(request.Message))
            {
                messageData += $":{request.Message}";
            }

            // For now, assume signature verification - in production this would use proper crypto
            return !string.IsNullOrEmpty(request.Signature) && request.Signature.Length > 10;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PodJoinLeave] Error verifying join request signature");
            return false;
        }
    }

    private async Task<bool> VerifyAcceptanceSignatureAsync(PodJoinAcceptance acceptance, CancellationToken cancellationToken)
    {
        try
        {
            // Create the message to verify
            var messageData = $"{acceptance.PodId}:{acceptance.PeerId}:{acceptance.AcceptedRole}:{acceptance.TimestampUnixMs}";
            if (!string.IsNullOrEmpty(acceptance.Message))
            {
                messageData += $":{acceptance.Message}";
            }

            // For now, assume signature verification
            return !string.IsNullOrEmpty(acceptance.Signature) && acceptance.Signature.Length > 10;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PodJoinLeave] Error verifying acceptance signature");
            return false;
        }
    }

    private async Task<bool> VerifyLeaveRequestSignatureAsync(PodLeaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var messageData = $"{request.PodId}:{request.PeerId}:{request.TimestampUnixMs}";
            if (!string.IsNullOrEmpty(request.Message))
            {
                messageData += $":{request.Message}";
            }

            // For now, assume signature verification
            return !string.IsNullOrEmpty(request.Signature) && request.Signature.Length > 10;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PodJoinLeave] Error verifying leave request signature");
            return false;
        }
    }

    private async Task<bool> VerifyLeaveAcceptanceSignatureAsync(PodLeaveAcceptance acceptance, CancellationToken cancellationToken)
    {
        try
        {
            var messageData = $"{acceptance.PodId}:{acceptance.PeerId}:{acceptance.TimestampUnixMs}";
            if (!string.IsNullOrEmpty(acceptance.Message))
            {
                messageData += $":{acceptance.Message}";
            }

            // For now, assume signature verification
            return !string.IsNullOrEmpty(acceptance.Signature) && acceptance.Signature.Length > 10;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PodJoinLeave] Error verifying leave acceptance signature");
            return false;
        }
    }
}
