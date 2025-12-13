// <copyright file="PodMembershipService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;

/// <summary>
/// Service for managing signed pod membership records in DHT.
/// </summary>
public class PodMembershipService : IPodMembershipService
{
    private readonly ILogger<PodMembershipService> _logger;
    private readonly IMeshDhtClient _dhtClient;
    private readonly IControlSigner _signer;

    // Statistics tracking
    private readonly ConcurrentDictionary<string, MembershipInfo> _activeMemberships = new();
    private readonly ConcurrentDictionary<string, int> _membershipsByRole = new();
    private readonly ConcurrentDictionary<string, int> _membershipsByPod = new();

    private int _totalMemberships;
    private int _activeMembershipsCount;
    private int _bannedMemberships;
    private int _expiredMemberships;
    private DateTimeOffset _lastOperation = DateTimeOffset.MinValue;

    public PodMembershipService(
        ILogger<PodMembershipService> logger,
        IMeshDhtClient dhtClient,
        IControlSigner signer)
    {
        _logger = logger;
        _dhtClient = dhtClient;
        _signer = signer;
    }

    /// <inheritdoc/>
    public async Task<MembershipPublishResult> PublishMembershipAsync(string podId, PodMember member, CancellationToken cancellationToken = default)
    {
        var dhtKey = GetMembershipDhtKey(podId, member.PeerId);
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("[PodMembership] Publishing membership for peer {PeerId} in pod {PodId}", member.PeerId, podId);

            // Create signed membership record
            var signedRecord = CreateSignedMembershipRecord(podId, member, "join");

            // Publish to DHT with 24-hour TTL
            await _dhtClient.PutAsync(dhtKey, signedRecord, ttlSeconds: 24 * 60 * 60, cancellationToken);

            var publishedAt = DateTimeOffset.UtcNow;
            var expiresAt = publishedAt.AddHours(24);

            // Track membership
            var membershipInfo = new MembershipInfo(
                PodId: podId,
                PeerId: member.PeerId,
                Role: member.Role,
                IsBanned: member.IsBanned,
                PublishedAt: publishedAt,
                ExpiresAt: expiresAt,
                Signature: signedRecord.Signature);

            var membershipKey = $"{podId}:{member.PeerId}";
            _activeMemberships[membershipKey] = membershipInfo;

            // Update statistics
            Interlocked.Increment(ref _totalMemberships);
            Interlocked.Increment(ref _activeMembershipsCount);
            _membershipsByRole.AddOrUpdate(member.Role, 1, (_, count) => count + 1);
            _membershipsByPod.AddOrUpdate(podId, 1, (_, count) => count + 1);
            _lastOperation = publishedAt;

            if (member.IsBanned)
            {
                Interlocked.Increment(ref _bannedMemberships);
            }

            _logger.LogInformation(
                "[PodMembership] Published membership for {PeerId} in {PodId}, role: {Role}, expires: {ExpiresAt}",
                member.PeerId, podId, member.Role, expiresAt);

            return new MembershipPublishResult(
                Success: true,
                PodId: podId,
                PeerId: member.PeerId,
                DhtKey: dhtKey,
                PublishedAt: publishedAt,
                ExpiresAt: expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error publishing membership for {PeerId} in {PodId}", member.PeerId, podId);
            return new MembershipPublishResult(
                Success: false,
                PodId: podId,
                PeerId: member.PeerId,
                DhtKey: dhtKey,
                PublishedAt: startTime,
                ExpiresAt: startTime,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<MembershipPublishResult> UpdateMembershipAsync(string podId, PodMember member, CancellationToken cancellationToken = default)
    {
        // For updates, we simply publish the new record (DHT will overwrite)
        return await PublishMembershipAsync(podId, member, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MembershipPublishResult> RemoveMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        var dhtKey = GetMembershipDhtKey(podId, peerId);
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("[PodMembership] Removing membership for peer {PeerId} from pod {PodId}", peerId, podId);

            // Note: DHT typically doesn't support explicit deletion
            // We'll publish a null value to effectively remove it
            await _dhtClient.PutAsync(dhtKey, null, ttlSeconds: 300, cancellationToken); // Short TTL for removal

            // Remove from local tracking
            var membershipKey = $"{podId}:{peerId}";
            if (_activeMemberships.TryRemove(membershipKey, out var membershipInfo))
            {
                Interlocked.Decrement(ref _activeMembershipsCount);
                Interlocked.Increment(ref _expiredMemberships);

                // Update role and pod counters
                _membershipsByRole.AddOrUpdate(membershipInfo.Role, 0, (_, count) => Math.Max(0, count - 1));
                _membershipsByPod.AddOrUpdate(podId, 0, (_, count) => Math.Max(0, count - 1));

                if (membershipInfo.IsBanned)
                {
                    Interlocked.Decrement(ref _bannedMemberships);
                }
            }

            _lastOperation = DateTimeOffset.UtcNow;

            _logger.LogInformation("[PodMembership] Removed membership for {PeerId} from {PodId}", peerId, podId);

            return new MembershipPublishResult(
                Success: true,
                PodId: podId,
                PeerId: peerId,
                DhtKey: dhtKey,
                PublishedAt: startTime,
                ExpiresAt: startTime.AddSeconds(300));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error removing membership for {PeerId} from {PodId}", peerId, podId);
            return new MembershipPublishResult(
                Success: false,
                PodId: podId,
                PeerId: peerId,
                DhtKey: dhtKey,
                PublishedAt: startTime,
                ExpiresAt: startTime,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<MembershipRetrievalResult> GetMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        var dhtKey = GetMembershipDhtKey(podId, peerId);

        try
        {
            _logger.LogDebug("[PodMembership] Retrieving membership for peer {PeerId} in pod {PodId}", peerId, podId);

            var signedRecord = await _dhtClient.GetAsync<SignedMembershipRecord>(dhtKey, cancellationToken);

            if (signedRecord == null)
            {
                return new MembershipRetrievalResult(
                    Found: false,
                    PodId: podId,
                    PeerId: peerId,
                    SignedRecord: null,
                    RetrievedAt: DateTimeOffset.UtcNow,
                    ExpiresAt: DateTimeOffset.MinValue,
                    IsValidSignature: false,
                    ErrorMessage: "Membership record not found");
            }

            var retrievedAt = DateTimeOffset.UtcNow;

            // Verify signature
            var isValidSignature = VerifyMembershipSignature(signedRecord);

            // Calculate expiration (assuming 24 hour TTL from signed timestamp)
            var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(signedRecord.TimestampUnixMs).AddHours(24);

            _logger.LogDebug(
                "[PodMembership] Retrieved membership for {PeerId} in {PodId}, signature valid: {IsValid}",
                peerId, podId, isValidSignature);

            return new MembershipRetrievalResult(
                Found: true,
                PodId: podId,
                PeerId: peerId,
                SignedRecord: signedRecord,
                RetrievedAt: retrievedAt,
                ExpiresAt: expiresAt,
                IsValidSignature: isValidSignature);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error retrieving membership for {PeerId} in {PodId}", peerId, podId);
            return new MembershipRetrievalResult(
                Found: false,
                PodId: podId,
                PeerId: peerId,
                SignedRecord: null,
                RetrievedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.MinValue,
                IsValidSignature: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MembershipRetrievalResult>> ListPodMembershipsAsync(string podId, CancellationToken cancellationToken = default)
    {
        // Note: DHT doesn't support efficient listing of all records for a pod
        // In a real implementation, this would require maintaining an index or
        // using a different approach. For now, return empty list.
        _logger.LogWarning("[PodMembership] ListPodMembershipsAsync not efficiently supported by DHT - requires index");
        return Array.Empty<MembershipRetrievalResult>();
    }

    /// <inheritdoc/>
    public async Task<MembershipPublishResult> BanMemberAsync(string podId, string peerId, string? reason, CancellationToken cancellationToken = default)
    {
        // Get current membership
        var currentResult = await GetMembershipAsync(podId, peerId, cancellationToken);
        if (!currentResult.Found || currentResult.SignedRecord == null)
        {
            return new MembershipPublishResult(
                Success: false,
                PodId: podId,
                PeerId: peerId,
                DhtKey: GetMembershipDhtKey(podId, peerId),
                PublishedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.MinValue,
                ErrorMessage: "Member not found");
        }

        // Create banned member record
        var bannedMember = new PodMember
        {
            PeerId = peerId,
            Role = currentResult.SignedRecord?.Role ?? "member",
            IsBanned = true,
            PublicKey = currentResult.SignedRecord?.PublicKey
        };

        return await UpdateMembershipAsync(podId, bannedMember, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MembershipPublishResult> UnbanMemberAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        // Get current membership
        var currentResult = await GetMembershipAsync(podId, peerId, cancellationToken);
        if (!currentResult.Found || currentResult.SignedRecord == null)
        {
            return new MembershipPublishResult(
                Success: false,
                PodId: podId,
                PeerId: peerId,
                DhtKey: GetMembershipDhtKey(podId, peerId),
                PublishedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.MinValue,
                ErrorMessage: "Member not found");
        }

        // Create unbanned member record
        var unbannedMember = new PodMember
        {
            PeerId = peerId,
            Role = currentResult.SignedRecord?.Role ?? "member",
            IsBanned = false,
            PublicKey = currentResult.SignedRecord?.PublicKey
        };

        return await UpdateMembershipAsync(podId, unbannedMember, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MembershipPublishResult> ChangeRoleAsync(string podId, string peerId, string newRole, CancellationToken cancellationToken = default)
    {
        // Get current membership
        var currentResult = await GetMembershipAsync(podId, peerId, cancellationToken);
        if (!currentResult.Found || currentResult.SignedRecord == null)
        {
            return new MembershipPublishResult(
                Success: false,
                PodId: podId,
                PeerId: peerId,
                DhtKey: GetMembershipDhtKey(podId, peerId),
                PublishedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.MinValue,
                ErrorMessage: "Member not found");
        }

        // Update role counters
        var oldRole = currentResult.SignedRecord.Role;
        _membershipsByRole.AddOrUpdate(oldRole, 0, (_, count) => Math.Max(0, count - 1));
        _membershipsByRole.AddOrUpdate(newRole, 1, (_, count) => count + 1);

        // Create updated member record
        var updatedMember = new PodMember
        {
            PeerId = peerId,
            Role = newRole,
            IsBanned = currentResult.SignedRecord != null && currentResult.SignedRecord.Action == "ban",
            PublicKey = currentResult.SignedRecord?.PublicKey
        };

        return await UpdateMembershipAsync(podId, updatedMember, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MembershipVerificationResult> VerifyMembershipAsync(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        var result = await GetMembershipAsync(podId, peerId, cancellationToken);

        if (!result.Found || result.SignedRecord == null)
        {
            return new MembershipVerificationResult(
                IsValidMember: false,
                IsBanned: false,
                Role: null,
                ErrorMessage: "Membership not found");
        }

        if (!result.IsValidSignature)
        {
            return new MembershipVerificationResult(
                IsValidMember: false,
                IsBanned: false,
                Role: null,
                ErrorMessage: "Invalid signature");
        }

        return new MembershipVerificationResult(
            IsValidMember: true,
            IsBanned: result.SignedRecord.Action == "ban",
            Role: result.SignedRecord.Role);
    }

    /// <inheritdoc/>
    public async Task<MembershipStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        // Clean up expired memberships
        var expired = _activeMemberships.Where(kvp => kvp.Value.ExpiresAt < DateTimeOffset.UtcNow)
                                       .Select(kvp => kvp.Key)
                                       .ToList();

        foreach (var membershipKey in expired)
        {
            if (_activeMemberships.TryRemove(membershipKey, out var membershipInfo))
            {
                Interlocked.Decrement(ref _activeMembershipsCount);
                Interlocked.Increment(ref _expiredMemberships);

                _membershipsByRole.AddOrUpdate(membershipInfo.Role, 0, (_, count) => Math.Max(0, count - 1));
                _membershipsByPod.AddOrUpdate(membershipInfo.PodId, 0, (_, count) => Math.Max(0, count - 1));

                if (membershipInfo.IsBanned)
                {
                    Interlocked.Decrement(ref _bannedMemberships);
                }
            }
        }

        return new MembershipStats(
            TotalMemberships: _totalMemberships,
            ActiveMemberships: _activeMembershipsCount,
            BannedMemberships: _bannedMemberships,
            ExpiredMemberships: _expiredMemberships,
            MembershipsByRole: _membershipsByRole.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            MembershipsByPod: _membershipsByPod.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LastOperation: _lastOperation);
    }

    /// <inheritdoc/>
    public async Task<MembershipCleanupResult> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cleaned = 0;
        var errors = 0;
        var expiredKeys = _activeMemberships.Where(kvp => kvp.Value.ExpiresAt < DateTimeOffset.UtcNow)
                                           .Select(kvp => kvp.Key)
                                           .ToList();

        foreach (var membershipKey in expiredKeys)
        {
            try
            {
                if (_activeMemberships.TryRemove(membershipKey, out var membershipInfo))
                {
                    Interlocked.Decrement(ref _activeMembershipsCount);
                    Interlocked.Increment(ref _expiredMemberships);
                    cleaned++;

                    _membershipsByRole.AddOrUpdate(membershipInfo.Role, 0, (_, count) => Math.Max(0, count - 1));
                    _membershipsByPod.AddOrUpdate(membershipInfo.PodId, 0, (_, count) => Math.Max(0, count - 1));

                    if (membershipInfo.IsBanned)
                    {
                        Interlocked.Decrement(ref _bannedMemberships);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PodMembership] Error cleaning up membership {Key}", membershipKey);
                errors++;
            }
        }

        _logger.LogInformation("[PodMembership] Cleaned up {Cleaned} expired memberships, {Errors} errors", cleaned, errors);

        return new MembershipCleanupResult(
            RecordsCleaned: cleaned,
            ErrorsEncountered: errors,
            CompletedAt: DateTimeOffset.UtcNow);
    }

    // Helper methods
    private static string GetMembershipDhtKey(string podId, string peerId) => $"pod:{podId}:member:{peerId}";

    private SignedMembershipRecord CreateSignedMembershipRecord(string podId, PodMember member, string action)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Create the signed membership record in the existing format
        var record = new SignedMembershipRecord
        {
            PodId = podId,
            PeerId = member.PeerId,
            Role = member.Role,
            Action = action,
            TimestampUnixMs = timestamp,
            PublicKey = member.PublicKey ?? string.Empty,
            Signature = string.Empty // Will be set after signing
        };

        // Create signable payload
        var payload = $"{record.PodId}:{record.PeerId}:{record.Role}:{record.Action}:{record.TimestampUnixMs}";

        // Create control envelope for signing
        var envelope = new ControlEnvelope
        {
            Type = "pod-membership",
            Payload = System.Text.Encoding.UTF8.GetBytes(payload),
            TimestampUnixMs = timestamp
        };

        // Sign the envelope
        var signedEnvelope = _signer.Sign(envelope);

        // Set the signature
        record.Signature = signedEnvelope.Signature;

        return record;
    }

    private bool VerifyMembershipSignature(SignedMembershipRecord signedRecord)
    {
        try
        {
            // Recreate the payload that was signed
            var payload = $"{signedRecord.PodId}:{signedRecord.PeerId}:{signedRecord.Role}:{signedRecord.Action}:{signedRecord.TimestampUnixMs}";

            // Create the envelope that would have been signed
            var envelope = new ControlEnvelope
            {
                Type = "pod-membership",
                Payload = System.Text.Encoding.UTF8.GetBytes(payload),
                TimestampUnixMs = signedRecord.TimestampUnixMs,
                Signature = signedRecord.Signature,
                PublicKey = signedRecord.PublicKey
            };

            return _signer.Verify(envelope);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Information about an active membership.
    /// </summary>
    private record MembershipInfo(
        string PodId,
        string PeerId,
        string Role,
        bool IsBanned,
        DateTimeOffset PublishedAt,
        DateTimeOffset ExpiresAt,
        string Signature);
}
