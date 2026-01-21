// <copyright file="IMediaCoreSwarmService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using slskd.MediaCore;

/// <summary>
/// Service for integrating MediaCore with multi-source swarm downloads.
/// </summary>
public interface IMediaCoreSwarmService
{
    /// <summary>
    /// Analyzes filename and discovers content variants using MediaCore fuzzy matching.
    /// </summary>
    /// <param name="filename">The filename to analyze.</param>
    /// <param name="fileSize">The expected file size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content variants grouped by ContentID and similarity scores.</returns>
    Task<ContentVariantsResult> DiscoverContentVariantsAsync(
        string filename,
        long fileSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups verified sources by ContentID for optimal swarm formation.
    /// </summary>
    /// <param name="verificationResult">The content verification result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sources grouped by ContentID with swarm recommendations.</returns>
    Task<ContentIdSwarmGrouping> GroupSourcesByContentIdAsync(
        ContentVerificationResult verificationResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects optimal peer combination for multi-source download using content similarity.
    /// </summary>
    /// <param name="swarmGrouping">The ContentID swarm grouping.</param>
    /// <param name="maxPeers">Maximum number of peers to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Optimal peer selection for the swarm.</returns>
    Task<SwarmPeerSelection> SelectOptimalPeersAsync(
        ContentIdSwarmGrouping swarmGrouping,
        int maxPeers = 5,
        CancellationToken cancellationToken = default);

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
}

/// <summary>
/// Result of content variant discovery.
/// </summary>
public record ContentVariantsResult(
    string OriginalFilename,
    long FileSize,
    IReadOnlyList<ContentVariant> Variants,
    IReadOnlyDictionary<string, double> SimilarityScores);

/// <summary>
/// A content variant discovered through fuzzy matching.
/// </summary>
public record ContentVariant(
    string ContentId,
    string Filename,
    double SimilarityScore,
    ContentDescriptor Descriptor,
    bool IsCanonical);

/// <summary>
/// Swarm grouping by ContentID.
/// </summary>
public record ContentIdSwarmGrouping(
    string PrimaryContentId,
    IReadOnlyDictionary<string, SwarmGroup> GroupsByContentId,
    IReadOnlyList<string> RecommendedContentIds,
    SwarmOptimizationStrategy OptimizationStrategy);

/// <summary>
/// A group of sources sharing the same ContentID.
/// </summary>
public record SwarmGroup(
    string ContentId,
    IReadOnlyList<VerifiedSource> Sources,
    double QualityScore,
    SwarmGroupMetadata Metadata);

/// <summary>
/// Metadata about a swarm group.
/// </summary>
public record SwarmGroupMetadata(
    int SourceCount,
    double AverageSimilarity,
    IReadOnlyList<string> Codecs,
    IReadOnlyList<long> Sizes,
    bool HasCanonicalSource);

/// <summary>
/// Optimal peer selection for swarm download.
/// </summary>
public record SwarmPeerSelection(
    IReadOnlyList<SelectedPeer> SelectedPeers,
    string PrimaryContentId,
    SwarmStrategy Strategy,
    SwarmOptimizationMetrics Metrics);

/// <summary>
/// A peer selected for the swarm.
/// </summary>
public record SelectedPeer(
    VerifiedSource Source,
    string ContentId,
    double ContentSimilarity,
    PeerRole Role,
    double QualityScore);

/// <summary>
/// Role of a peer in the swarm.
/// </summary>
public enum PeerRole
{
    /// <summary>
    /// Primary source with canonical content.
    /// </summary>
    Primary,

    /// <summary>
    /// Backup source with high similarity content.
    /// </summary>
    Backup,

    /// <summary>
    /// Fallback source with lower similarity content.
    /// </summary>
    Fallback
}

/// <summary>
/// Swarm download strategy.
/// </summary>
public enum SwarmStrategy
{
    /// <summary>
    /// Use only canonical sources for highest quality.
    /// </summary>
    CanonicalOnly,

    /// <summary>
    /// Mix canonical and high-similarity variants for speed.
    /// </summary>
    QualityOptimized,

    /// <summary>
    /// Use maximum peers regardless of content similarity.
    /// </summary>
    SpeedOptimized,

    /// <summary>
    /// Balance quality and speed based on swarm intelligence.
    /// </summary>
    Adaptive
}

/// <summary>
/// Optimization strategy for swarm formation.
/// </summary>
public enum SwarmOptimizationStrategy
{
    /// <summary>
    /// Prioritize content quality and accuracy.
    /// </summary>
    QualityFirst,

    /// <summary>
    /// Prioritize download speed and redundancy.
    /// </summary>
    SpeedFirst,

    /// <summary>
    /// Balance quality and speed based on content type.
    /// </summary>
    Balanced,

    /// <summary>
    /// Use swarm intelligence for dynamic optimization.
    /// </summary>
    Intelligent
}

/// <summary>
/// Swarm intelligence and optimization recommendations.
/// </summary>
public record SwarmIntelligence(
    string ContentId,
    SwarmHealth Health,
    IReadOnlyList<PeerRecommendation> PeerRecommendations,
    SwarmOptimizationAdvice OptimizationAdvice);

/// <summary>
/// Overall swarm health assessment.
/// </summary>
public record SwarmHealth(
    double QualityScore,
    double DiversityScore,
    double RedundancyScore,
    SwarmHealthStatus Status);

/// <summary>
/// Swarm health status.
/// </summary>
public enum SwarmHealthStatus
{
    /// <summary>
    /// Swarm is optimal for the content type.
    /// </summary>
    Optimal,

    /// <summary>
    /// Swarm is acceptable but could be improved.
    /// </summary>
    Acceptable,

    /// <summary>
    /// Swarm needs optimization.
    /// </summary>
    NeedsOptimization,

    /// <summary>
    /// Swarm has critical issues.
    /// </summary>
    Critical
}

/// <summary>
/// Recommendation for peer management.
/// </summary>
public record PeerRecommendation(
    string Username,
    PeerRecommendationAction Action,
    string Reason,
    double Confidence);

/// <summary>
/// Recommended action for peer management.
/// </summary>
public enum PeerRecommendationAction
{
    /// <summary>
    /// Keep the peer in the swarm.
    /// </summary>
    Keep,

    /// <summary>
    /// Add the peer to the swarm.
    /// </summary>
    Add,

    /// <summary>
    /// Remove the peer from the swarm.
    /// </summary>
    Remove,

    /// <summary>
    /// Replace current peer with this one.
    /// </summary>
    Replace
}

/// <summary>
/// Optimization advice for the swarm.
/// </summary>
public record SwarmOptimizationAdvice(
    SwarmOptimizationStrategy RecommendedStrategy,
    IReadOnlyList<string> SuggestedContentIds,
    int OptimalPeerCount,
    string Reasoning);

/// <summary>
/// Metrics for swarm optimization evaluation.
/// </summary>
public record SwarmOptimizationMetrics(
    double QualityScore,
    double SpeedPotential,
    double ReliabilityScore,
    IReadOnlyDictionary<string, double> ContentIdDistribution);

