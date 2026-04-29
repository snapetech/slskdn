// <copyright file="PodOpinionAggregator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.PodCore;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
///     Service for aggregating and weighting pod member opinions based on affinity and trust.
/// </summary>
public class PodOpinionAggregator : IPodOpinionAggregator
{
    private const int ActivityWindowDays = 30;
    private const int MaxMessagesPerChannelForAffinity = 5000;
    private readonly IPodService _podService;
    private readonly IPodOpinionService _opinionService;
    private readonly IPodMessageStorage _messageStorage;
    private readonly ILogger<PodOpinionAggregator> _logger;

    // Cached affinity scores (podId -> memberPeerId -> affinity)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MemberAffinity>> _affinityCache = new();

    // Cache for aggregated opinions
    private readonly ConcurrentDictionary<string, AggregatedOpinions> _aggregatedCache = new();

    public PodOpinionAggregator(
        IPodService podService,
        IPodOpinionService opinionService,
        IPodMessageStorage messageStorage,
        ILogger<PodOpinionAggregator> logger)
    {
        _podService = podService;
        _opinionService = opinionService;
        _messageStorage = messageStorage;
        _logger = logger;
    }

    public async Task<AggregatedOpinions> GetAggregatedOpinionsAsync(string podId, string contentId, CancellationToken ct = default)
    {
        var cacheKey = $"{podId}:{contentId}";

        // Check cache first (with 5-minute expiry)
        if (_aggregatedCache.TryGetValue(cacheKey, out var cached) &&
            DateTimeOffset.UtcNow - cached.LastUpdated < TimeSpan.FromMinutes(5))
        {
            return cached;
        }

        try
        {
            // Get all opinions for the content
            var opinions = await _opinionService.GetOpinionsAsync(podId, contentId, ct);
            if (!opinions.Any())
            {
                var emptyResult = new AggregatedOpinions(
                    PodId: podId,
                    ContentId: contentId,
                    WeightedAverageScore: 0,
                    UnweightedAverageScore: 0,
                    TotalOpinions: 0,
                    UniqueVariants: 0,
                    ContributingMembers: 0,
                    ConsensusStrength: 0,
                    VariantAggregates: Array.Empty<VariantAggregate>(),
                    MemberContributions: new Dictionary<string, double>(),
                    LastUpdated: DateTimeOffset.UtcNow);

                _aggregatedCache[cacheKey] = emptyResult;
                return emptyResult;
            }

            // Get member affinities
            var affinities = await GetMemberAffinitiesAsync(podId, ct);

            // Group opinions by variant
            var variantGroups = opinions.GroupBy(o => o.VariantHash);

            // Calculate weighted opinions and aggregates
            var variantAggregates = new List<VariantAggregate>();
            var memberContributions = new Dictionary<string, double>();
            var allUnweightedScores = new List<double>();
            var totalWeightedScore = 0.0;
            var totalWeight = 0.0;

            foreach (var variantGroup in variantGroups)
            {
                var variantOpinions = variantGroup.ToList();
                var weightedOpinions = new List<WeightedOpinion>();

                foreach (var opinion in variantOpinions)
                {
                    var affinity = affinities.GetValueOrDefault(opinion.SenderPeerId, new MemberAffinity(
                        PeerId: opinion.SenderPeerId,
                        AffinityScore: 0.5, // Default neutral affinity
                        MessageCount: 0,
                        OpinionCount: 0,
                        MembershipDuration: TimeSpan.Zero,
                        LastActivity: DateTimeOffset.MinValue,
                        TrustScore: 0.5,
                        RecentActivity: Array.Empty<string>()));

                    var weightedOpinion = new WeightedOpinion(
                        Opinion: opinion,
                        AffinityWeight: affinity.AffinityScore,
                        WeightedScore: opinion.Score * affinity.AffinityScore);

                    weightedOpinions.Add(weightedOpinion);

                    // Track member contributions
                    memberContributions[opinion.SenderPeerId] =
                        memberContributions.GetValueOrDefault(opinion.SenderPeerId, 0) + affinity.AffinityScore;

                    allUnweightedScores.Add(opinion.Score);
                    totalWeightedScore += weightedOpinion.WeightedScore;
                    totalWeight += affinity.AffinityScore;
                }

                // Calculate variant statistics
                var weightSum = weightedOpinions.Sum(wo => wo.AffinityWeight);
                var weightedAvg = weightSum > 0
                    ? weightedOpinions.Sum(wo => wo.WeightedScore) / weightSum
                    : 0;
                var unweightedAvg = variantOpinions.Average(o => o.Score);
                var stdDev = CalculateStandardDeviation(variantOpinions.Select(o => o.Score));

                variantAggregates.Add(new VariantAggregate(
                    VariantHash: variantGroup.Key,
                    WeightedAverageScore: weightedAvg,
                    UnweightedAverageScore: unweightedAvg,
                    OpinionCount: variantOpinions.Count,
                    ScoreStandardDeviation: stdDev,
                    AffinityWeightSum: weightSum,
                    Opinions: weightedOpinions));
            }

            // Calculate overall statistics
            var weightedAverage = totalWeight > 0 ? totalWeightedScore / totalWeight : 0;
            var unweightedAverage = allUnweightedScores.Any() ? allUnweightedScores.Average() : 0;
            var consensusStrength = CalculateConsensusStrength(variantAggregates);

            var result = new AggregatedOpinions(
                PodId: podId,
                ContentId: contentId,
                WeightedAverageScore: weightedAverage,
                UnweightedAverageScore: unweightedAverage,
                TotalOpinions: opinions.Count,
                UniqueVariants: variantGroups.Count(),
                ContributingMembers: memberContributions.Count,
                ConsensusStrength: consensusStrength,
                VariantAggregates: variantAggregates,
                MemberContributions: memberContributions,
                LastUpdated: DateTimeOffset.UtcNow);

            // Cache the result
            _aggregatedCache[cacheKey] = result;

            return result;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating opinions for pod {PodId} content {ContentId}", podId, contentId);
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<string, MemberAffinity>> GetMemberAffinitiesAsync(string podId, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        // Check cache first (with 10-minute expiry)
        if (_affinityCache.TryGetValue(podId, out var cachedAffinities))
        {
            var now = DateTimeOffset.UtcNow;
            var allValid = cachedAffinities.Values.All(a => now - a.LastActivity < TimeSpan.FromMinutes(10));
            if (allValid)
            {
                return cachedAffinities.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        try
        {
            // Get pod members
            var members = await _podService.GetMembersAsync(podId, ct);
            var affinities = new ConcurrentDictionary<string, MemberAffinity>();

            // Get pod/message/opinion activity data once per pod and fan it into member affinities.
            var pod = await _podService.GetPodAsync(podId, ct);
            var channelIds = pod?.Channels.Select(c => c.ChannelId).ToList() ?? new List<string>();
            var membershipHistory = await _podService.GetMembershipHistoryAsync(podId, ct);
            var knownContentIds = await _opinionService.GetKnownContentIdsAsync(podId, ct);

            var now = DateTimeOffset.UtcNow;
            var activityWindowStart = now.AddDays(-ActivityWindowDays);
            var messageActivityByPeer = await LoadMessageActivityByPeerAsync(podId, channelIds, activityWindowStart, ct);
            var opinionCountsByPeer = await LoadOpinionCountsByPeerAsync(podId, knownContentIds, ct);
            var membershipHistoryByPeer = membershipHistory
                .Where(record => !string.IsNullOrWhiteSpace(record.PeerId))
                .GroupBy(record => record.PeerId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var member in members)
            {
                try
                {
                    var normalizedPeerId = member.PeerId?.Trim() ?? string.Empty;
                    var messageActivity = messageActivityByPeer.GetValueOrDefault(normalizedPeerId);
                    var messageCount = messageActivity?.MessageCount ?? 0;
                    var memberOpinions = opinionCountsByPeer.GetValueOrDefault(normalizedPeerId);
                    var membershipRecords = membershipHistoryByPeer.GetValueOrDefault(normalizedPeerId);
                    var joinedAt = member.JoinedAt ?? TryGetJoinedAt(membershipRecords);
                    var membershipDuration = joinedAt.HasValue ? now - joinedAt.Value : TimeSpan.Zero;
                    var lastMembershipActivity = TryGetLastMembershipActivity(membershipRecords);
                    var lastActivity = new[]
                    {
                        member.LastSeen,
                        messageActivity?.LastMessageAt,
                        lastMembershipActivity
                    }
                    .Where(timestamp => timestamp.HasValue)
                    .Select(timestamp => timestamp!.Value)
                    .DefaultIfEmpty(DateTimeOffset.MinValue)
                    .Max();

                    var recentActivity = BuildRecentActivity(messageCount, memberOpinions, membershipRecords);
                    var affinityScore = CalculateAffinityScore(
                        messageCount: messageCount,
                        opinionCount: memberOpinions,
                        membershipDuration: membershipDuration,
                        isActive: lastActivity >= activityWindowStart);

                    var affinity = new MemberAffinity(
                        PeerId: normalizedPeerId,
                        AffinityScore: affinityScore,
                        MessageCount: messageCount,
                        OpinionCount: memberOpinions,
                        MembershipDuration: membershipDuration,
                        LastActivity: lastActivity,
                        TrustScore: CalculateTrustScore(member),
                        RecentActivity: recentActivity);

                    affinities[normalizedPeerId] = affinity;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error calculating affinity for member {MemberId} in pod {PodId}", member.PeerId, podId);

                    // Fallback affinity
                    var normalizedPeerId = member.PeerId?.Trim() ?? string.Empty;
                    affinities[normalizedPeerId] = new MemberAffinity(
                        PeerId: normalizedPeerId,
                        AffinityScore: 0.5,
                        MessageCount: 0,
                        OpinionCount: 0,
                        MembershipDuration: TimeSpan.Zero,
                        LastActivity: DateTimeOffset.MinValue,
                        TrustScore: 0.5,
                        RecentActivity: Array.Empty<string>());
                }
            }

            // Cache the results
            _affinityCache[podId] = affinities;

            return affinities.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting member affinities for pod {PodId}", podId);
            return new Dictionary<string, MemberAffinity>();
        }
    }

    private async Task<Dictionary<string, MemberMessageActivity>> LoadMessageActivityByPeerAsync(
        string podId,
        IReadOnlyList<string> channelIds,
        DateTimeOffset activityWindowStart,
        CancellationToken ct)
    {
        var activityByPeer = new Dictionary<string, MemberMessageActivity>(StringComparer.OrdinalIgnoreCase);
        var sinceTimestamp = activityWindowStart.ToUnixTimeMilliseconds();

        foreach (var channelId in channelIds.Where(channelId => !string.IsNullOrWhiteSpace(channelId)))
        {
            var messages = await _messageStorage.GetMessagesAsync(podId, channelId, sinceTimestamp, MaxMessagesPerChannelForAffinity, ct);
            foreach (var message in messages)
            {
                var normalizedPeerId = message.SenderPeerId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPeerId))
                {
                    continue;
                }

                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(message.TimestampUnixMs);
                if (!activityByPeer.TryGetValue(normalizedPeerId, out var activity))
                {
                    activityByPeer[normalizedPeerId] = new MemberMessageActivity(1, timestamp);
                    continue;
                }

                activityByPeer[normalizedPeerId] = new MemberMessageActivity(
                    activity.MessageCount + 1,
                    timestamp > activity.LastMessageAt ? timestamp : activity.LastMessageAt);
            }
        }

        return activityByPeer;
    }

    private async Task<Dictionary<string, int>> LoadOpinionCountsByPeerAsync(
        string podId,
        IReadOnlyList<string> knownContentIds,
        CancellationToken ct)
    {
        var opinionCountsByPeer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var contentId in knownContentIds
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var opinions = await _opinionService.GetOpinionsAsync(podId, contentId, ct);
            foreach (var opinion in opinions)
            {
                var normalizedPeerId = opinion.SenderPeerId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPeerId))
                {
                    continue;
                }

                opinionCountsByPeer[normalizedPeerId] = opinionCountsByPeer.GetValueOrDefault(normalizedPeerId) + 1;
            }
        }

