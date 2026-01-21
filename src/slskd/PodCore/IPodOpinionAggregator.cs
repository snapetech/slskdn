// <copyright file="IPodOpinionAggregator.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Service for aggregating and weighting pod member opinions based on affinity and trust.
/// </summary>
public interface IPodOpinionAggregator
{
    /// <summary>
    ///     Gets aggregated opinions for a content item with affinity weighting.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Aggregated opinion results with consensus metrics.</returns>
    Task<AggregatedOpinions> GetAggregatedOpinionsAsync(string podId, string contentId, CancellationToken ct = default);

    /// <summary>
    ///     Gets affinity scores for all pod members.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Member affinity scores.</returns>
    Task<IReadOnlyDictionary<string, MemberAffinity>> GetMemberAffinitiesAsync(string podId, CancellationToken ct = default);

    /// <summary>
    ///     Gets affinity score for a specific pod member.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="memberPeerId">The member peer ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The member's affinity score.</returns>
    Task<MemberAffinity> GetMemberAffinityAsync(string podId, string memberPeerId, CancellationToken ct = default);

    /// <summary>
    ///     Updates affinity scores for pod members based on recent activity.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The update result.</returns>
    Task<AffinityUpdateResult> UpdateMemberAffinitiesAsync(string podId, CancellationToken ct = default);

    /// <summary>
    ///     Gets consensus recommendations for content variants.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Consensus recommendations for variants.</returns>
    Task<IReadOnlyList<VariantRecommendation>> GetConsensusRecommendationsAsync(string podId, string contentId, CancellationToken ct = default);
}

/// <summary>
///     Aggregated opinions with consensus metrics.
/// </summary>
public record AggregatedOpinions(
    string PodId,
    string ContentId,
    double WeightedAverageScore,
    double UnweightedAverageScore,
    int TotalOpinions,
    int UniqueVariants,
    int ContributingMembers,
    double ConsensusStrength,
    IReadOnlyList<VariantAggregate> VariantAggregates,
    IReadOnlyDictionary<string, double> MemberContributions,
    DateTimeOffset LastUpdated);

/// <summary>
///     Aggregate statistics for a content variant.
/// </summary>
public record VariantAggregate(
    string VariantHash,
    double WeightedAverageScore,
    double UnweightedAverageScore,
    int OpinionCount,
    double ScoreStandardDeviation,
    double AffinityWeightSum,
    IReadOnlyList<WeightedOpinion> Opinions);

/// <summary>
///     A weighted opinion with affinity score.
/// </summary>
public record WeightedOpinion(
    PodVariantOpinion Opinion,
    double AffinityWeight,
    double WeightedScore);

/// <summary>
///     Member affinity score based on engagement and trust.
/// </summary>
public record MemberAffinity(
    string PeerId,
    double AffinityScore,
    int MessageCount,
    int OpinionCount,
    TimeSpan MembershipDuration,
    DateTimeOffset LastActivity,
    double TrustScore,
    IReadOnlyList<string> RecentActivity);

/// <summary>
///     Result of affinity score updates.
/// </summary>
public record AffinityUpdateResult(
    bool Success,
    string PodId,
    int MembersUpdated,
    TimeSpan Duration,
    string? ErrorMessage = null);

/// <summary>
///     Consensus recommendation for a content variant.
/// </summary>
public record VariantRecommendation(
    string VariantHash,
    double ConsensusScore,
    RecommendationLevel Recommendation,
    string Reasoning,
    IReadOnlyList<string> SupportingFactors);

/// <summary>
///     Recommendation confidence level.
/// </summary>
public enum RecommendationLevel
{
    /// <summary>
    ///     Strong recommendation - high consensus agreement.
    /// </summary>
    StronglyRecommended,

    /// <summary>
    ///     Moderate recommendation - good consensus.
    /// </summary>
    Recommended,

    /// <summary>
    ///     Neutral - mixed opinions or insufficient data.
    /// </summary>
    Neutral,

    /// <summary>
    ///     Not recommended - poor consensus.
    /// </summary>
    NotRecommended,

    /// <summary>
    ///     Strongly not recommended - very poor consensus.
    /// </summary>
    StronglyNotRecommended
}
