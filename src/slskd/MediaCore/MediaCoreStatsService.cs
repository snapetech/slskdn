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

        // Get system resources
        var systemResources = await GetSystemResourceStatsAsync(cancellationToken);

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
        var mappingsByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var domain in mappingsByDomain.Keys)
        {
            var contentIds = await _contentRegistry.FindByDomainAsync(domain, cancellationToken);
            foreach (var contentId in contentIds)
            {
                var type = ContentIdParser.GetType(contentId) ?? "unknown";
                mappingsByType.TryGetValue(type, out var count);
                mappingsByType[type] = count + 1;
            }
        }

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
    public Task<FuzzyMatchingStats> GetFuzzyMatchingStatsAsync(CancellationToken cancellationToken = default)
    {
        var accuracyByAlgorithm = new Dictionary<string, MatchAccuracyStats>(StringComparer.OrdinalIgnoreCase);
        var matchesByDomain = _fuzzyMatchAttempts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var successRate = _totalFuzzyMatches > 0 ? (double)_successfulFuzzyMatches / _totalFuzzyMatches : 0.0;
        var averageConfidenceScore = successRate;

        foreach (var (domain, attempts) in _fuzzyMatchAttempts)
        {
            _fuzzyMatchSuccesses.TryGetValue(domain, out var successes);
            var ratio = attempts > 0 ? (double)successes / attempts : 0.0;
            accuracyByAlgorithm[$"conservative:{domain}"] = new MatchAccuracyStats(
                TotalAttempts: attempts,
                CorrectMatches: successes,
                FalsePositives: 0,
                FalseNegatives: Math.Max(0, attempts - successes),
                Precision: ratio,
                Recall: ratio,
                F1Score: ratio);
        }

        accuracyByAlgorithm["conservative"] = new MatchAccuracyStats(
            TotalAttempts: _totalFuzzyMatches,
            CorrectMatches: _successfulFuzzyMatches,
            FalsePositives: 0,
            FalseNegatives: Math.Max(0, _totalFuzzyMatches - _successfulFuzzyMatches),
            Precision: successRate,
            Recall: successRate,
            F1Score: successRate);

        return Task.FromResult(new FuzzyMatchingStats(
            TotalMatches: _totalFuzzyMatches,
            SuccessfulMatches: _successfulFuzzyMatches,
            SuccessRate: successRate,
            AverageMatchingTime: TimeSpan.Zero,
            AccuracyByAlgorithm: accuracyByAlgorithm,
            MatchesByDomain: matchesByDomain,
            AverageConfidenceScore: averageConfidenceScore));
    }

    /// <inheritdoc/>
    public async Task<IpldMappingStats> GetIpldMappingStatsAsync(CancellationToken cancellationToken = default)
    {
        var registryStats = await _contentRegistry.GetStatsAsync(cancellationToken);
        var contentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var domain in registryStats.MappingsByDomain.Keys)
        {
            foreach (var contentId in await _contentRegistry.FindByDomainAsync(domain, cancellationToken))
            {
                contentIds.Add(contentId);
            }
        }

        var linksByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var graphsByRoot = new Dictionary<string, GraphStats>(StringComparer.OrdinalIgnoreCase);
        var seenNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connectedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orphanedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalLinks = 0;
        var totalTraversalTime = TimeSpan.Zero;

        foreach (var contentId in contentIds)
        {
            var started = Stopwatch.GetTimestamp();
            var graph = await _ipldMapper.GetGraphAsync(contentId, 1, cancellationToken);
            totalTraversalTime += Stopwatch.GetElapsedTime(started);

            var graphLinkCount = 0;
            var maxDepth = 0;

            foreach (var node in graph.Nodes)
            {
                seenNodes.Add(node.ContentId);

                if (node.OutgoingLinks.Count == 0 && node.IncomingLinks.Count == 0)
                {
                    orphanedNodes.Add(node.ContentId);
                }
                else
                {
                    connectedNodes.Add(node.ContentId);
                }

                foreach (var link in node.OutgoingLinks)
                {
                    graphLinkCount++;
                    totalLinks++;
                    linksByType.TryGetValue(link.Name, out var count);
                    linksByType[link.Name] = count + 1;
                }
            }

            foreach (var path in graph.Paths)
            {
                maxDepth = Math.Max(maxDepth, Math.Max(0, path.ContentIds.Count - 1));
            }

            var rootGraphConnectivityRatio = graph.Nodes.Count > 0
                ? (double)graph.Nodes.Count(n => n.OutgoingLinks.Count > 0 || n.IncomingLinks.Count > 0) / graph.Nodes.Count
                : 0.0;

            graphsByRoot[contentId] = new GraphStats(
                RootContentId: contentId,
                NodeCount: graph.Nodes.Count,
                LinkCount: graphLinkCount,
                MaxDepth: maxDepth,
                ConnectivityRatio: rootGraphConnectivityRatio);
        }

        var validation = await _ipldMapper.ValidateLinksAsync(cancellationToken);

        var averageTraversalTime = contentIds.Count > 0
            ? TimeSpan.FromTicks(totalTraversalTime.Ticks / contentIds.Count)
            : TimeSpan.Zero;
        var overallGraphConnectivityRatio = seenNodes.Count > 0
            ? (double)connectedNodes.Count / seenNodes.Count
            : 0.0;

        return new IpldMappingStats(
            TotalLinks: totalLinks,
            TotalNodes: seenNodes.Count,
            TotalGraphs: graphsByRoot.Count,
            LinksByType: linksByType,
            GraphsByRoot: graphsByRoot,
            BrokenLinksDetected: validation.BrokenLinks.Count,
            OrphanedNodes: orphanedNodes.Count,
            AverageTraversalTime: averageTraversalTime,
            GraphConnectivityRatio: overallGraphConnectivityRatio);
    }

    /// <inheritdoc/>
    public Task<PerceptualHashingStats> GetPerceptualHashingStatsAsync(CancellationToken cancellationToken = default)
    {
        var statsByAlgorithm = new Dictionary<PerceptualHashAlgorithm, AlgorithmStats>();
        foreach (var (algorithmName, totalMilliseconds) in _hashComputationTimes)
        {
            if (!Enum.TryParse<PerceptualHashAlgorithm>(algorithmName, true, out var algorithm))
            {
                continue;
            }

            statsByAlgorithm[algorithm] = new AlgorithmStats(
                HashesComputed: 1,
                AverageTime: TimeSpan.FromMilliseconds(totalMilliseconds),
                Accuracy: 0.0,
                PerformanceByContentType: new Dictionary<string, double>());
        }

        return Task.FromResult(new PerceptualHashingStats(
            TotalHashesComputed: _totalHashesComputed,
            AverageComputationTime: TimeSpan.FromMilliseconds(_totalHashesComputed > 0 ? _totalHashComputationTimeNs / (_totalHashesComputed * 1000000.0) : 0),
            StatsByAlgorithm: statsByAlgorithm,
            OverallAccuracy: 0.0,
            HashesByContentType: new Dictionary<string, int>(),
            DuplicateHashesDetected: 0));
    }

    /// <inheritdoc/>
    public Task<MetadataPortabilityStats> GetMetadataPortabilityStatsAsync(CancellationToken cancellationToken = default)
    {
        var resolutionsUsed = new Dictionary<ConflictResolutionStrategy, int>
        {
            [ConflictResolutionStrategy.Merge] = _successfulImports,
            [ConflictResolutionStrategy.Overwrite] = 0,
            [ConflictResolutionStrategy.Skip] = _totalImports - _successfulImports,
            [ConflictResolutionStrategy.KeepExisting] = 0
        };

        var importSuccessRate = _totalImports > 0 ? (double)_successfulImports / _totalImports : 0.0;

        return Task.FromResult(new MetadataPortabilityStats(
            TotalExports: _totalExports,
            TotalImports: _totalImports,
            SuccessfulImports: _successfulImports,
            ImportSuccessRate: importSuccessRate,
            ConflictsByType: new Dictionary<string, int>(),
            ResolutionsUsed: resolutionsUsed,
            AverageExportTime: TimeSpan.Zero,
            AverageImportTime: TimeSpan.Zero,
            TotalDataTransferred: 0));
    }

    /// <inheritdoc/>
    public async Task<ContentPublishingStats> GetContentPublishingStatsAsync(CancellationToken cancellationToken = default)
    {
        var publishingStats = await _contentPublisher.GetStatsAsync(cancellationToken);
        var totalPublicationAttempts = publishingStats.TotalPublishedDescriptors + _failedPublications;
        var publicationSuccessRate = totalPublicationAttempts > 0
            ? (double)publishingStats.TotalPublishedDescriptors / totalPublicationAttempts
            : 0.0;
        var publicationsByDomain = publishingStats.PublicationsByDomain.Count > 0
            ? publishingStats.PublicationsByDomain
            : _publicationsByDomain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new ContentPublishingStats(
            TotalPublished: publishingStats.TotalPublishedDescriptors,
            ActivePublications: publishingStats.ActivePublications,
            ExpiredPublications: publishingStats.ExpiringSoon, // Approximate
            PublicationSuccessRate: publicationSuccessRate,
            PublicationsByDomain: publicationsByDomain,
            AveragePublishTime: TimeSpan.Zero,
            RepublishedDescriptors: 0, // Would need to be tracked
            FailedPublications: _failedPublications,
            RecentErrors: new Dictionary<string, string>());
    }

    /// <inheritdoc/>
    public Task ResetStatsAsync(CancellationToken cancellationToken = default)
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
        return Task.CompletedTask;
    }

    private static Task<SystemResourceStats> GetSystemResourceStatsAsync(CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();

        return Task.FromResult(new SystemResourceStats(
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
            }));
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
