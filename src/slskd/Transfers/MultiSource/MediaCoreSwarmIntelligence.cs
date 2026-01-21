// <copyright file="MediaCoreSwarmIntelligence.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.MediaCore;

/// <summary>
/// Service for providing swarm intelligence and optimization recommendations.
/// </summary>
public class MediaCoreSwarmIntelligence : IMediaCoreSwarmIntelligence
{
    private readonly ILogger<MediaCoreSwarmIntelligence> _logger;
    private readonly IDescriptorRetriever _descriptorRetriever;
    private readonly IContentIdRegistry _contentRegistry;

    public MediaCoreSwarmIntelligence(
        ILogger<MediaCoreSwarmIntelligence> logger,
        IDescriptorRetriever descriptorRetriever,
        IContentIdRegistry contentRegistry)
    {
        _logger = logger;
        _descriptorRetriever = descriptorRetriever;
        _contentRegistry = contentRegistry;
    }

    /// <inheritdoc/>
    public async Task<SwarmIntelligence> GetSwarmIntelligenceAsync(
        string contentId,
        IEnumerable<string> activePeers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activePeerList = activePeers.ToList();

            _logger.LogInformation(
                "[SwarmIntelligence] Analyzing swarm for {ContentId} with {PeerCount} active peers",
                contentId, activePeerList.Count);

            // Get content descriptor for intelligence analysis
            var descriptor = await _descriptorRetriever.RetrieveAsync(contentId, cancellationToken: cancellationToken);
            var health = await AnalyzeSwarmHealthAsync(contentId, activePeerList, descriptor.Descriptor, cancellationToken);
            var recommendations = await GeneratePeerRecommendationsAsync(contentId, activePeerList, cancellationToken);
            var optimizationAdvice = await GenerateOptimizationAdviceAsync(contentId, activePeerList, health, cancellationToken);

            var intelligence = new SwarmIntelligence(
                ContentId: contentId,
                Health: health,
                PeerRecommendations: recommendations,
                OptimizationAdvice: optimizationAdvice);

            _logger.LogInformation(
                "[SwarmIntelligence] Swarm analysis complete for {ContentId}: Health={Health}, Recommendations={RecCount}",
                contentId, health.Status, recommendations.Count);

            return intelligence;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SwarmIntelligence] Error analyzing swarm for {ContentId}", contentId);

