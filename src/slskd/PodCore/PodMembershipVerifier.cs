// <copyright file="PodMembershipVerifier.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for verifying pod membership and message authenticity.
/// </summary>
public class PodMembershipVerifier : IPodMembershipVerifier
{
    private readonly ILogger<PodMembershipVerifier> _logger;
    private readonly IPodMembershipService _membershipService;

    // Statistics tracking
    private int _totalVerifications;
    private int _successfulVerifications;
    private int _failedMembershipChecks;
    private int _failedSignatureChecks;
    private int _bannedMemberRejections;
    private long _totalVerificationTimeMs;
    private DateTimeOffset _lastVerification = DateTimeOffset.MinValue;

    public PodMembershipVerifier(
        ILogger<PodMembershipVerifier> logger,
        IPodMembershipService membershipService)
    {
        _logger = logger;
        _membershipService = membershipService;
    }

    /// <inheritdoc/>
    public async Task<MembershipVerificationResult> VerifyMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var result = await _membershipService.VerifyMembershipAsync(podId, peerId, cancellationToken);

            var verificationTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            Interlocked.Add(ref _totalVerificationTimeMs, (long)verificationTime);
            Interlocked.Increment(ref _totalVerifications);
            _lastVerification = DateTimeOffset.UtcNow;

            if (result.IsValidMember && !result.IsBanned)
            {
                Interlocked.Increment(ref _successfulVerifications);
            }
            else if (result.IsBanned)
            {
                Interlocked.Increment(ref _bannedMemberRejections);
            }
            else
            {
                Interlocked.Increment(ref _failedMembershipChecks);
            }

            return result;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedMembershipChecks);
            _logger.LogError(ex, "[PodMembershipVerifier] Error verifying membership for {PeerId} in {PodId}", peerId, podId);
            return new MembershipVerificationResult(
                IsValidMember: false,
                IsBanned: false,
                Role: null,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<MessageVerificationResult> VerifyMessageAsync(PodMessage message, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Extract podId from channelId (format: "podId:channelId")
            var channelParts = message.ChannelId.Split(':', 2);
            if (channelParts.Length != 2)
            {
                return new MessageVerificationResult(
                    IsValid: false,
                    IsFromValidMember: false,
                    HasValidSignature: false,
                    IsNotBanned: false,
                    ErrorMessage: "Invalid channel ID format");
            }

            var podId = channelParts[0];

            // 1. Verify membership
            var membershipResult = await VerifyMembershipAsync(podId, message.SenderPeerId, cancellationToken);
            var isFromValidMember = membershipResult.IsValidMember;
            var isNotBanned = !membershipResult.IsBanned;

            // 2. Verify message signature
            var signatureValid = await VerifyMessageSignatureAsync(message, cancellationToken);
            var hasValidSignature = signatureValid;

            // 3. Overall validation
            var isValid = isFromValidMember && isNotBanned && hasValidSignature;

            var verificationTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            Interlocked.Add(ref _totalVerificationTimeMs, (long)verificationTime);

            if (!isFromValidMember || !isNotBanned)
            {
                Interlocked.Increment(ref _failedMembershipChecks);
            }

            if (!hasValidSignature)
            {
                Interlocked.Increment(ref _failedSignatureChecks);
            }

            if (isValid)
            {
                Interlocked.Increment(ref _successfulVerifications);
            }

            _logger.LogDebug(
                "[PodMembershipVerifier] Message {MessageId} verification: valid={IsValid}, member={IsMember}, notBanned={NotBanned}, signature={SignatureValid}",
                message.MessageId, isValid, isFromValidMember, isNotBanned, hasValidSignature);

            return new MessageVerificationResult(
                IsValid: isValid,
                IsFromValidMember: isFromValidMember,
                HasValidSignature: hasValidSignature,
                IsNotBanned: isNotBanned);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembershipVerifier] Error verifying message {MessageId}", message.MessageId);
            return new MessageVerificationResult(
                IsValid: false,
                IsFromValidMember: false,
                HasValidSignature: false,
                IsNotBanned: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasRoleAsync(string podId, string peerId, string requiredRole, CancellationToken cancellationToken = default)
    {
        try
        {
            var verification = await VerifyMembershipAsync(podId, peerId, cancellationToken);

            if (!verification.IsValidMember || verification.IsBanned)
            {
                return false;
            }

            var userRole = verification.Role;
            if (string.IsNullOrEmpty(userRole))
            {
                return false;
            }

            // Role hierarchy: owner > mod > member
            var roleHierarchy = new Dictionary<string, int>
            {
                [PodRoles.Member] = 1,
                [PodRoles.Moderator] = 2,
                [PodRoles.Owner] = 3
            };

            return roleHierarchy.TryGetValue(userRole, out var userLevel) &&
                   roleHierarchy.TryGetValue(requiredRole, out var requiredLevel) &&
                   userLevel >= requiredLevel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembershipVerifier] Error checking role for {PeerId} in {PodId}", peerId, podId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<VerificationStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalVerifications = _totalVerifications;
        var averageTime = totalVerifications > 0 ? _totalVerificationTimeMs / (double)totalVerifications : 0.0;

        return new VerificationStats(
            TotalVerifications: totalVerifications,
            SuccessfulVerifications: _successfulVerifications,
            FailedMembershipChecks: _failedMembershipChecks,
            FailedSignatureChecks: _failedSignatureChecks,
            BannedMemberRejections: _bannedMemberRejections,
            AverageVerificationTimeMs: averageTime,
            LastVerification: _lastVerification);
    }

    // Helper methods
    private Task<bool> VerifyMessageSignatureAsync(PodMessage message, CancellationToken cancellationToken)
    {
        // TODO: Implement proper message signature verification
        // For now, assume signature is valid if present
        var hasSignature = !string.IsNullOrEmpty(message.Signature);
        if (!hasSignature)
        {
            _logger.LogWarning("[PodMembershipVerifier] Message {MessageId} has no signature", message.MessageId);
        }
        return Task.FromResult(hasSignature);
    }
}
