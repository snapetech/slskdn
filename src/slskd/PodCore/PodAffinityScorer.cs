namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pod affinity scoring service.
/// Computes engagement-based and trust-based scores for pod recommendations.
/// </summary>
public interface IPodAffinityScorer
{
    /// <summary>
    /// Computes affinity score for a pod (0.0 to 1.0).
    /// Higher scores indicate better match for user interests.
    /// </summary>
    Task<double> ComputeAffinityAsync(string podId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Gets ranked pod recommendations for a user.
    /// </summary>
    Task<IReadOnlyList<PodRecommendation>> GetRecommendationsAsync(
        string userId, 
        int limit = 10, 
        CancellationToken ct = default);
}

public record PodRecommendation(string PodId, double AffinityScore, string Reason);

public class PodAffinityScorer : IPodAffinityScorer
{
    private readonly ILogger<PodAffinityScorer> logger;
    private readonly IPodService podService;
    private readonly IPodMessaging podMessaging;

    public PodAffinityScorer(
        ILogger<PodAffinityScorer> logger,
        IPodService podService,
        IPodMessaging podMessaging)
    {
        this.logger = logger;
        this.podService = podService;
        this.podMessaging = podMessaging;
    }

    public async Task<double> ComputeAffinityAsync(string podId, string userId, CancellationToken ct = default)
    {
        try
        {
            var pod = await podService.GetPodAsync(podId, ct);
            if (pod == null) return 0.0;

            var members = await podService.GetMembersAsync(podId, ct);
            var messages = await podMessaging.GetMessagesAsync(podId, "general", null, ct);

            // Compute weighted affinity components
            var engagementScore = ComputeEngagementScore(members, messages);
            var trustScore = await ComputeTrustScoreAsync(podId, userId, members, ct);
            var sizeScore = ComputeSizeScore(members.Count);
            var activityScore = ComputeActivityScore(messages);

            // Weighted combination
            var affinity = 
                (engagementScore * 0.3) +
                (trustScore * 0.4) +
                (sizeScore * 0.15) +
                (activityScore * 0.15);

            return Math.Clamp(affinity, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compute affinity for pod {PodId}", podId);
            return 0.0;
        }
    }

    public async Task<IReadOnlyList<PodRecommendation>> GetRecommendationsAsync(
        string userId, 
        int limit = 10, 
        CancellationToken ct = default)
    {
        try
        {
            var allPods = await podService.ListAsync(ct);
            var recommendations = new List<PodRecommendation>();

            foreach (var pod in allPods)
            {
                var affinity = await ComputeAffinityAsync(pod.PodId, userId, ct);
                var reason = DetermineReason(affinity);
                recommendations.Add(new PodRecommendation(pod.PodId, affinity, reason));
            }

            return recommendations
                .OrderByDescending(r => r.AffinityScore)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pod recommendations for user {UserId}", userId);
            return Array.Empty<PodRecommendation>();
        }
    }

    /// <summary>
    /// Engagement score based on member count and message activity.
    /// </summary>
    private static double ComputeEngagementScore(
        IReadOnlyList<PodMember> members, 
        IReadOnlyList<PodMessage> messages)
    {
        if (members.Count == 0) return 0.0;

        // Active members (posted in last 7 days)
        var recentCutoff = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds();
        var recentMessages = messages.Where(m => m.TimestampUnixMs >= recentCutoff).ToList();
        var activeMembers = recentMessages.Select(m => m.SenderPeerId).Distinct().Count();

        // Engagement ratio: active members / total members
        var engagementRatio = members.Count > 0 ? (double)activeMembers / members.Count : 0.0;

        // Message frequency (messages per day)
        var messageFrequency = recentMessages.Count / 7.0;
        var frequencyScore = Math.Min(messageFrequency / 10.0, 1.0); // Cap at 10 msgs/day = 1.0

        return (engagementRatio * 0.6) + (frequencyScore * 0.4);
    }

    /// <summary>
    /// Trust score based on known/trusted members in the pod.
    /// </summary>
    private async Task<double> ComputeTrustScoreAsync(
        string podId, 
        string userId, 
        IReadOnlyList<PodMember> members, 
        CancellationToken ct)
    {
        // TODO: Integrate with SecurityCore PeerReputation system
        // For now, use simple heuristics:
        
        // 1. Is user already a member? → high trust
        if (members.Any(m => m.PeerId == userId))
            return 1.0;

        // 2. Are there verified/trusted peers? (placeholder)
        var verifiedCount = members.Count(m => !string.IsNullOrEmpty(m.PublicKey));
        var verifiedRatio = members.Count > 0 ? (double)verifiedCount / members.Count : 0.0;

        // 3. No banned members? (placeholder)
        var bannedCount = members.Count(m => m.IsBanned);
        var bannedPenalty = bannedCount > 0 ? 0.5 : 1.0;

        return verifiedRatio * bannedPenalty;
    }

    /// <summary>
    /// Size score based on pod member count.
    /// Favors moderately-sized pods (not too small, not too large).
    /// </summary>
    private static double ComputeSizeScore(int memberCount)
    {
        if (memberCount == 0) return 0.0;
        if (memberCount == 1) return 0.3; // Solo pods are low value

        // Optimal range: 5-50 members
        if (memberCount >= 5 && memberCount <= 50)
            return 1.0;

        // Smaller pods: scale up from 0.3 to 1.0
        if (memberCount < 5)
            return 0.3 + (0.7 * memberCount / 5.0);

        // Larger pods: scale down from 1.0 to 0.5
        if (memberCount > 50 && memberCount <= 500)
            return 1.0 - (0.5 * (memberCount - 50) / 450.0);

        // Very large pods (>500): capped at 0.5
        return 0.5;
    }

    /// <summary>
    /// Activity score based on recent message frequency.
    /// </summary>
    private static double ComputeActivityScore(IReadOnlyList<PodMessage> messages)
    {
        if (messages.Count == 0) return 0.0;

        // Check last 24 hours
        var recentCutoff = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds();
        var recentCount = messages.Count(m => m.TimestampUnixMs >= recentCutoff);

        // Active pods have at least 1 message per day
        if (recentCount == 0) return 0.2; // Some historical activity

        // Score based on message frequency (1-10+ messages/day)
        return Math.Min(recentCount / 10.0, 1.0);
    }

    /// <summary>
    /// Determines human-readable reason for recommendation.
    /// </summary>
    private static string DetermineReason(double affinity)
    {
        return affinity switch
        {
            >= 0.8 => "Highly active with trusted members",
            >= 0.6 => "Good engagement and activity",
            >= 0.4 => "Moderate activity",
            >= 0.2 => "Growing community",
            _ => "New or quiet pod"
        };
    }
}