        return opinionCountsByPeer;
    }

    private static DateTimeOffset? TryGetJoinedAt(IReadOnlyList<SignedMembershipRecord>? membershipRecords)
    {
        return membershipRecords?
            .Where(record => string.Equals(record.Action, "join", StringComparison.OrdinalIgnoreCase))
            .Select(record => DateTimeOffset.FromUnixTimeMilliseconds(record.TimestampUnixMs))
            .OrderBy(timestamp => timestamp)
            .FirstOrDefault();
    }

    private static DateTimeOffset? TryGetLastMembershipActivity(IReadOnlyList<SignedMembershipRecord>? membershipRecords)
    {
        return membershipRecords?
            .Select(record => DateTimeOffset.FromUnixTimeMilliseconds(record.TimestampUnixMs))
            .OrderByDescending(timestamp => timestamp)
            .FirstOrDefault();
    }

    private static string[] BuildRecentActivity(int messageCount, int opinionCount, IReadOnlyList<SignedMembershipRecord>? membershipRecords)
    {
        var activity = new List<string>();
        if (messageCount > 0)
        {
            activity.Add("messages");
        }

        if (opinionCount > 0)
        {
            activity.Add("opinions");
        }

        if (membershipRecords != null && membershipRecords.Count > 0)
        {
            activity.Add("membership");
        }

        return activity.ToArray();
    }

    public async Task<MemberAffinity> GetMemberAffinityAsync(string podId, string memberPeerId, CancellationToken ct = default)
    {
        var affinities = await GetMemberAffinitiesAsync(podId, ct);
        return affinities.GetValueOrDefault(memberPeerId, new MemberAffinity(
            PeerId: memberPeerId,
            AffinityScore: 0.5,
            MessageCount: 0,
            OpinionCount: 0,
            MembershipDuration: TimeSpan.Zero,
            LastActivity: DateTimeOffset.MinValue,
            TrustScore: 0.5,
            RecentActivity: Array.Empty<string>()));
    }

    public async Task<AffinityUpdateResult> UpdateMemberAffinitiesAsync(string podId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Clear cache to force refresh
            _affinityCache.TryRemove(podId, out _);

            // Re-calculate affinities
            var affinities = await GetMemberAffinitiesAsync(podId, ct);

            return new AffinityUpdateResult(
                Success: true,
                PodId: podId,
                MembersUpdated: affinities.Count,
                Duration: stopwatch.Elapsed);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member affinities for pod {PodId}", podId);
            return new AffinityUpdateResult(
                Success: false,
                PodId: podId,
                MembersUpdated: 0,
                Duration: stopwatch.Elapsed,
                ErrorMessage: "Failed to update member affinities");
        }
    }

    public async Task<IReadOnlyList<VariantRecommendation>> GetConsensusRecommendationsAsync(string podId, string contentId, CancellationToken ct = default)
    {
        var aggregated = await GetAggregatedOpinionsAsync(podId, contentId, ct);
        var recommendations = new List<VariantRecommendation>();

        foreach (var variant in aggregated.VariantAggregates)
        {
            var recommendation = GenerateRecommendation(variant, aggregated);
            recommendations.Add(recommendation);
        }

        // Sort by consensus score (descending)
        return recommendations.OrderByDescending(r => r.ConsensusScore).ToList();
    }

    private double CalculateAffinityScore(int messageCount, int opinionCount, TimeSpan membershipDuration, bool isActive)
    {
        // Base score from activity
        var activityScore = Math.Min(1.0, (messageCount + opinionCount * 2.0) / 100.0);

        // Membership duration bonus (up to 0.3)
        var durationMonths = membershipDuration.TotalDays / 30.0;
        var durationBonus = Math.Min(0.3, durationMonths / 12.0);

        // Activity recency bonus (0.2 if active)
        var activityBonus = isActive ? 0.2 : 0.0;

        // Trust component (starts at 0.5, increases with consistent activity)
        var trustScore = Math.Min(1.0, 0.5 + (activityScore * 0.5));

        return Math.Min(1.0, activityScore + durationBonus + activityBonus) * trustScore;
    }

    private double CalculateTrustScore(PodMember member)
    {
        // Simplified trust calculation based on membership status
        var baseTrust = 0.5;

        // Role-based trust (owners/mods get higher trust)
        var roleBonus = member.Role switch
        {
            "owner" => 0.3,
            "mod" => 0.2,
            _ => 0.0
        };

        // No bans = trust bonus
        var cleanRecordBonus = !member.IsBanned ? 0.2 : 0.0;

        return Math.Min(1.0, baseTrust + roleBonus + cleanRecordBonus);
    }

    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (valueList.Count <= 1) return 0;

        var average = valueList.Average();
        var sumOfSquaresOfDifferences = valueList.Sum(val => Math.Pow(val - average, 2));
        return Math.Sqrt(sumOfSquaresOfDifferences / (valueList.Count - 1));
    }

    private double CalculateConsensusStrength(IReadOnlyList<VariantAggregate> variants)
    {
        if (!variants.Any()) return 0;

        // Consensus strength based on score distribution and agreement
        var scores = variants.Select(v => v.WeightedAverageScore).ToList();
        var stdDev = CalculateStandardDeviation(scores);

        // Lower standard deviation = higher consensus
        var consensusFromVariance = Math.Max(0, 1.0 - (stdDev / 5.0)); // Normalize against max expected variance

        // Weight by number of opinions
        var totalOpinions = variants.Sum(v => v.OpinionCount);
        var opinionWeight = Math.Min(1.0, totalOpinions / 10.0); // Full weight at 10+ opinions

        return consensusFromVariance * opinionWeight;
    }

    private VariantRecommendation GenerateRecommendation(VariantAggregate variant, AggregatedOpinions aggregated)
    {
        var score = variant.WeightedAverageScore;
        var consensusScore = score / 10.0; // Normalize to 0-1

        var factors = new List<string>();

        if (variant.OpinionCount >= 3)
            factors.Add($"Based on {variant.OpinionCount} opinions");
        else
            factors.Add("Limited opinion data");

        if (variant.ScoreStandardDeviation < 1.0)
            factors.Add("High agreement among reviewers");
        else if (variant.ScoreStandardDeviation < 2.0)
            factors.Add("Moderate agreement among reviewers");
        else
            factors.Add("Mixed opinions");

        var affinityWeight = variant.AffinityWeightSum / variant.OpinionCount;
        if (affinityWeight > 0.7)
            factors.Add("Strong reviewer credibility");
        else if (affinityWeight > 0.4)
            factors.Add("Moderate reviewer credibility");

        RecommendationLevel level;
        string reasoning;

        if (score >= 8.5)
        {
            level = RecommendationLevel.StronglyRecommended;
            reasoning = "Excellent quality with strong consensus";
        }
        else if (score >= 7.0)
        {
            level = RecommendationLevel.Recommended;
            reasoning = "Good quality with positive feedback";
        }
        else if (score >= 5.0)
        {
            level = RecommendationLevel.Neutral;
            reasoning = "Average quality or mixed opinions";
        }
        else if (score >= 3.0)
        {
            level = RecommendationLevel.NotRecommended;
            reasoning = "Below average quality";
        }
        else
        {
            level = RecommendationLevel.StronglyNotRecommended;
            reasoning = "Poor quality with negative consensus";
        }

        return new VariantRecommendation(
            VariantHash: variant.VariantHash,
            ConsensusScore: consensusScore,
            Recommendation: level,
            Reasoning: reasoning,
            SupportingFactors: factors);
    }

    private sealed record MemberMessageActivity(int MessageCount, DateTimeOffset LastMessageAt);
}
