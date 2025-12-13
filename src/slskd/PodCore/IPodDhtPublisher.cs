// <copyright file="IPodDhtPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for publishing pod metadata to the DHT.
/// </summary>
public interface IPodDhtPublisher
{
    /// <summary>
    /// Publishes pod metadata to the DHT.
    /// </summary>
    /// <param name="pod">The pod to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish result.</returns>
    Task<PodPublishResult> PublishAsync(Pod pod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates existing pod metadata in the DHT.
    /// </summary>
    /// <param name="pod">The updated pod.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    Task<PodPublishResult> UpdateAsync(Pod pod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublishes pod metadata from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID to unpublish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unpublish result.</returns>
    Task<PodUnpublishResult> UnpublishAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current published metadata for a pod.
    /// </summary>
    /// <param name="podId">The pod ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pod metadata if found.</returns>
    Task<PodMetadataResult> GetPublishedMetadataAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes published pod metadata (republishes if expiring soon).
    /// </summary>
    /// <param name="podId">The pod ID to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    Task<PodRefreshResult> RefreshAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets publishing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing statistics.</returns>
    Task<PodPublishingStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a pod publish operation.
/// </summary>
public record PodPublishResult(
    bool Success,
    string PodId,
    string DhtKey,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt,
    string? ErrorMessage = null);

/// <summary>
/// Result of a pod unpublish operation.
/// </summary>
public record PodUnpublishResult(
    bool Success,
    string PodId,
    string DhtKey,
    string? ErrorMessage = null);

/// <summary>
/// Result of retrieving published pod metadata.
/// </summary>
public record PodMetadataResult(
    bool Found,
    string PodId,
    Pod PublishedPod,
    DateTimeOffset RetrievedAt,
    DateTimeOffset ExpiresAt,
    bool IsValidSignature,
    string? ErrorMessage = null);

/// <summary>
/// Result of a pod refresh operation.
/// </summary>
public record PodRefreshResult(
    bool Success,
    string PodId,
    bool WasRepublished,
    DateTimeOffset NextRefresh,
    string? ErrorMessage = null);

/// <summary>
/// Pod publishing statistics.
/// </summary>
public record PodPublishingStats(
    int TotalPublished,
    int ActivePublications,
    int ExpiredPublications,
    int FailedPublications,
    TimeSpan AveragePublishTime,
    IReadOnlyDictionary<string, int> PublicationsByDomain,
    IReadOnlyDictionary<PodVisibility, int> PublicationsByVisibility,
    DateTimeOffset LastPublishOperation);
