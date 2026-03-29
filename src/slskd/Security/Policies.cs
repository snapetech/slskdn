// <copyright file="Policies.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Security;

using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using slskd.Common.Security;
using slskd.DhtRendezvous.Security;

/// <summary>
/// Network guard policy - checks IP blocklist, rate limits, and connection patterns.
/// </summary>
public class NetworkGuardPolicy : ISecurityPolicy
{
    private readonly ILogger<NetworkGuardPolicy> logger;
    private readonly OverlayBlocklist? blocklist;
    private readonly OverlayRateLimiter? rateLimiter;
    private readonly NetworkGuard? networkGuard;

    public NetworkGuardPolicy(
        ILogger<NetworkGuardPolicy> logger,
        OverlayBlocklist? blocklist = null,
        OverlayRateLimiter? rateLimiter = null,
        NetworkGuard? networkGuard = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.blocklist = blocklist;
        this.rateLimiter = rateLimiter;
        this.networkGuard = networkGuard;
    }

    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.PeerId))
        {
            return Task.FromResult(new SecurityDecision(false, "PeerId is required"));
        }

        // Check username blocklist (PeerId might be username for Mesh)
        if (blocklist != null && blocklist.IsBlocked(context.PeerId))
        {
            logger.LogWarning("[NetworkGuard] Blocked peer {PeerId} (username in blocklist)", context.PeerId);
            return Task.FromResult(new SecurityDecision(false, $"Peer {context.PeerId} is blocked"));
        }

        if (!string.IsNullOrWhiteSpace(context.Operation) && rateLimiter != null)
        {
            var op = context.Operation.ToLowerInvariant();
            var rateLimitResult =
                op.Contains("mesh-search", StringComparison.Ordinal) ? rateLimiter.CheckMeshSearchRequest(context.PeerId) :
                op.Contains("delta", StringComparison.Ordinal) ? rateLimiter.CheckDeltaRequest(context.PeerId) :
                op.Contains("message", StringComparison.Ordinal) ? rateLimiter.CheckMessage(context.PeerId) :
                RateLimitResult.Allowed();

            if (!rateLimitResult.IsAllowed)
            {
                logger.LogWarning("[NetworkGuard] Rate limited peer {PeerId} for {Operation}: {Reason}", context.PeerId, context.Operation, rateLimitResult.Reason);
                return Task.FromResult(new SecurityDecision(false, rateLimitResult.Reason ?? "rate limited"));
            }
        }

        if (networkGuard != null &&
            IPAddress.TryParse(context.PeerId, out var peerIp) &&
            !networkGuard.AllowRequest(peerIp))
        {
            logger.LogWarning("[NetworkGuard] Request limit exceeded for {PeerIp}", peerIp);
            return Task.FromResult(new SecurityDecision(false, $"Too many pending requests from {peerIp}"));
        }

        return Task.FromResult(new SecurityDecision(true, "network ok"));
    }
}

/// <summary>
/// Reputation policy - checks peer reputation scores and abuse history.
/// </summary>
public class ReputationPolicy : ISecurityPolicy
{
    private readonly ILogger<ReputationPolicy> logger;
    private readonly PeerReputation? reputation;

    public ReputationPolicy(
        ILogger<ReputationPolicy> logger,
        PeerReputation? reputation = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.reputation = reputation;
    }

    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.PeerId))
        {
            return Task.FromResult(new SecurityDecision(false, "PeerId is required"));
        }

        if (reputation == null)
        {
            // If reputation service is not available, allow but log warning
            logger.LogDebug("[Reputation] Reputation service not available, allowing");
            return Task.FromResult(new SecurityDecision(true, "reputation service unavailable"));
        }

        var score = reputation.GetScore(context.PeerId);

        // If peer is untrusted (score <= 20), deny
        if (score.HasValue && score.Value <= PeerReputation.UntrustedThreshold)
        {
            logger.LogWarning("[Reputation] Denied peer {PeerId} - untrusted (score: {Score})", context.PeerId, score.Value);
            return Task.FromResult(new SecurityDecision(false, $"Peer {context.PeerId} has low reputation (score: {score.Value})"));
        }

        // If peer is unknown (no score), allow but log
        if (!score.HasValue)
        {
            logger.LogDebug("[Reputation] Unknown peer {PeerId}, allowing", context.PeerId);
            return Task.FromResult(new SecurityDecision(true, "peer reputation unknown"));
        }

        logger.LogDebug("[Reputation] Allowed peer {PeerId} (score: {Score})", context.PeerId, score.Value);
        return Task.FromResult(new SecurityDecision(true, $"reputation ok (score: {score.Value})"));
    }
}