            // Return minimal intelligence on error
            return new SwarmIntelligence(
                ContentId: contentId,
                Health: new SwarmHealth(0.5, 0.5, 0.5, SwarmHealthStatus.Acceptable),
                PeerRecommendations: Array.Empty<PeerRecommendation>(),
                OptimizationAdvice: new SwarmOptimizationAdvice(
                    RecommendedStrategy: SwarmOptimizationStrategy.Balanced,
                    SuggestedContentIds: new[] { contentId },
                    OptimalPeerCount: 3,
                    Reasoning: "Fallback due to analysis error"));
        }
    }

    /// <inheritdoc/>
    public async Task<SwarmPerformanceAnalysis> AnalyzeSwarmPerformanceAsync(
        string contentId,
        SwarmMetrics swarmMetrics,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issues = new List<string>();
            var recommendations = new List<string>();

            // Analyze speed performance
            var speedRating = AnalyzeSpeedPerformance(swarmMetrics, issues, recommendations);

            // Analyze quality performance
            var qualityRating = AnalyzeQualityPerformance(contentId, swarmMetrics, issues, recommendations);

            // Analyze peer distribution
            var distributionRating = AnalyzePeerDistribution(swarmMetrics, issues, recommendations);

            // Determine overall rating
            var overallRating = DetermineOverallRating(speedRating, qualityRating, distributionRating);

            // Generate optimization advice
            var optimizationAdvice = await GenerateDetailedOptimizationAdviceAsync(
                contentId, swarmMetrics, overallRating, cancellationToken);

            var analysis = new SwarmPerformanceAnalysis(
                Rating: overallRating,
                Issues: issues,
                Recommendations: recommendations,
                OptimizationAdvice: optimizationAdvice);

            _logger.LogInformation(
                "[SwarmIntelligence] Performance analysis for {ContentId}: {Rating} with {IssueCount} issues",
                contentId, overallRating, issues.Count);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SwarmIntelligence] Error analyzing performance for {ContentId}", contentId);

            return new SwarmPerformanceAnalysis(
                Rating: SwarmPerformanceRating.Acceptable,
                Issues: new[] { "Analysis failed due to error" },
                Recommendations: new[] { "Retry analysis or use default settings" },
                OptimizationAdvice: new SwarmOptimizationAdvice(
                    RecommendedStrategy: SwarmOptimizationStrategy.Balanced,
                    SuggestedContentIds: new[] { contentId },
                    OptimalPeerCount: 3,
                    Reasoning: "Fallback due to analysis error"));
        }
    }

    /// <inheritdoc/>
    public async Task<SwarmPrediction> PredictOptimalConfigurationAsync(
        string contentId,
        IEnumerable<PeerCapability> availablePeers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var peerList = availablePeers.ToList();

            _logger.LogInformation(
                "[SwarmIntelligence] Predicting optimal configuration for {ContentId} with {PeerCount} available peers",
                contentId, peerList.Count);

            // Get content characteristics
            var descriptor = await _descriptorRetriever.RetrieveAsync(contentId, cancellationToken: cancellationToken);
            var contentType = DetermineContentType(contentId, descriptor.Descriptor);

            // Analyze peer capabilities
            var peerAnalysis = AnalyzePeerCapabilities(availablePeers.ToList(), contentType);

            // Predict optimal configuration
            var recommendedStrategy = DetermineOptimalStrategy(contentType, peerAnalysis);
            var optimalPeerCount = CalculateOptimalPeerCount(peerAnalysis, contentType);
            var recommendedPeers = SelectRecommendedPeers(peerList, optimalPeerCount, peerAnalysis);

            // Calculate predicted performance
            var predictedSpeed = CalculatePredictedSpeed(recommendedPeers, peerAnalysis);
            var predictedQuality = CalculatePredictedQuality(recommendedPeers, contentType);

            var reasoning = GeneratePredictionReasoning(contentType, peerAnalysis, recommendedPeers.Count);

            var prediction = new SwarmPrediction(
                RecommendedStrategy: recommendedStrategy,
                OptimalPeerCount: optimalPeerCount,
                RecommendedPeers: recommendedPeers,
                PredictedSpeed: predictedSpeed,
                PredictedQuality: predictedQuality,
                Reasoning: reasoning);

            _logger.LogInformation(
                "[SwarmIntelligence] Predicted configuration for {ContentId}: {Strategy}, {PeerCount} peers, {Speed:F1}x speed, {Quality:F1}x quality",
                contentId, recommendedStrategy, optimalPeerCount, predictedSpeed, predictedQuality);

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SwarmIntelligence] Error predicting configuration for {ContentId}", contentId);

            // Return conservative fallback prediction
            return new SwarmPrediction(
                RecommendedStrategy: SwarmStrategy.Adaptive,
                OptimalPeerCount: 3,
                RecommendedPeers: availablePeers.Take(3).Select(p => p.Username).ToList(),
                PredictedSpeed: 1.0,
                PredictedQuality: 0.8,
                Reasoning: "Fallback prediction due to analysis error");
        }
    }

    // Helper methods for swarm intelligence
    private async Task<SwarmHealth> AnalyzeSwarmHealthAsync(
        string contentId,
        IReadOnlyList<string> activePeers,
        ContentDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var qualityScore = await CalculateQualityScoreAsync(contentId, activePeers, descriptor, cancellationToken);
        var diversityScore = CalculateDiversityScore(activePeers);
        var redundancyScore = CalculateRedundancyScore(activePeers, contentId);

        var averageScore = (qualityScore + diversityScore + redundancyScore) / 3.0;
        var status = averageScore switch
        {
            >= 0.8 => SwarmHealthStatus.Optimal,
            >= 0.6 => SwarmHealthStatus.Acceptable,
            >= 0.4 => SwarmHealthStatus.NeedsOptimization,
            _ => SwarmHealthStatus.Critical
        };

        return new SwarmHealth(
            QualityScore: qualityScore,
            DiversityScore: diversityScore,
            RedundancyScore: redundancyScore,
            Status: status);
    }

    private async Task<IReadOnlyList<PeerRecommendation>> GeneratePeerRecommendationsAsync(
        string contentId,
        IReadOnlyList<string> activePeers,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<PeerRecommendation>();

        // This is a simplified implementation - in practice, this would analyze
        // peer performance, content compatibility, and network conditions

        foreach (var peer in activePeers)
        {
            // Random recommendations for demonstration - in practice, use real metrics
            var random = new Random(peer.GetHashCode());
            var action = random.Next(10) switch
            {
                < 7 => PeerRecommendationAction.Keep,
                < 9 => PeerRecommendationAction.Add,
                _ => PeerRecommendationAction.Remove
            };

            var confidence = random.NextDouble() * 0.5 + 0.5; // 0.5-1.0

            recommendations.Add(new PeerRecommendation(
                Username: peer,
                Action: action,
                Reason: GenerateRecommendationReason(action, confidence),
                Confidence: confidence));
        }

        return recommendations;
    }

    private async Task<SwarmOptimizationAdvice> GenerateOptimizationAdviceAsync(
        string contentId,
        IReadOnlyList<string> activePeers,
        SwarmHealth health,
        CancellationToken cancellationToken)
    {
        var strategy = health.Status switch
        {
            SwarmHealthStatus.Optimal => SwarmOptimizationStrategy.Intelligent,
            SwarmHealthStatus.Acceptable => SwarmOptimizationStrategy.Balanced,
            SwarmHealthStatus.NeedsOptimization => SwarmOptimizationStrategy.QualityFirst,
            SwarmHealthStatus.Critical => SwarmOptimizationStrategy.SpeedFirst,
            _ => SwarmOptimizationStrategy.Balanced
        };

        var suggestedContentIds = await FindRelatedContentIdsAsync(contentId, cancellationToken);
        var optimalPeerCount = CalculateOptimalPeerCount(health, activePeers.Count);

        var reasoning = GenerateOptimizationReasoning(strategy, health, optimalPeerCount);

        return new SwarmOptimizationAdvice(
            RecommendedStrategy: strategy,
            SuggestedContentIds: suggestedContentIds,
            OptimalPeerCount: optimalPeerCount,
            Reasoning: reasoning);
    }

    // Performance analysis methods
    private static SwarmPerformanceRating AnalyzeSpeedPerformance(
        SwarmMetrics metrics, ICollection<string> issues, ICollection<string> recommendations)
    {
        var expectedSpeed = metrics.AveragePeerSpeed * Math.Min(metrics.ActivePeerCount, 5);
        var speedRatio = metrics.CurrentSpeed / expectedSpeed;

        if (speedRatio < 0.5)
        {
            issues.Add($"Download speed is only {speedRatio:P0} of expected");
            recommendations.Add("Consider adding more peers or checking network conditions");
            return SwarmPerformanceRating.Poor;
        }
        else if (speedRatio < 0.8)
        {
            recommendations.Add("Speed could be improved with additional peers");
            return SwarmPerformanceRating.Acceptable;
        }
        else if (speedRatio < 1.2)
        {
            return SwarmPerformanceRating.Good;
        }
        else
        {
            return SwarmPerformanceRating.Excellent;
        }
    }

    private static SwarmPerformanceRating AnalyzeQualityPerformance(
        string contentId, SwarmMetrics metrics, ICollection<string> issues, ICollection<string> recommendations)
    {
        if (metrics.QualityScore < 0.6)
        {
            issues.Add($"Content quality score is low ({metrics.QualityScore:P0})");
            recommendations.Add("Consider switching to higher quality sources");
            return SwarmPerformanceRating.Poor;
        }
        else if (metrics.QualityScore < 0.8)
        {
            recommendations.Add("Quality could be improved with canonical sources");
            return SwarmPerformanceRating.Acceptable;
        }
        else if (metrics.QualityScore < 0.95)
        {
            return SwarmPerformanceRating.Good;
        }
        else
        {
            return SwarmPerformanceRating.Excellent;
        }
    }

    private static SwarmPerformanceRating AnalyzePeerDistribution(
        SwarmMetrics metrics, ICollection<string> issues, ICollection<string> recommendations)
    {
        var averagePerformance = metrics.PeerPerformance.Values.Average();
        var performanceVariance = metrics.PeerPerformance.Values
            .Select(p => Math.Pow(p - averagePerformance, 2))
            .Average();

        if (performanceVariance > 0.5)
        {
            issues.Add("Peer performance variance is high");
            recommendations.Add("Consider replacing underperforming peers");
            return SwarmPerformanceRating.Poor;
        }
        else if (performanceVariance > 0.2)
        {
            recommendations.Add("Peer performance could be more balanced");
            return SwarmPerformanceRating.Acceptable;
        }
        else if (performanceVariance < 0.05)
        {
            return SwarmPerformanceRating.Excellent;
        }
        else
        {
            return SwarmPerformanceRating.Good;
        }
    }

    private static SwarmPerformanceRating DetermineOverallRating(
        SwarmPerformanceRating speed, SwarmPerformanceRating quality, SwarmPerformanceRating distribution)
    {
        var ratings = new[] { speed, quality, distribution };
        var minRating = ratings.Min();

        // If any rating is critical, overall is critical
        if (minRating == SwarmPerformanceRating.Critical)
            return SwarmPerformanceRating.Critical;

        // Overall rating is the average, biased toward the lowest
        var average = (int)ratings.Average(r => (int)r);
        return (SwarmPerformanceRating)Math.Max(average - 1, (int)minRating);
    }

    private async Task<SwarmOptimizationAdvice> GenerateDetailedOptimizationAdviceAsync(
        string contentId, SwarmMetrics metrics, SwarmPerformanceRating rating, CancellationToken cancellationToken)
    {
        var strategy = rating switch
        {
            SwarmPerformanceRating.Excellent => SwarmOptimizationStrategy.Intelligent,
            SwarmPerformanceRating.Good => SwarmOptimizationStrategy.Balanced,
            SwarmPerformanceRating.Acceptable => SwarmOptimizationStrategy.QualityFirst,
            SwarmPerformanceRating.Poor => SwarmOptimizationStrategy.SpeedFirst,
            SwarmPerformanceRating.Critical => SwarmOptimizationStrategy.SpeedFirst,
            _ => SwarmOptimizationStrategy.Balanced
        };

        var suggestedContentIds = await FindRelatedContentIdsAsync(contentId, cancellationToken);
        var optimalPeerCount = rating switch
        {
            SwarmPerformanceRating.Critical => Math.Max(metrics.ActivePeerCount + 2, 5),
            SwarmPerformanceRating.Poor => Math.Max(metrics.ActivePeerCount + 1, 4),
            SwarmPerformanceRating.Acceptable => metrics.ActivePeerCount,
            SwarmPerformanceRating.Good => Math.Max(metrics.ActivePeerCount - 1, 3),
            SwarmPerformanceRating.Excellent => Math.Min(metrics.ActivePeerCount, 3),
            _ => 3
        };

        return new SwarmOptimizationAdvice(
            RecommendedStrategy: strategy,
            SuggestedContentIds: suggestedContentIds,
            OptimalPeerCount: optimalPeerCount,
            Reasoning: $"Optimization based on {rating} performance rating");
    }

    // Prediction methods
        private static ContentType DetermineContentType(string contentId, ContentDescriptor descriptor)
        {
            var parsed = ContentIdParser.Parse(contentId);
            if (parsed != null)
            {
                if (parsed.IsAudio)
                    return ContentType.Audio;
                if (parsed.IsVideo)
                    return ContentType.Video;
                if (parsed.IsImage)
                    return ContentType.Image;
            }

            return ContentType.Unknown;
        }

    private static PeerCapabilityAnalysis AnalyzePeerCapabilities(IReadOnlyList<PeerCapability> peers, ContentType contentType)
    {
        var compatiblePeers = peers.Where(p => IsCompatibleWithContentType(p, contentType)).ToList();
        var averageSpeed = compatiblePeers.Any() ? compatiblePeers.Average(p => p.AverageSpeed) : 0;
        var reliabilityDistribution = compatiblePeers.GroupBy(p => p.Reliability)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PeerCapabilityAnalysis(
            CompatiblePeerCount: compatiblePeers.Count,
            AverageSpeed: averageSpeed,
            ReliabilityDistribution: reliabilityDistribution,
            FastPeers: compatiblePeers.Count(p => p.AverageSpeed > averageSpeed * 1.5),
            ReliablePeers: compatiblePeers.Count(p => p.Reliability >= PeerReliability.Good));
    }

    private static SwarmStrategy DetermineOptimalStrategy(ContentType contentType, PeerCapabilityAnalysis analysis)
    {
        if (analysis.CompatiblePeerCount == 0)
            return SwarmStrategy.SpeedOptimized; // Any peers are better than none

        var qualityPeers = analysis.ReliabilityDistribution
            .Where(kvp => kvp.Key >= PeerReliability.Good)
            .Sum(kvp => kvp.Value);

        var qualityRatio = (double)qualityPeers / analysis.CompatiblePeerCount;

        return contentType switch
        {
            ContentType.Audio when qualityRatio > 0.7 => SwarmStrategy.QualityOptimized,
            ContentType.Video when qualityRatio > 0.8 => SwarmStrategy.QualityOptimized,
            ContentType.Audio when analysis.FastPeers > 3 => SwarmStrategy.SpeedOptimized,
            ContentType.Video when analysis.ReliablePeers > 5 => SwarmStrategy.CanonicalOnly,
            _ => SwarmStrategy.Adaptive
        };
    }

    private static int CalculateOptimalPeerCount(PeerCapabilityAnalysis analysis, ContentType contentType)
    {
        var baseCount = contentType switch
        {
            ContentType.Audio => 3,
            ContentType.Video => 4,
            ContentType.Image => 2,
            _ => 3
        };

        var speedBonus = Math.Min(analysis.FastPeers / 2, 2);
        var reliabilityBonus = Math.Min(analysis.ReliablePeers / 3, 1);

        return Math.Min(baseCount + speedBonus + reliabilityBonus, 7);
    }

    private static IReadOnlyList<string> SelectRecommendedPeers(
        IReadOnlyList<PeerCapability> peers, int count, PeerCapabilityAnalysis analysis)
    {
        // Sort by combined score: reliability + speed + compatibility
        var scoredPeers = peers
            .Select(p => new
            {
                Peer = p,
                Score = CalculatePeerScore(p, analysis)
            })
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Peer.Username)
            .ToList();

        return scoredPeers;
    }

    // Helper methods
    private async Task<double> CalculateQualityScoreAsync(
        string contentId, IReadOnlyList<string> activePeers, ContentDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor == null)
            return 0.5;

        // Simplified quality calculation - in practice, this would analyze
        // codec quality, file size consistency, and descriptor completeness
        var codecQuality = descriptor.Codec != null ? 0.8 : 0.4;
        var sizeConsistency = descriptor.SizeBytes.HasValue ? 0.9 : 0.5;
        var linksCount = descriptor.Links.LinkNames.Count + descriptor.Links.Targets.Count;
        var descriptorCompleteness = (descriptor.Hashes.Count > 0 ? 0.3 : 0) +
                                    (descriptor.PerceptualHashes.Count > 0 ? 0.3 : 0) +
                                    (linksCount > 0 ? 0.4 : 0);

        return (codecQuality + sizeConsistency + descriptorCompleteness) / 3.0;
    }

    private static double CalculateDiversityScore(IReadOnlyList<string> activePeers)
    {
        if (activePeers.Count <= 1)
            return 0.0;

        // Simple diversity based on unique peers (no real geographic/location diversity analysis)
        var uniquePeers = activePeers.Distinct().Count();
        return Math.Min((double)uniquePeers / activePeers.Count, 1.0);
    }

    private static double CalculateRedundancyScore(IReadOnlyList<string> activePeers, string contentId)
    {
        if (activePeers.Count <= 1)
            return 0.0;

        // Redundancy based on peer count relative to optimal swarm size
        var optimalSize = 4; // Simplified optimal swarm size
        var redundancyRatio = Math.Min((double)activePeers.Count / optimalSize, 2.0);

        return Math.Min(redundancyRatio / 2.0, 1.0);
    }

    private async Task<IReadOnlyList<string>> FindRelatedContentIdsAsync(string contentId, CancellationToken cancellationToken)
    {
        try
        {
            var domain = ContentIdParser.GetDomain(contentId);
            if (domain == null)
                return new[] { contentId };

            var relatedContent = await _contentRegistry.FindByDomainAsync(domain);
            return relatedContent.Take(5).ToList(); // Limit to prevent overwhelming
        }
        catch
        {
            return new[] { contentId };
        }
    }

    private static int CalculateOptimalPeerCount(SwarmHealth health, int currentCount)
    {
        return health.Status switch
        {
            SwarmHealthStatus.Optimal => Math.Max(currentCount - 1, 2),
            SwarmHealthStatus.Acceptable => currentCount,
            SwarmHealthStatus.NeedsOptimization => Math.Min(currentCount + 1, 6),
            SwarmHealthStatus.Critical => Math.Min(currentCount + 2, 8),
            _ => currentCount
        };
    }

    private static string GenerateOptimizationReasoning(
        SwarmOptimizationStrategy strategy, SwarmHealth health, int optimalCount)
    {
        return $"Strategy {strategy} recommended due to {health.Status} health status. " +
               $"Optimal peer count: {optimalCount}. " +
               $"Quality: {health.QualityScore:P0}, Diversity: {health.DiversityScore:P0}, Redundancy: {health.RedundancyScore:P0}.";
    }

    private static string GenerateRecommendationReason(PeerRecommendationAction action, double confidence)
    {
        return action switch
        {
            PeerRecommendationAction.Keep => $"Peer performing well ({confidence:P0} confidence)",
            PeerRecommendationAction.Add => $"Peer would improve swarm ({confidence:P0} confidence)",
            PeerRecommendationAction.Remove => $"Peer underperforming ({confidence:P0} confidence)",
            PeerRecommendationAction.Replace => $"Peer needs replacement ({confidence:P0} confidence)",
            _ => "Analysis inconclusive"
        };
    }

    private static bool IsCompatibleWithContentType(PeerCapability peer, ContentType contentType)
    {
        // Simplified compatibility check - in practice, this would check
        // supported codecs, content types, and peer capabilities
        return peer.SupportedContentIds.Any(id =>
            contentType switch
            {
                ContentType.Audio => ContentIdParser.Parse(id)?.IsAudio ?? false,
                ContentType.Video => ContentIdParser.Parse(id)?.IsVideo ?? false,
                ContentType.Image => ContentIdParser.Parse(id)?.IsImage ?? false,
                _ => true
            });
    }

    private static double CalculatePeerScore(PeerCapability peer, PeerCapabilityAnalysis analysis)
    {
        var reliabilityScore = (int)peer.Reliability / 4.0; // Normalize to 0-1
        var speedScore = Math.Min(peer.AverageSpeed / analysis.AverageSpeed, 2.0) / 2.0; // Cap at 2x average
        var responseTimeScore = Math.Max(0, 1.0 - (peer.AverageResponseTime.TotalSeconds / 10.0)); // Penalize slow response

        return (reliabilityScore * 0.5) + (speedScore * 0.3) + (responseTimeScore * 0.2);
    }

    private static double CalculatePredictedSpeed(IReadOnlyList<string> recommendedPeers, PeerCapabilityAnalysis analysis)
    {
        // Simplified speed prediction based on peer count and average speed
        var speedMultiplier = Math.Min(recommendedPeers.Count * 0.7, 3.0); // Diminishing returns
        return speedMultiplier;
    }

    private static double CalculatePredictedQuality(IReadOnlyList<string> recommendedPeers, ContentType contentType)
    {
        // Quality prediction based on content type and peer selection
        var baseQuality = contentType switch
        {
            ContentType.Audio => 0.85,
            ContentType.Video => 0.90,
            ContentType.Image => 0.95,
            _ => 0.80
        };

        // Adjust based on peer count (more peers = potentially better quality distribution)
        var peerBonus = Math.Min(recommendedPeers.Count * 0.02, 0.1);
        return Math.Min(baseQuality + peerBonus, 1.0);
    }

    private static string GeneratePredictionReasoning(ContentType contentType, PeerCapabilityAnalysis analysis, int peerCount)
    {
        return $"Predicted configuration for {contentType} content with {analysis.CompatiblePeerCount} compatible peers. " +
               $"Selected {peerCount} peers from {analysis.FastPeers} fast peers and {analysis.ReliablePeers} reliable peers.";
    }

    private enum ContentType { Audio, Video, Image, Unknown }

    private record PeerCapabilityAnalysis(
        int CompatiblePeerCount,
        double AverageSpeed,
        IReadOnlyDictionary<PeerReliability, int> ReliabilityDistribution,
        int FastPeers,
        int ReliablePeers);
}
