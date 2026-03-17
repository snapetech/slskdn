// <copyright file="IMediaCoreSwarmIntelligence.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for providing swarm intelligence and optimization recommendations.
/// </summary>
public interface IMediaCoreSwarmIntelligence
{
    /// <summary>
    /// Gets swarm intelligence for download optimization.
    /// </summary>
    /// <param name="contentId">The ContentID being downloaded.</param>
    /// <param name="activePeers">Currently active peers in the swarm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Swarm intelligence and optimization recommendations.</returns>
    Task<SwarmIntelligence> GetSwarmIntelligenceAsync(
        string contentId,
        IEnumerable<string> activePeers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes swarm performance and provides optimization recommendations.
    /// </summary>
    /// <param name="contentId">The ContentID being downloaded.</param>
    /// <param name="swarmMetrics">Current swarm metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Performance analysis and recommendations.</returns>
    Task<SwarmPerformanceAnalysis> AnalyzeSwarmPerformanceAsync(
        string contentId,
        SwarmMetrics swarmMetrics,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Predicts optimal swarm configuration based on content characteristics.
    /// </summary>
    /// <param name="contentId">The ContentID to analyze.</param>
    /// <param name="availablePeers">Available peers for the swarm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Predicted optimal swarm configuration.</returns>
    Task<SwarmPrediction> PredictOptimalConfigurationAsync(
        string contentId,
        IEnumerable<PeerCapability> availablePeers,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Swarm performance metrics.
/// </summary>
public record SwarmMetrics(
    double CurrentSpeed,
    double AveragePeerSpeed,
    int ActivePeerCount,
    int TotalPeerCount,
    double QualityScore,
    TimeSpan ElapsedTime,
    IReadOnlyDictionary<string, double> PeerPerformance);

/// <summary>
/// Peer capability information.
/// </summary>
public record PeerCapability(
    string Username,
    double AverageSpeed,
    IReadOnlyList<string> SupportedContentIds,
    PeerReliability Reliability,
    TimeSpan AverageResponseTime);

/// <summary>
/// Peer reliability rating.
/// </summary>
public enum PeerReliability
{
    /// <summary>
    /// Highly reliable peer.
    /// </summary>
    Excellent,

    /// <summary>
    /// Generally reliable peer.
    /// </summary>
    Good,

    /// <summary>
    /// Moderately reliable peer.
    /// </summary>
    Fair,

    /// <summary>
    /// Unreliable peer.
    /// </summary>
    Poor,

    /// <summary>
    /// Known problematic peer.
    /// </summary>
    Unreliable
}

/// <summary>
/// Swarm performance analysis.
/// </summary>
public record SwarmPerformanceAnalysis(
    SwarmPerformanceRating Rating,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Recommendations,
    SwarmOptimizationAdvice OptimizationAdvice);

/// <summary>
/// Swarm performance rating.
/// </summary>
public enum SwarmPerformanceRating
{
    /// <summary>
    /// Swarm is performing optimally.
    /// </summary>
    Excellent,

    /// <summary>
    /// Swarm is performing well.
    /// </summary>
    Good,

    /// <summary>
    /// Swarm performance is acceptable.
    /// </summary>
    Acceptable,

    /// <summary>
    /// Swarm needs improvement.
    /// </summary>
    Poor,

    /// <summary>
    /// Swarm has critical performance issues.
    /// </summary>
    Critical
}

/// <summary>
/// Swarm configuration prediction.
/// </summary>
public record SwarmPrediction(
    SwarmStrategy RecommendedStrategy,
    int OptimalPeerCount,
    IReadOnlyList<string> RecommendedPeers,
    double PredictedSpeed,
    double PredictedQuality,
    string Reasoning);
