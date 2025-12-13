// <copyright file="MediaCoreStatsService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for collecting and providing MediaCore statistics.
/// </summary>
public class MediaCoreStatsService : IMediaCoreStatsService
{
    private readonly ILogger<MediaCoreStatsService> _logger;
    private readonly IContentIdRegistry _contentRegistry;
    private readonly IDescriptorRetriever _descriptorRetriever;
    private readonly IFuzzyMatcher _fuzzyMatcher;
    private readonly IIpldMapper _ipldMapper;
    private readonly IPerceptualHasher _perceptualHasher;
    private readonly IMetadataPortability _metadataPortability;
    private readonly IContentDescriptorPublisher _contentPublisher;

    // Statistics tracking
    private readonly ConcurrentDictionary<string, int> _fuzzyMatchAttempts = new();
    private readonly ConcurrentDictionary<string, int> _fuzzyMatchSuccesses = new();
    private readonly ConcurrentDictionary<string, long> _hashComputationTimes = new();
    private readonly ConcurrentDictionary<string, int> _publicationsByDomain = new();
    private readonly ConcurrentDictionary<string, int> _retrievalsByDomain = new();

    private int _totalFuzzyMatches;
    private int _successfulFuzzyMatches;
    private long _totalHashComputationTimeNs;
    private int _totalHashesComputed;
    private int _totalExports;
    private int _totalImports;
    private int _successfulImports;
    private int _totalPublications;
    private int _activePublications;
    private int _failedPublications;

    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

    public MediaCoreStatsService(
        ILogger<MediaCoreStatsService> logger,
        IContentIdRegistry contentRegistry,
        IDescriptorRetriever descriptorRetriever,
        IFuzzyMatcher fuzzyMatcher,
        IIpldMapper ipldMapper,
        IPerceptualHasher perceptualHasher,
        IMetadataPortability metadataPortability,
        IContentDescriptorPublisher contentPublisher)
    {
        _logger = logger;
        _contentRegistry = contentRegistry;
        _descriptorRetriever = descriptorRetriever;
        _fuzzyMatcher = fuzzyMatcher;
        _ipldMapper = ipldMapper;
        _perceptualHasher = perceptualHasher;
        _metadataPortability = metadataPortability;
        _contentPublisher = contentPublisher;
    }

    /// <inheritdoc/>
    public async Task<MediaCoreStatsDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var uptime = _uptimeStopwatch.Elapsed;

        // Collect all statistics (some in parallel for performance)
        var contentRegistryTask = GetContentRegistryStatsAsync(cancellationToken);
        var descriptorTask = GetDescriptorStatsAsync(cancellationToken);
        var fuzzyTask = GetFuzzyMatchingStatsAsync(cancellationToken);
        var ipldTask = GetIpldMappingStatsAsync(cancellationToken);
        var perceptualTask = GetPerceptualHashingStatsAsync(cancellationToken);
        var portabilityTask = GetMetadataPortabilityStatsAsync(cancellationToken);
        var publishingTask = GetContentPublishingStatsAsync(cancellationToken);

        // Wait for all async tasks
        await Task.WhenAll(contentRegistryTask, descriptorTask, fuzzyTask, ipldTask,
                          perceptualTask, portabilityTask, publishingTask);

        // Get synchronous system resources
        var systemResources = GetSystemResourceStatsAsync(cancellationToken).GetAwaiter().GetResult();

