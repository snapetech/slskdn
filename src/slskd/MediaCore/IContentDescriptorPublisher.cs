// <copyright file="IContentDescriptorPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore;

/// <summary>
/// Advanced content descriptor publishing service with versioning and batch operations.
/// </summary>
public interface IContentDescriptorPublisher
{
    /// <summary>
    /// Publish a single content descriptor with versioning support.
    /// </summary>
    /// <param name="descriptor">The descriptor to publish.</param>
    /// <param name="forceUpdate">Whether to force update even if version is not newer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing result with version information.</returns>
    Task<DescriptorPublishResult> PublishAsync(ContentDescriptor descriptor, bool forceUpdate = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish multiple content descriptors in batch.
    /// </summary>
    /// <param name="descriptors">The descriptors to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch publishing results.</returns>
    Task<BatchPublishResult> PublishBatchAsync(IEnumerable<ContentDescriptor> descriptors, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing published descriptor with new metadata.
    /// </summary>
    /// <param name="contentId">The ContentID to update.</param>
    /// <param name="updates">The updates to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result.</returns>
    Task<DescriptorUpdateResult> UpdateAsync(string contentId, DescriptorUpdates updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Republish descriptors that are about to expire.
    /// </summary>
    /// <param name="contentIds">Specific ContentIDs to republish, or null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Republish results.</returns>
    Task<RepublishResult> RepublishExpiringAsync(IEnumerable<string>? contentIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublish a content descriptor from the DHT.
    /// </summary>
    /// <param name="contentId">The ContentID to unpublish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unpublish result.</returns>
    Task<UnpublishResult> UnpublishAsync(string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get publishing statistics and status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing statistics.</returns>
    Task<PublishingStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a descriptor publishing operation.
/// </summary>
public record DescriptorPublishResult(
    bool Success,
    string ContentId,
    string Version,
    DateTimeOffset PublishedAt,
    TimeSpan Ttl,
    string? ErrorMessage = null,
    bool WasUpdated = false,
    string? PreviousVersion = null);

/// <summary>
/// Result of a batch publishing operation.
/// </summary>
public record BatchPublishResult(
    int TotalRequested,
    int SuccessfullyPublished,
    int FailedToPublish,
    int Skipped,
    TimeSpan TotalDuration,
    IReadOnlyList<DescriptorPublishResult> Results);

/// <summary>
/// Updates to apply to an existing descriptor.
/// </summary>
public record DescriptorUpdates(
    string? NewCodec = null,
    long? NewSizeBytes = null,
    double? NewConfidence = null,
    IReadOnlyList<ContentHash>? AdditionalHashes = null,
    IReadOnlyList<PerceptualHash>? AdditionalPerceptualHashes = null,
    string? Notes = null);

/// <summary>
/// Result of a descriptor update operation.
/// </summary>
public record DescriptorUpdateResult(
    bool Success,
    string ContentId,
    string NewVersion,
    string PreviousVersion,
    IReadOnlyList<string> AppliedUpdates,
    string? ErrorMessage = null);

/// <summary>
/// Result of a republish operation.
/// </summary>
public record RepublishResult(
    int TotalChecked,
    int Republished,
    int Failed,
    int StillValid,
    TimeSpan Duration);

/// <summary>
/// Result of an unpublish operation.
/// </summary>
public record UnpublishResult(
    bool Success,
    string ContentId,
    bool WasPublished,
    string? ErrorMessage = null);

/// <summary>
/// Publishing statistics and status.
/// </summary>
public record PublishingStats(
    int TotalPublishedDescriptors,
    int ActivePublications,
    int ExpiringSoon,
    DateTimeOffset LastPublishOperation,
    IReadOnlyDictionary<string, int> PublicationsByDomain,
    long TotalStorageBytes,
    double AverageTtlHours);

