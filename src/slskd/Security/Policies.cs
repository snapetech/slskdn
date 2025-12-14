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

        // TODO: Resolve PeerId to IP address for IP-based checks
        // For now, we only check username blocklist
        // IP-based checks require a PeerId -> IP mapping service

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

        // TODO: Implement mesh consensus verification
        // This requires:
        // 1. Querying multiple mesh peers for their opinion on this peer/content
        // 2. Checking if there's consensus (e.g., >50% agree)
        // 3. Denying if there's strong negative consensus
        // For now, allow all requests but log that consensus check is not implemented
        logger.LogDebug("[Consensus] Consensus check not fully implemented, allowing peer {PeerId}", context.PeerId);
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
                tracker.RecentOperations.RemoveAll(o => o.Item2 < now.AddMinutes(-10));

                var uniqueOperations = tracker.RecentOperations.Select(o => o.Item1).Distinct().Count();
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

        // TODO: Implement NAT abuse detection
        // This requires:
        // 1. Tracking peers that claim symmetric NAT but successfully connect directly
        // 2. Detecting relay abuse (excessive relay usage when direct connection should work)
        // 3. Detecting peers that change NAT type frequently (suspicious)
        // For now, allow all requests but log that NAT abuse detection is not fully implemented
        logger.LogDebug("[NatAbuse] NAT abuse detection not fully implemented, allowing peer {PeerId}", context.PeerId);
        return Task.FromResult(new SecurityDecision(true, "nat abuse detection not fully implemented"));
    }

    private class NatAbuseTracker
    {
        public int ClaimedSymmetricNatCount { get; set; }
        public int SuccessfulDirectConnections { get; set; }
        public DateTimeOffset? LastNatTypeChange { get; set; }
    }
}