/// <summary>
/// Consensus policy - verifies mesh consensus on content/peers.
/// </summary>
public class ConsensusPolicy : ISecurityPolicy
{
    private readonly ILogger<ConsensusPolicy> logger;

    public ConsensusPolicy(ILogger<ConsensusPolicy> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.PeerId))
        {
            return Task.FromResult(new SecurityDecision(false, "PeerId is required"));
        }

        if (!string.IsNullOrWhiteSpace(context.Operation) &&
            context.Operation.Contains("consensus", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[Consensus] Denied consensus-gated operation {Operation} for peer {PeerId}: no consensus backend is wired", context.Operation, context.PeerId);
            return Task.FromResult(new SecurityDecision(false, "consensus verification unavailable"));
        }

        return Task.FromResult(new SecurityDecision(true, "consensus check not implemented"));
    }
}

/// <summary>
/// Content safety policy - scans content hashes against known bad content.
/// </summary>
public class ContentSafetyPolicy : ISecurityPolicy
{
    private readonly ILogger<ContentSafetyPolicy> logger;
    private readonly HashSet<string>? knownBadContentHashes;

    public ContentSafetyPolicy(
        ILogger<ContentSafetyPolicy> logger,
        IEnumerable<string>? knownBadContentHashes = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.knownBadContentHashes = knownBadContentHashes != null ? new HashSet<string>(knownBadContentHashes, StringComparer.OrdinalIgnoreCase) : null;
    }

    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        // If no content ID provided, allow (this policy only applies to content operations)
        if (string.IsNullOrEmpty(context.ContentId))
        {
            return Task.FromResult(new SecurityDecision(true, "no content ID to check"));
        }

        // If no known bad content list, allow but log warning
        if (knownBadContentHashes == null || knownBadContentHashes.Count == 0)
        {
            logger.LogDebug("[ContentSafety] No known bad content list, allowing content {ContentId}", context.ContentId);
            return Task.FromResult(new SecurityDecision(true, "content safety list unavailable"));
        }

        // Check if content ID matches known bad content
        // ContentId might be a hash, MBID, or other identifier
        if (knownBadContentHashes.Contains(context.ContentId))
        {
            logger.LogWarning("[ContentSafety] Blocked content {ContentId} - matches known bad content", context.ContentId);
            return Task.FromResult(new SecurityDecision(false, $"Content {context.ContentId} is flagged as unsafe"));
        }

        logger.LogDebug("[ContentSafety] Allowed content {ContentId}", context.ContentId);
        return Task.FromResult(new SecurityDecision(true, "content ok"));
    }
}

/// <summary>
/// Honeypot policy - detects suspicious behavior patterns.
/// </summary>
public class HoneypotPolicy : ISecurityPolicy
{
    private readonly ILogger<HoneypotPolicy> logger;
    private readonly ConcurrentDictionary<string, SuspiciousActivityTracker> activityTrackers = new();