        return new MediaCoreStatsDashboard(
            Timestamp: timestamp,
            Uptime: uptime,
            ContentRegistry: await contentRegistryTask,
            Descriptors: await descriptorTask,
            FuzzyMatching: await fuzzyTask,
            IpldMapping: await ipldTask,
            PerceptualHashing: await perceptualTask,
            MetadataPortability: await portabilityTask,
            ContentPublishing: await publishingTask,
            SystemResources: systemResources);
    }

    /// <inheritdoc/>
    public async Task<ContentRegistryStats> GetContentRegistryStatsAsync(CancellationToken cancellationToken = default)
    {
        var registryStats = await _contentRegistry.GetStatsAsync(cancellationToken);

        var mappingsByDomain = registryStats.MappingsByDomain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var mappingsByType = new Dictionary<string, int>(); // Would need to be tracked in registry

        var averageMappingsPerDomain = registryStats.TotalDomains > 0
            ? (double)registryStats.TotalMappings / registryStats.TotalDomains
            : 0.0;

        return new ContentRegistryStats(
            TotalMappings: registryStats.TotalMappings,
            TotalDomains: registryStats.TotalDomains,
            MappingsByDomain: mappingsByDomain,
            MappingsByType: mappingsByType,
            LastUpdated: DateTimeOffset.UtcNow, // Registry doesn't track this
            AverageMappingsPerDomain: averageMappingsPerDomain);
    }

    /// <inheritdoc/>
    public async Task<DescriptorStats> GetDescriptorStatsAsync(CancellationToken cancellationToken = default)
    {
        var descriptorStats = await _descriptorRetriever.GetStatsAsync(cancellationToken);

        return new DescriptorStats(
            TotalRetrievals: descriptorStats.TotalRetrievals,
            CacheHits: descriptorStats.CacheHits,
            CacheMisses: descriptorStats.CacheMisses,
            CacheHitRatio: descriptorStats.CacheHitRatio,
            AverageRetrievalTime: descriptorStats.AverageRetrievalTime,
            ActiveCacheEntries: descriptorStats.ActiveCacheEntries,
            CacheSizeBytes: descriptorStats.CacheSizeBytes,
            ExpiredEntriesCleaned: 0, // Would need to be tracked
            LastCacheCleanup: descriptorStats.LastCacheCleanup,
            RetrievalsByDomain: _retrievalsByDomain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    /// <inheritdoc/>
    public async Task<FuzzyMatchingStats> GetFuzzyMatchingStatsAsync(CancellationToken cancellationToken = default)
    {
        var accuracyByAlgorithm = new Dictionary<string, MatchAccuracyStats>
        {
            ["Levenshtein"] = new MatchAccuracyStats(0, 0, 0, 0, 0.0, 0.0, 0.0), // Placeholder
            ["Phonetic"] = new MatchAccuracyStats(0, 0, 0, 0, 0.0, 0.0, 0.0),     // Placeholder
            ["Perceptual"] = new MatchAccuracyStats(0, 0, 0, 0, 0.0, 0.0, 0.0)    // Placeholder
        };

        var matchesByDomain = _fuzzyMatchAttempts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var successRate = _totalFuzzyMatches > 0 ? (double)_successfulFuzzyMatches / _totalFuzzyMatches : 0.0;
        var averageConfidenceScore = 0.75; // Placeholder - would need to track actual scores

        return new FuzzyMatchingStats(
            TotalMatches: _totalFuzzyMatches,
            SuccessfulMatches: _successfulFuzzyMatches,
            SuccessRate: successRate,
            AverageMatchingTime: TimeSpan.FromMilliseconds(5), // Placeholder
            AccuracyByAlgorithm: accuracyByAlgorithm,
            MatchesByDomain: matchesByDomain,
            AverageConfidenceScore: averageConfidenceScore);
    }

    /// <inheritdoc/>
    public async Task<IpldMappingStats> GetIpldMappingStatsAsync(CancellationToken cancellationToken = default)
    {
        // This would need to be implemented in IpldMapper to track statistics
        // For now, return placeholder stats
        var linksByType = new Dictionary<string, int>
        {
            ["album"] = 0,
            ["artist"] = 0,
            ["parent"] = 0,
            ["children"] = 0
        };

        var graphsByRoot = new Dictionary<string, GraphStats>(); // Would need to be tracked

        return new IpldMappingStats(
            TotalLinks: 0,
            TotalNodes: 0,
            TotalGraphs: 0,
            LinksByType: linksByType,
            GraphsByRoot: graphsByRoot,
            BrokenLinksDetected: 0,
            OrphanedNodes: 0,
            AverageTraversalTime: TimeSpan.Zero,
            GraphConnectivityRatio: 0.0);
    }

    /// <inheritdoc/>
    public async Task<PerceptualHashingStats> GetPerceptualHashingStatsAsync(CancellationToken cancellationToken = default)
    {
        var statsByAlgorithm = new Dictionary<PerceptualHashAlgorithm, AlgorithmStats>
        {
            [PerceptualHashAlgorithm.Chromaprint] = new AlgorithmStats(
                HashesComputed: _totalHashesComputed / 3, // Placeholder distribution
                AverageTime: TimeSpan.FromMilliseconds(_totalHashesComputed > 0 ? _totalHashComputationTimeNs / (_totalHashesComputed * 1000000.0) : 0),
                Accuracy: 0.95, // Placeholder
                PerformanceByContentType: new Dictionary<string, double> { ["audio"] = 0.95 }),
            [PerceptualHashAlgorithm.PHash] = new AlgorithmStats(
                HashesComputed: _totalHashesComputed / 3,
                AverageTime: TimeSpan.FromMilliseconds(10),
                Accuracy: 0.90,
                PerformanceByContentType: new Dictionary<string, double> { ["image"] = 0.90 }),
            [PerceptualHashAlgorithm.Spectral] = new AlgorithmStats(
                HashesComputed: _totalHashesComputed / 3,
                AverageTime: TimeSpan.FromMilliseconds(5),
                Accuracy: 0.85,
                PerformanceByContentType: new Dictionary<string, double> { ["audio"] = 0.85 })
        };

        var hashesByContentType = new Dictionary<string, int>
        {
            ["audio"] = _totalHashesComputed * 2 / 3,
            ["image"] = _totalHashesComputed / 3
        };

        return new PerceptualHashingStats(
            TotalHashesComputed: _totalHashesComputed,
            AverageComputationTime: TimeSpan.FromMilliseconds(_totalHashesComputed > 0 ? _totalHashComputationTimeNs / (_totalHashesComputed * 1000000.0) : 0),
            StatsByAlgorithm: statsByAlgorithm,
            OverallAccuracy: 0.92,
            HashesByContentType: hashesByContentType,
            DuplicateHashesDetected: 0);
    }

    /// <inheritdoc/>
    public async Task<MetadataPortabilityStats> GetMetadataPortabilityStatsAsync(CancellationToken cancellationToken = default)
    {
        var conflictsByType = new Dictionary<string, int>
        {
            ["version"] = 0,
            ["codec"] = 0,
            ["size"] = 0,
            ["hash"] = 0
        };

        var resolutionsUsed = new Dictionary<ConflictResolutionStrategy, int>
        {
            [ConflictResolutionStrategy.Merge] = _successfulImports,
            [ConflictResolutionStrategy.Overwrite] = 0,
            [ConflictResolutionStrategy.Skip] = _totalImports - _successfulImports,
            [ConflictResolutionStrategy.KeepExisting] = 0
        };

        var importSuccessRate = _totalImports > 0 ? (double)_successfulImports / _totalImports : 0.0;

        return new MetadataPortabilityStats(
            TotalExports: _totalExports,
            TotalImports: _totalImports,
            SuccessfulImports: _successfulImports,
            ImportSuccessRate: importSuccessRate,
            ConflictsByType: conflictsByType,
            ResolutionsUsed: resolutionsUsed,
            AverageExportTime: TimeSpan.FromSeconds(0.5), // Placeholder
            AverageImportTime: TimeSpan.FromSeconds(1.0), // Placeholder
            TotalDataTransferred: 0); // Would need to be tracked
    }

    /// <inheritdoc/>
    public async Task<ContentPublishingStats> GetContentPublishingStatsAsync(CancellationToken cancellationToken = default)
    {
        var publishingStats = await _contentPublisher.GetStatsAsync(cancellationToken);

        return new ContentPublishingStats(
            TotalPublished: publishingStats.TotalPublishedDescriptors,
            ActivePublications: publishingStats.ActivePublications,
            ExpiredPublications: publishingStats.ExpiringSoon, // Approximate
            PublicationSuccessRate: 0.95, // Placeholder - would need to be tracked
            PublicationsByDomain: _publicationsByDomain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            AveragePublishTime: TimeSpan.FromSeconds(2), // Placeholder
            RepublishedDescriptors: 0, // Would need to be tracked
            FailedPublications: 0, // Would need to be tracked
            RecentErrors: new Dictionary<string, string>()); // Would need to be tracked
    }

    /// <inheritdoc/>
    public async Task ResetStatsAsync(CancellationToken cancellationToken = default)
    {
        _fuzzyMatchAttempts.Clear();
        _fuzzyMatchSuccesses.Clear();
        _hashComputationTimes.Clear();
        _publicationsByDomain.Clear();
        _retrievalsByDomain.Clear();

        _totalFuzzyMatches = 0;
        _successfulFuzzyMatches = 0;
        _totalHashComputationTimeNs = 0;
        _totalHashesComputed = 0;
        _totalExports = 0;
        _totalImports = 0;
        _successfulImports = 0;
        _totalPublications = 0;
        _activePublications = 0;
        _failedPublications = 0;

        _logger.LogInformation("[MediaCoreStats] All statistics have been reset");
    }

    private static async Task<SystemResourceStats> GetSystemResourceStatsAsync(CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();

        return new SystemResourceStats(
            WorkingSetBytes: process.WorkingSet64,
            PrivateMemoryBytes: process.PrivateMemorySize64,
            CpuUsagePercent: 0.0, // Would need performance counter
            ThreadCount: process.Threads.Count,
            GcTotalMemoryBytes: GC.GetTotalMemory(false),
            GcCollectionsByGeneration: new Dictionary<int, int>
            {
                [0] = GC.CollectionCount(0),
                [1] = GC.CollectionCount(1),
                [2] = GC.CollectionCount(2)
            });
    }

    // Public methods for other components to report statistics
    public void RecordFuzzyMatchAttempt(string domain, bool success)
    {
        Interlocked.Increment(ref _totalFuzzyMatches);
        _fuzzyMatchAttempts.AddOrUpdate(domain, 1, (_, count) => count + 1);

        if (success)
        {
            Interlocked.Increment(ref _successfulFuzzyMatches);
            _fuzzyMatchSuccesses.AddOrUpdate(domain, 1, (_, count) => count + 1);
        }
    }

    public void RecordHashComputation(string algorithm, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalHashesComputed);
        Interlocked.Add(ref _totalHashComputationTimeNs, (long)(duration.TotalMilliseconds * 1000000));
        _hashComputationTimes.AddOrUpdate(algorithm, (long)duration.TotalMilliseconds,
            (_, total) => total + (long)duration.TotalMilliseconds);
    }

    public void RecordPublication(string domain, bool success)
    {
        Interlocked.Increment(ref _totalPublications);
        _publicationsByDomain.AddOrUpdate(domain, 1, (_, count) => count + 1);

        if (success)
        {
            Interlocked.Increment(ref _activePublications);
        }
        else
        {
            Interlocked.Increment(ref _failedPublications);
        }
    }

    public void RecordRetrieval(string domain)
    {
        _retrievalsByDomain.AddOrUpdate(domain, 1, (_, count) => count + 1);
    }

    public void RecordExport(bool success)
    {
        Interlocked.Increment(ref _totalExports);
    }

    public void RecordImport(bool success)
    {
        Interlocked.Increment(ref _totalImports);
        if (success)
        {
            Interlocked.Increment(ref _successfulImports);
        }
    }
}
