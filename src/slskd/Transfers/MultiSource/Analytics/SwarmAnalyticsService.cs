// <copyright file="SwarmAnalyticsService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Analytics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Transfers.MultiSource.Metrics;
using slskd.Telemetry;
using static slskd.Telemetry.SwarmMetrics;
using static slskd.Telemetry.PeerMetrics;

/// <summary>
///     Service for swarm analytics and reporting.
/// </summary>
public class SwarmAnalyticsService : ISwarmAnalyticsService
{
    private readonly IPeerMetricsService _peerMetricsService;
    private readonly IMultiSourceDownloadService _downloadService;
    private readonly ILogger<SwarmAnalyticsService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SwarmAnalyticsService"/> class.
    /// </summary>
    public SwarmAnalyticsService(
        IPeerMetricsService peerMetricsService,
        IMultiSourceDownloadService downloadService,
        ILogger<SwarmAnalyticsService> logger)
    {
        _peerMetricsService = peerMetricsService;
        _downloadService = downloadService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SwarmPerformanceMetrics> GetPerformanceMetricsAsync(TimeSpan? timeWindow = null, CancellationToken cancellationToken = default)
    {
        timeWindow ??= TimeSpan.FromHours(24);

        try
        {
            // Get metrics from Prometheus (simplified - in production, query Prometheus API)
            // For now, we'll use the metric values directly if available
            var metrics = new SwarmPerformanceMetrics
            {
                TimeWindow = timeWindow.Value,
            };

            // Get active downloads count
            metrics.TotalDownloads = (long)SwarmDownloadsActive.Value;

            // Calculate success rate from counters (simplified)
            var started = SwarmDownloadsTotal.WithLabels("started").Value;
            var success = SwarmDownloadsTotal.WithLabels("success").Value;
            var failed = SwarmDownloadsTotal.WithLabels("failed").Value;

            metrics.SuccessfulDownloads = (long)success;
            metrics.FailedDownloads = (long)failed;
            metrics.TotalDownloads = (long)(started + success + failed);

            if (metrics.TotalDownloads > 0)
            {
                metrics.SuccessRate = (double)success / metrics.TotalDownloads;
            }

            // Get average duration from histogram (simplified - would need Prometheus query in production)
            // For now, use default values
            metrics.AverageDurationSeconds = 60.0; // Placeholder
            metrics.AverageSpeedBytesPerSecond = 1024 * 1024; // 1 MB/s placeholder
            metrics.AverageSourcesUsed = 3.0; // Placeholder

            // Get total bytes downloaded
            metrics.TotalBytesDownloaded = (long)SwarmBytesDownloadedTotal.Value;

            // Get chunk metrics
            var chunksSuccess = SwarmChunksCompletedTotal.WithLabels("success").Value;
            var chunksFailed = SwarmChunksCompletedTotal.WithLabels("failed").Value;
            var chunksTimeout = SwarmChunksCompletedTotal.WithLabels("timeout").Value;
            var chunksCorrupted = SwarmChunksCompletedTotal.WithLabels("corrupted").Value;

            metrics.TotalChunksCompleted = (long)(chunksSuccess + chunksFailed + chunksTimeout + chunksCorrupted);
            if (metrics.TotalChunksCompleted > 0)
            {
                metrics.ChunkSuccessRate = (double)chunksSuccess / metrics.TotalChunksCompleted;
            }

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics");
            return new SwarmPerformanceMetrics { TimeWindow = timeWindow.Value };
        }
    }

    /// <inheritdoc/>
    public async Task<List<PeerPerformanceRanking>> GetPeerRankingsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var rankedPeers = await _peerMetricsService.GetRankedPeersAsync(limit, cancellationToken).ConfigureAwait(false);

            var rankings = new List<PeerPerformanceRanking>();
            int rank = 1;

            foreach (var peer in rankedPeers)
            {
                var ranking = new PeerPerformanceRanking
                {
                    PeerId = peer.PeerId,
                    Source = peer.Source.ToString().ToLowerInvariant(),
                    ReputationScore = peer.ReputationScore,
                    AverageRttMs = peer.RttAvgMs,
                    AverageThroughputBytesPerSecond = peer.ThroughputAvgBytesPerSec,
                    ChunksCompleted = peer.ChunksCompleted,
                    ChunksFailed = peer.ChunksFailed,
                    TotalBytesTransferred = peer.TotalBytesTransferred,
                    Rank = rank++,
                };

                var totalChunks = peer.ChunksCompleted + peer.ChunksFailed + peer.ChunksTimedOut + peer.ChunksCorrupted;
                if (totalChunks > 0)
                {
                    ranking.ChunkSuccessRate = (double)peer.ChunksCompleted / totalChunks;
                }

                rankings.Add(ranking);
            }

            return rankings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting peer rankings");
            return new List<PeerPerformanceRanking>();
        }
    }

    /// <inheritdoc/>
    public async Task<SwarmEfficiencyMetrics> GetEfficiencyMetricsAsync(TimeSpan? timeWindow = null, CancellationToken cancellationToken = default)
    {
        timeWindow ??= TimeSpan.FromHours(24);

        try
        {
            // Calculate efficiency metrics
            var metrics = new SwarmEfficiencyMetrics();

            // Get active downloads to calculate utilization
            var activeDownloads = _downloadService.ActiveDownloads.Count;
            var totalDownloads = (long)SwarmDownloadsTotal.WithLabels("started").Value;
            if (totalDownloads > 0)
            {
                metrics.ChunkUtilization = Math.Min(1.0, (double)activeDownloads / totalDownloads);
            }

            // Get peer metrics for utilization
            var rankedPeers = await _peerMetricsService.GetRankedPeersAsync(100, CancellationToken.None).ConfigureAwait(false);
            var activePeers = rankedPeers.Count(p => p.ChunksCompleted > 0 || p.ChunksFailed > 0);
            if (rankedPeers.Count > 0)
            {
                metrics.PeerUtilization = (double)activePeers / rankedPeers.Count;
            }

            // Calculate redundancy (simplified)
            // Note: Prometheus Histogram doesn't expose .Value directly
            // In production, would query Prometheus API for actual histogram statistics
            metrics.RedundancyFactor = 1.5; // Placeholder - would calculate from actual data

            // Placeholder values (would need historical data)
            metrics.AverageTimeToFirstByteMs = 100.0;
            metrics.AverageReassignmentRate = 0.1;
            metrics.AverageRescueRate = 0.05;

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting efficiency metrics");
            return new SwarmEfficiencyMetrics();
        }
    }

    /// <inheritdoc/>
    public async Task<SwarmTrends> GetTrendsAsync(TimeSpan timeWindow, int dataPoints = 24, CancellationToken cancellationToken = default)
    {
        try
        {
            // For now, return placeholder trends
            // In production, this would query historical data from a time-series database
            var trends = new SwarmTrends();
            var interval = TimeSpan.FromMilliseconds(timeWindow.TotalMilliseconds / dataPoints);
            var now = DateTime.UtcNow;

            for (int i = 0; i < dataPoints; i++)
            {
                var timePoint = now - TimeSpan.FromMilliseconds((dataPoints - i) * interval.TotalMilliseconds);
                trends.TimePoints.Add(timePoint);
                trends.SuccessRates.Add(0.95); // Placeholder
                trends.AverageSpeeds.Add(1024 * 1024); // 1 MB/s placeholder
                trends.AverageDurations.Add(60.0); // 60s placeholder
                trends.AverageSourcesUsed.Add(3.0); // 3 sources placeholder
                trends.DownloadCounts.Add(10); // 10 downloads placeholder
            }

            return await Task.FromResult(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trends");
            return new SwarmTrends();
        }
    }

    /// <inheritdoc/>
    public async Task<List<SwarmRecommendation>> GetRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var recommendations = new List<SwarmRecommendation>();

            // Get current metrics
            var performanceMetrics = await GetPerformanceMetricsAsync(TimeSpan.FromHours(24), cancellationToken).ConfigureAwait(false);
            var efficiencyMetrics = await GetEfficiencyMetricsAsync(TimeSpan.FromHours(24), cancellationToken).ConfigureAwait(false);
            var peerRankings = await GetPeerRankingsAsync(10, cancellationToken).ConfigureAwait(false);

            // Check success rate
            if (performanceMetrics.SuccessRate < 0.8)
            {
                recommendations.Add(new SwarmRecommendation
                {
                    Type = RecommendationType.PeerSelection,
                    Priority = RecommendationPriority.High,
                    Title = "Low Success Rate",
                    Description = $"Current success rate is {performanceMetrics.SuccessRate:P1}. Consider improving peer selection criteria.",
                    Action = "Review peer reputation thresholds and increase minimum reputation score for peer selection.",
                    EstimatedImpact = 0.3,
                });
            }

            // Check chunk success rate
            if (performanceMetrics.ChunkSuccessRate < 0.9)
            {
                recommendations.Add(new SwarmRecommendation
                {
                    Type = RecommendationType.ChunkSize,
                    Priority = RecommendationPriority.Medium,
                    Title = "High Chunk Failure Rate",
                    Description = $"Chunk success rate is {performanceMetrics.ChunkSuccessRate:P1}. Consider adjusting chunk size.",
                    Action = "Try reducing chunk size to improve reliability, or increase timeout values.",
                    EstimatedImpact = 0.2,
                });
            }

            // Check peer utilization
            if (efficiencyMetrics.PeerUtilization < 0.5)
            {
                recommendations.Add(new SwarmRecommendation
                {
                    Type = RecommendationType.SourceCount,
                    Priority = RecommendationPriority.Low,
                    Title = "Low Peer Utilization",
                    Description = $"Only {efficiencyMetrics.PeerUtilization:P1} of available peers are being utilized.",
                    Action = "Consider increasing the number of sources per download to improve redundancy.",
                    EstimatedImpact = 0.15,
                });
            }

            // Check for low-reputation peers
            var lowReputationPeers = peerRankings.Count(p => p.ReputationScore < 0.5);
            if (lowReputationPeers > 0)
            {
                recommendations.Add(new SwarmRecommendation
                {
                    Type = RecommendationType.PeerSelection,
                    Priority = RecommendationPriority.Medium,
                    Title = "Low-Reputation Peers Detected",
                    Description = $"{lowReputationPeers} peers have reputation scores below 0.5.",
                    Action = "Consider blacklisting or deprioritizing low-reputation peers to improve overall performance.",
                    EstimatedImpact = 0.25,
                });
            }

            // Check average speed
            var speedMbps = performanceMetrics.AverageSpeedBytesPerSecond / (1024.0 * 1024.0);
            if (speedMbps < 0.5)
            {
                recommendations.Add(new SwarmRecommendation
                {
                    Type = RecommendationType.NetworkConfig,
                    Priority = RecommendationPriority.High,
                    Title = "Low Download Speed",
                    Description = $"Average download speed is {speedMbps:F2} MB/s. This may indicate network or peer issues.",
                    Action = "Check network connectivity, firewall settings, and consider using more sources per download.",
                    EstimatedImpact = 0.4,
                });
            }

            return recommendations.OrderByDescending(r => r.Priority).ThenByDescending(r => r.EstimatedImpact).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations");
            return new List<SwarmRecommendation>();
        }
    }
}