    public HoneypotPolicy(ILogger<HoneypotPolicy> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.PeerId))
        {
            return Task.FromResult(new SecurityDecision(false, "PeerId is required"));
        }

        var tracker = activityTrackers.GetOrAdd(context.PeerId, _ => new SuspiciousActivityTracker());

        lock (tracker)
        {
            var now = DateTimeOffset.UtcNow;

            // Clean old activity records (older than 1 hour)
            tracker.RecentActivities.RemoveAll(a => a < now.AddHours(-1));

            // Check for suspicious patterns:
            // 1. Too many requests in short time (potential scanning/probing)
            var recentCount = tracker.RecentActivities.Count(a => a > now.AddMinutes(-5));
            if (recentCount > 50)
            {
                logger.LogWarning("[Honeypot] Suspicious activity detected from {PeerId}: {Count} requests in 5 minutes", context.PeerId, recentCount);
                tracker.SuspiciousCount++;
                if (tracker.SuspiciousCount >= 3)
                {
                    return Task.FromResult(new SecurityDecision(false, $"Peer {context.PeerId} shows suspicious behavior pattern"));
                }
            }

            // 2. Rapid operation switching (potential enumeration attack)
            if (!string.IsNullOrEmpty(context.Operation))
            {
                tracker.RecentOperations.Add((context.Operation, now));
                tracker.RecentOperations.RemoveAll(o => o.Timestamp < now.AddMinutes(-10));

                var uniqueOperations = tracker.RecentOperations.Select(o => o.Operation).Distinct().Count();
                if (uniqueOperations > 20)
                {
                    logger.LogWarning("[Honeypot] Suspicious operation pattern from {PeerId}: {Count} unique operations in 10 minutes", context.PeerId, uniqueOperations);
                    tracker.SuspiciousCount++;
                    if (tracker.SuspiciousCount >= 3)
                    {
                        return Task.FromResult(new SecurityDecision(false, $"Peer {context.PeerId} shows enumeration pattern"));
                    }
                }
            }

            // Record this activity
            tracker.RecentActivities.Add(now);
            tracker.TotalRequests++;
        }

        return Task.FromResult(new SecurityDecision(true, "honeypot check ok"));
    }

    private class SuspiciousActivityTracker
    {
        public List<DateTimeOffset> RecentActivities { get; } = new();
        public List<(string Operation, DateTimeOffset Timestamp)> RecentOperations { get; } = new();
        public int SuspiciousCount { get; set; }
        public long TotalRequests { get; set; }
    }
}

/// <summary>
/// NAT abuse detection policy - detects peers falsely claiming symmetric NAT or relay abuse.
/// </summary>
public class NatAbuseDetectionPolicy : ISecurityPolicy
{
    private readonly ILogger<NatAbuseDetectionPolicy> logger;
    private readonly ConcurrentDictionary<string, NatAbuseTracker> abuseTrackers = new();

    public NatAbuseDetectionPolicy(ILogger<NatAbuseDetectionPolicy> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.PeerId))
        {
            return Task.FromResult(new SecurityDecision(false, "PeerId is required"));
        }

        if (string.IsNullOrWhiteSpace(context.Operation))
        {
            return Task.FromResult(new SecurityDecision(true, "no nat signal"));
        }

        var tracker = abuseTrackers.GetOrAdd(context.PeerId, _ => new NatAbuseTracker());
        var operation = context.Operation.ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        lock (tracker)
        {
            if (operation.Contains("symmetric", StringComparison.Ordinal))
            {
                tracker.ClaimedSymmetricNatCount++;
            }

            if (operation.Contains("direct", StringComparison.Ordinal) && operation.Contains("success", StringComparison.Ordinal))
            {
                tracker.SuccessfulDirectConnections++;
            }

            if (operation.Contains("relay", StringComparison.Ordinal))
            {
                tracker.RelayUsageCount++;
            }

            if (operation.Contains("nat-type-change", StringComparison.Ordinal))
            {
                if (tracker.LastNatTypeChange.HasValue && tracker.LastNatTypeChange.Value > now.AddMinutes(-10))
                {
                    logger.LogWarning("[NatAbuse] Denied peer {PeerId} for frequent NAT type changes", context.PeerId);
                    return Task.FromResult(new SecurityDecision(false, "frequent NAT type changes detected"));
                }

                tracker.LastNatTypeChange = now;
            }

            if (tracker.ClaimedSymmetricNatCount >= 3 && tracker.SuccessfulDirectConnections >= 2)
            {
                logger.LogWarning("[NatAbuse] Denied peer {PeerId} for inconsistent NAT claims", context.PeerId);
                return Task.FromResult(new SecurityDecision(false, "inconsistent NAT claims detected"));
            }

            if (tracker.RelayUsageCount >= 20 && tracker.SuccessfulDirectConnections > 0)
            {
                logger.LogWarning("[NatAbuse] Denied peer {PeerId} for relay abuse", context.PeerId);
                return Task.FromResult(new SecurityDecision(false, "relay abuse detected"));
            }
        }

        return Task.FromResult(new SecurityDecision(true, "nat behavior ok"));
    }

    private class NatAbuseTracker
    {
        public int ClaimedSymmetricNatCount { get; set; }
        public int SuccessfulDirectConnections { get; set; }
        public int RelayUsageCount { get; set; }
        public DateTimeOffset? LastNatTypeChange { get; set; }
    }
}
