// <copyright file="ISwarmAnalyticsService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Analytics;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Service for swarm analytics and reporting.
/// </summary>
public interface ISwarmAnalyticsService
{
    /// <summary>
    ///     Gets overall swarm performance metrics.
    /// </summary>
    Task<SwarmPerformanceMetrics> GetPerformanceMetricsAsync(TimeSpan? timeWindow = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets peer performance rankings.
    /// </summary>
    Task<List<PeerPerformanceRanking>> GetPeerRankingsAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets swarm efficiency metrics.
    /// </summary>
    Task<SwarmEfficiencyMetrics> GetEfficiencyMetricsAsync(TimeSpan? timeWindow = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets historical trends for swarm metrics.
    /// </summary>
    Task<SwarmTrends> GetTrendsAsync(TimeSpan timeWindow, int dataPoints = 24, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets recommendations for optimizing swarm performance.
    /// </summary>
    Task<List<SwarmRecommendation>> GetRecommendationsAsync(CancellationToken cancellationToken = default);
}
