// <copyright file="IDescriptorRetriever.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore;

/// <summary>
/// Content descriptor retrieval service with caching and verification.
/// </summary>
public interface IDescriptorRetriever
{
    /// <summary>
    /// Retrieve a single content descriptor by ContentID.
    /// </summary>
    /// <param name="contentId">The ContentID to retrieve.</param>
    /// <param name="bypassCache">Whether to bypass the cache and force fresh retrieval.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The retrieval result with descriptor and metadata.</returns>
    Task<DescriptorRetrievalResult> RetrieveAsync(string contentId, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve multiple content descriptors in batch.
    /// </summary>
    /// <param name="contentIds">The ContentIDs to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch retrieval results.</returns>
    Task<BatchRetrievalResult> RetrieveBatchAsync(IEnumerable<string> contentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query descriptors by domain and type.
    /// </summary>
    /// <param name="domain">The content domain (audio, video, etc.).</param>
    /// <param name="type">The content type within the domain.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query results with matching descriptors.</returns>
    Task<DescriptorQueryResult> QueryByDomainAsync(string domain, string? type = null, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify the signature and freshness of a descriptor.
    /// </summary>
    /// <param name="descriptor">The descriptor to verify.</param>
    /// <param name="retrievedAt">When the descriptor was retrieved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result with validity assessment.</returns>
    Task<DescriptorVerificationResult> VerifyAsync(ContentDescriptor descriptor, DateTimeOffset retrievedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get retrieval statistics and cache performance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retrieval statistics.</returns>
    Task<RetrievalStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the retrieval cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cache clearing result.</returns>
    Task<CacheOperationResult> ClearCacheAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a descriptor retrieval operation.
/// </summary>
public record DescriptorRetrievalResult(
    bool Found,
    ContentDescriptor? Descriptor,
    DateTimeOffset RetrievedAt,
    TimeSpan RetrievalDuration,
    bool FromCache,
    DescriptorVerificationResult? Verification,
    string? ErrorMessage = null);

/// <summary>
/// Result of a batch retrieval operation.
/// </summary>
public record BatchRetrievalResult(
    int Requested,
    int Found,
    int Failed,
    TimeSpan TotalDuration,
    IReadOnlyList<DescriptorRetrievalResult> Results);

/// <summary>
/// Result of a descriptor query operation.
/// </summary>
public record DescriptorQueryResult(
    string Domain,
    string? Type,
    int TotalFound,
    TimeSpan QueryDuration,
    IReadOnlyList<ContentDescriptor> Descriptors,
    bool HasMoreResults);

/// <summary>
/// Result of descriptor verification.
/// </summary>
public record DescriptorVerificationResult(
    bool IsValid,
    bool SignatureValid,
    bool FreshnessValid,
    TimeSpan Age,
    string? ValidationError = null,
    IReadOnlyList<string> Warnings = null);

/// <summary>
/// Retrieval statistics.
/// </summary>
public record RetrievalStats(
    int TotalRetrievals,
    int CacheHits,
    int CacheMisses,
    double CacheHitRatio,
    TimeSpan AverageRetrievalTime,
    int ActiveCacheEntries,
    long CacheSizeBytes,
    DateTimeOffset LastCacheCleanup);

/// <summary>
/// Result of a cache operation.
/// </summary>
public record CacheOperationResult(
    bool Success,
    int EntriesCleared,
    long BytesFreed,
    string? ErrorMessage = null);

