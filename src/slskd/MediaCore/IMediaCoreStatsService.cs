// <copyright file="IMediaCoreStatsService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for collecting and providing MediaCore statistics.
/// </summary>
public interface IMediaCoreStatsService
{
    /// <summary>
    /// Gets comprehensive MediaCore statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete MediaCore statistics dashboard.</returns>
    Task<MediaCoreStatsDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content registry statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content registry statistics.</returns>
    Task<ContentRegistryStats> GetContentRegistryStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets descriptor retrieval and caching statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptor statistics.</returns>
    Task<DescriptorStats> GetDescriptorStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fuzzy matching performance statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fuzzy matching statistics.</returns>
    Task<FuzzyMatchingStats> GetFuzzyMatchingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets IPLD mapping and graph statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>IPLD mapping statistics.</returns>
    Task<IpldMappingStats> GetIpldMappingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets perceptual hashing performance statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Perceptual hashing statistics.</returns>
    Task<PerceptualHashingStats> GetPerceptualHashingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata portability statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata portability statistics.</returns>
    Task<MetadataPortabilityStats> GetMetadataPortabilityStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content publishing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content publishing statistics.</returns>
    Task<ContentPublishingStats> GetContentPublishingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all statistics counters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the reset operation.</returns>
    Task ResetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Comprehensive MediaCore statistics dashboard.
/// </summary>
public record MediaCoreStatsDashboard(
    DateTimeOffset Timestamp,
    TimeSpan Uptime,
    ContentRegistryStats ContentRegistry,
    DescriptorStats Descriptors,
    FuzzyMatchingStats FuzzyMatching,
    IpldMappingStats IpldMapping,
    PerceptualHashingStats PerceptualHashing,
    MetadataPortabilityStats MetadataPortability,
    ContentPublishingStats ContentPublishing,
    SystemResourceStats SystemResources);

/// <summary>
/// Content registry statistics.
/// </summary>
public record ContentRegistryStats(
    int TotalMappings,
    int TotalDomains,
    IReadOnlyDictionary<string, int> MappingsByDomain,
    IReadOnlyDictionary<string, int> MappingsByType,
    DateTimeOffset LastUpdated,
    double AverageMappingsPerDomain);

/// <summary>
/// Descriptor retrieval and caching statistics.
/// </summary>
public record DescriptorStats(
    int TotalRetrievals,
    int CacheHits,
    int CacheMisses,
    double CacheHitRatio,
    TimeSpan AverageRetrievalTime,
    int ActiveCacheEntries,
    long CacheSizeBytes,
    int ExpiredEntriesCleaned,
    DateTimeOffset LastCacheCleanup,
    IReadOnlyDictionary<string, int> RetrievalsByDomain);

/// <summary>
/// Fuzzy matching performance statistics.
/// </summary>
public record FuzzyMatchingStats(
    int TotalMatches,
    int SuccessfulMatches,
    double SuccessRate,
    TimeSpan AverageMatchingTime,
    IReadOnlyDictionary<string, MatchAccuracyStats> AccuracyByAlgorithm,
    IReadOnlyDictionary<string, int> MatchesByDomain,
    double AverageConfidenceScore);

/// <summary>
/// Match accuracy statistics for a specific algorithm.
/// </summary>
public record MatchAccuracyStats(
    int TotalAttempts,
    int CorrectMatches,
    int FalsePositives,
    int FalseNegatives,
    double Precision,
    double Recall,
    double F1Score);

/// <summary>
/// IPLD mapping and graph statistics.
/// </summary>
public record IpldMappingStats(
    int TotalLinks,
    int TotalNodes,
    int TotalGraphs,
    IReadOnlyDictionary<string, int> LinksByType,
    IReadOnlyDictionary<string, GraphStats> GraphsByRoot,
    int BrokenLinksDetected,
    int OrphanedNodes,
    TimeSpan AverageTraversalTime,
    double GraphConnectivityRatio);

/// <summary>
/// Statistics for a specific graph.
/// </summary>
public record GraphStats(
    string RootContentId,
    int NodeCount,
    int LinkCount,
    int MaxDepth,
    double ConnectivityRatio);

/// <summary>
/// Perceptual hashing performance statistics.
/// </summary>
public record PerceptualHashingStats(
    int TotalHashesComputed,
    TimeSpan AverageComputationTime,
    IReadOnlyDictionary<PerceptualHashAlgorithm, AlgorithmStats> StatsByAlgorithm,
    double OverallAccuracy,
    IReadOnlyDictionary<string, int> HashesByContentType,
    int DuplicateHashesDetected);

/// <summary>
/// Statistics for a specific perceptual hash algorithm.
/// </summary>
public record AlgorithmStats(
    int HashesComputed,
    TimeSpan AverageTime,
    double Accuracy,
    IReadOnlyDictionary<string, double> PerformanceByContentType);

/// <summary>
/// Metadata portability statistics.
/// </summary>
public record MetadataPortabilityStats(
    int TotalExports,
    int TotalImports,
    int SuccessfulImports,
    double ImportSuccessRate,
    IReadOnlyDictionary<string, int> ConflictsByType,
    IReadOnlyDictionary<ConflictResolutionStrategy, int> ResolutionsUsed,
    TimeSpan AverageExportTime,
    TimeSpan AverageImportTime,
    long TotalDataTransferred);

/// <summary>
/// Content publishing statistics.
/// </summary>
public record ContentPublishingStats(
    int TotalPublished,
    int ActivePublications,
    int ExpiredPublications,
    double PublicationSuccessRate,
    IReadOnlyDictionary<string, int> PublicationsByDomain,
    TimeSpan AveragePublishTime,
    int RepublishedDescriptors,
    int FailedPublications,
    IReadOnlyDictionary<string, string> RecentErrors);

/// <summary>
/// System resource statistics.
/// </summary>
public record SystemResourceStats(
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    double CpuUsagePercent,
    int ThreadCount,
    long GcTotalMemoryBytes,
    IReadOnlyDictionary<int, int> GcCollectionsByGeneration);

