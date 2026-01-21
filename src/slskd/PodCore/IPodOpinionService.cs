// <copyright file="IPodOpinionService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Service for managing pod member opinions on content variants.
/// </summary>
public interface IPodOpinionService
{
    /// <summary>
    ///     Publishes a pod member's opinion on a content variant to the DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="opinion">The opinion to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The published opinion result.</returns>
    Task<OpinionPublishResult> PublishOpinionAsync(string podId, PodVariantOpinion opinion, CancellationToken ct = default);

    /// <summary>
    ///     Retrieves all opinions for a specific content item from a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The list of opinions for the content.</returns>
    Task<IReadOnlyList<PodVariantOpinion>> GetOpinionsAsync(string podId, string contentId, CancellationToken ct = default);

    /// <summary>
    ///     Retrieves opinions for a specific content variant from a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="variantHash">The variant hash.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The list of opinions for the specific variant.</returns>
    Task<IReadOnlyList<PodVariantOpinion>> GetVariantOpinionsAsync(string podId, string contentId, string variantHash, CancellationToken ct = default);

    /// <summary>
    ///     Validates an opinion before publishing.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="opinion">The opinion to validate.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Validation result.</returns>
    Task<OpinionValidationResult> ValidateOpinionAsync(string podId, PodVariantOpinion opinion, CancellationToken ct = default);

    /// <summary>
    ///     Gets aggregated opinion statistics for a content item.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Aggregated opinion statistics.</returns>
    Task<OpinionStatistics> GetOpinionStatisticsAsync(string podId, string contentId, CancellationToken ct = default);

    /// <summary>
    ///     Refreshes opinions for a pod from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The refresh result.</returns>
    Task<OpinionRefreshResult> RefreshOpinionsAsync(string podId, CancellationToken ct = default);
}

/// <summary>
///     Result of publishing an opinion.
/// </summary>
public record OpinionPublishResult(
    bool Success,
    string PodId,
    string ContentId,
    string VariantHash,
    string? ErrorMessage = null,
    PodVariantOpinion? PublishedOpinion = null);

/// <summary>
///     Result of validating an opinion.
/// </summary>
public record OpinionValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    PodVariantOpinion? ValidatedOpinion = null);

/// <summary>
///     Aggregated opinion statistics for content.
/// </summary>
public record OpinionStatistics(
    string PodId,
    string ContentId,
    int TotalOpinions,
    int UniqueVariants,
    double AverageScore,
    double MinScore,
    double MaxScore,
    Dictionary<string, int> ScoreDistribution,
    DateTimeOffset LastUpdated);

/// <summary>
///     Result of refreshing opinions from DHT.
/// </summary>
public record OpinionRefreshResult(
    bool Success,
    string PodId,
    int OpinionsRefreshed,
    int NewOpinions,
    TimeSpan Duration,
    string? ErrorMessage = null);
