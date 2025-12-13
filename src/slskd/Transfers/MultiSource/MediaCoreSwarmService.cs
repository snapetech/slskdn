// <copyright file="MediaCoreSwarmService.cs" company="slskdN Team">
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
/// Service for integrating MediaCore with multi-source swarm downloads.
/// </summary>
public class MediaCoreSwarmService : IMediaCoreSwarmService
{
    private readonly ILogger<MediaCoreSwarmService> _logger;
    private readonly IContentIdRegistry _contentRegistry;
    private readonly IFuzzyMatcher _fuzzyMatcher;
    private readonly IDescriptorRetriever _descriptorRetriever;
    private readonly IMediaCoreSwarmIntelligence _swarmIntelligence;

    public MediaCoreSwarmService(
        ILogger<MediaCoreSwarmService> logger,
        IContentIdRegistry contentRegistry,
        IFuzzyMatcher fuzzyMatcher,
        IDescriptorRetriever descriptorRetriever,
        IMediaCoreSwarmIntelligence swarmIntelligence)
    {
        _logger = logger;
        _contentRegistry = contentRegistry;
        _fuzzyMatcher = fuzzyMatcher;
        _descriptorRetriever = descriptorRetriever;
        _swarmIntelligence = swarmIntelligence;
    }

    /// <inheritdoc/>
    public async Task<ContentVariantsResult> DiscoverContentVariantsAsync(
        string filename,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[MediaCoreSwarm] Discovering content variants for {Filename}", filename);

            // Extract potential ContentID from filename
            var potentialContentId = ExtractContentIdFromFilename(filename);
            var variants = new List<ContentVariant>();
            var similarityScores = new Dictionary<string, double>();

            if (potentialContentId != null)
            {
                // Try direct ContentID lookup
                var descriptor = await _descriptorRetriever.RetrieveAsync(potentialContentId, cancellationToken: cancellationToken);
                if (descriptor.Found && descriptor.Descriptor != null)
                {
                    variants.Add(new ContentVariant(
                        ContentId: potentialContentId,
                        Filename: filename,
                        SimilarityScore: 1.0,
                        Descriptor: descriptor.Descriptor,
                        IsCanonical: true));

                    similarityScores[potentialContentId] = 1.0;
                }
            }

            // Use fuzzy matching to find similar content
            var fuzzyVariants = await FindFuzzyVariantsAsync(filename, fileSize, cancellationToken);
            variants.AddRange(fuzzyVariants);

            foreach (var variant in fuzzyVariants)
            {
                similarityScores[variant.ContentId] = variant.SimilarityScore;
            }

            var result = new ContentVariantsResult(
                OriginalFilename: filename,
                FileSize: fileSize,
                Variants: variants,
                SimilarityScores: similarityScores);

            _logger.LogInformation(
                "[MediaCoreSwarm] Found {Count} content variants for {Filename}",
                variants.Count, filename);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreSwarm] Error discovering variants for {Filename}", filename);
            return new ContentVariantsResult(
                OriginalFilename: filename,
                FileSize: fileSize,
                Variants: Array.Empty<ContentVariant>(),
                SimilarityScores: new Dictionary<string, double>());
        }
    }

    /// <inheritdoc/>
    public async Task<ContentIdSwarmGrouping> GroupSourcesByContentIdAsync(
        ContentVerificationResult verificationResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "[MediaCoreSwarm] Grouping {Count} sources by ContentID for {Filename}",
                verificationResult.SourcesByHash.Sum(kvp => kvp.Value.Count),
                verificationResult.Filename);

            var groupsByContentId = new Dictionary<string, SwarmGroup>();
            var recommendedContentIds = new List<string>();

            // Process each hash group and try to map to ContentIDs
            foreach (var (hash, sources) in verificationResult.SourcesByHash)
            {
                var contentVariants = await DiscoverContentVariantsAsync(
                    verificationResult.Filename,
                    verificationResult.FileSize,
                    cancellationToken);

                foreach (var variant in contentVariants.Variants)
                {
                    if (!groupsByContentId.ContainsKey(variant.ContentId))
                    {
                        // Create swarm group for this ContentID
                        var groupSources = sources.Where(s => MatchesVariant(s, variant)).ToList();
                        if (groupSources.Any())
                        {
                            var metadata = CalculateGroupMetadata(groupSources, variant);
                            var qualityScore = CalculateGroupQualityScore(groupSources, variant, metadata);

                            groupsByContentId[variant.ContentId] = new SwarmGroup(
                                ContentId: variant.ContentId,
                                Sources: groupSources,
                                QualityScore: qualityScore,
                                Metadata: metadata);

                            if (variant.IsCanonical || qualityScore > 0.8)
                            {
                                recommendedContentIds.Add(variant.ContentId);
                            }
                        }
                    }
                }
            }

            // Determine primary ContentID and optimization strategy
            var primaryContentId = DeterminePrimaryContentId(groupsByContentId);
            var optimizationStrategy = DetermineOptimizationStrategy(primaryContentId, groupsByContentId);

            var result = new ContentIdSwarmGrouping(
                PrimaryContentId: primaryContentId,
                GroupsByContentId: groupsByContentId,
                RecommendedContentIds: recommendedContentIds.OrderByDescending(id =>
                    groupsByContentId.GetValueOrDefault(id)?.QualityScore ?? 0).ToList(),
                OptimizationStrategy: optimizationStrategy);

            _logger.LogInformation(
                "[MediaCoreSwarm] Created {GroupCount} ContentID groups with primary {PrimaryContentId}",
                groupsByContentId.Count, primaryContentId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreSwarm] Error grouping sources by ContentID for {Filename}",
                verificationResult.Filename);

            // Fallback to basic grouping
            return await CreateFallbackGroupingAsync(verificationResult, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<SwarmPeerSelection> SelectOptimalPeersAsync(
        ContentIdSwarmGrouping swarmGrouping,
        int maxPeers = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedPeers = new List<SelectedPeer>();
            var strategy = SwarmStrategy.Adaptive;
            var contentIdDistribution = new Dictionary<string, double>();

            // Select primary sources (canonical content)
            if (swarmGrouping.GroupsByContentId.TryGetValue(swarmGrouping.PrimaryContentId, out var primaryGroup))
            {
                var primaryPeers = SelectPeersFromGroup(primaryGroup, PeerRole.Primary, maxPeers / 2);
                selectedPeers.AddRange(primaryPeers);
                contentIdDistribution[swarmGrouping.PrimaryContentId] = primaryPeers.Count;
            }

            // Select backup sources (high similarity variants)
            var remainingSlots = maxPeers - selectedPeers.Count;
            if (remainingSlots > 0)
            {
                var backupPeers = await SelectBackupPeersAsync(
                    swarmGrouping, selectedPeers.Select(p => p.Source.Username).ToHashSet(), remainingSlots, cancellationToken);
                selectedPeers.AddRange(backupPeers);

                foreach (var peer in backupPeers)
                {
                    contentIdDistribution[peer.ContentId] =
                        contentIdDistribution.GetValueOrDefault(peer.ContentId, 0) + 1;
                }
            }

            // Determine optimal strategy based on selection
            strategy = DetermineSwarmStrategy(selectedPeers, swarmGrouping);

            var metrics = new SwarmOptimizationMetrics(
                QualityScore: CalculateSelectionQuality(selectedPeers),
                SpeedPotential: CalculateSpeedPotential(selectedPeers),
                ReliabilityScore: CalculateReliabilityScore(selectedPeers),
                ContentIdDistribution: contentIdDistribution);

            var result = new SwarmPeerSelection(
                SelectedPeers: selectedPeers,
                PrimaryContentId: swarmGrouping.PrimaryContentId,
                Strategy: strategy,
                Metrics: metrics);

            _logger.LogInformation(
                "[MediaCoreSwarm] Selected {PeerCount} peers using {Strategy} strategy for {ContentId}",
                selectedPeers.Count, strategy, swarmGrouping.PrimaryContentId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreSwarm] Error selecting optimal peers for {ContentId}",
                swarmGrouping.PrimaryContentId);

            // Fallback to simple peer selection
            return await CreateFallbackPeerSelectionAsync(swarmGrouping, maxPeers, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<SwarmIntelligence> GetSwarmIntelligenceAsync(
        string contentId,
        IEnumerable<string> activePeers,
        CancellationToken cancellationToken = default)
    {
        return await _swarmIntelligence.GetSwarmIntelligenceAsync(contentId, activePeers, cancellationToken);
    }

    // Helper methods
    private static string ExtractContentIdFromFilename(string filename)
    {
        // Simple extraction - look for content:domain:type:id pattern in filename
        // This is a basic implementation; in practice, this might involve more sophisticated parsing
        var contentPrefix = "content:";
        var startIndex = filename.IndexOf(contentPrefix, StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0)
        {
            var potentialId = filename.Substring(startIndex);
            // Try to validate it's a proper ContentID
            if (ContentIdParser.IsValid(potentialId))
            {
                return potentialId;
            }
        }
        return null;
    }

    private async Task<IReadOnlyList<ContentVariant>> FindFuzzyVariantsAsync(
        string filename, long fileSize, CancellationToken cancellationToken)
    {
        var variants = new List<ContentVariant>();

        try
        {
            // Search for similar content in the registry
            // This is a simplified implementation - in practice, this would involve
            // more sophisticated fuzzy matching against known content

            var audioContent = await _contentRegistry.FindByDomainAsync("audio");
            var videoContent = await _contentRegistry.FindByDomainAsync("video");

            var candidateContentIds = audioContent.Concat(videoContent).Take(50); // Limit for performance

            foreach (var candidateId in candidateContentIds)
            {
                // Get descriptor for similarity comparison
                var descriptor = await _descriptorRetriever.RetrieveAsync(candidateId, cancellationToken: cancellationToken);
                if (!descriptor.Found || descriptor.Descriptor == null)
                    continue;

                // Calculate similarity (simplified - in practice would use perceptual hashes)
                var similarity = CalculateFilenameSimilarity(filename, descriptor.Descriptor);
                if (similarity > 0.6) // Similarity threshold
                {
                    variants.Add(new ContentVariant(
                        ContentId: candidateId,
                        Filename: GenerateFilenameFromDescriptor(descriptor.Descriptor),
                        SimilarityScore: similarity,
                        Descriptor: descriptor.Descriptor,
                        IsCanonical: false));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MediaCoreSwarm] Error finding fuzzy variants for {Filename}", filename);
        }

        return variants.OrderByDescending(v => v.SimilarityScore).ToList();
    }

    private static bool MatchesVariant(VerifiedSource source, ContentVariant variant)
    {
        // Simple matching logic - in practice, this would involve hash comparison
        // and potentially perceptual similarity analysis
        var sourceFilename = System.IO.Path.GetFileName(source.FullPath);
        return sourceFilename.Contains(variant.ContentId.Split(':').Last()) ||
               variant.Filename.Contains(sourceFilename.Split('.').First());
    }

    private static SwarmGroupMetadata CalculateGroupMetadata(IReadOnlyList<VerifiedSource> sources, ContentVariant variant)
    {
        var codecs = sources.Select(s => InferCodecFromFilename(System.IO.Path.GetFileName(s.FullPath))).Distinct().ToList();
        var sizes = sources.Select(s => 0L).Distinct().ToList(); // Placeholder - file size not directly available in VerifiedSource

        return new SwarmGroupMetadata(
            SourceCount: sources.Count,
            AverageSimilarity: variant.SimilarityScore,
            Codecs: codecs,
            Sizes: sizes,
            HasCanonicalSource: variant.IsCanonical);
    }

    private static double CalculateGroupQualityScore(IReadOnlyList<VerifiedSource> sources, ContentVariant variant, SwarmGroupMetadata metadata)
    {
        var baseScore = variant.SimilarityScore;
        var sourceCountBonus = Math.Min(metadata.SourceCount / 10.0, 0.3); // Up to 30% bonus for more sources
        var codecConsistencyBonus = metadata.Codecs.Count == 1 ? 0.1 : 0.0; // 10% bonus for codec consistency
        var canonicalBonus = variant.IsCanonical ? 0.2 : 0.0; // 20% bonus for canonical content

        return Math.Min(baseScore + sourceCountBonus + codecConsistencyBonus + canonicalBonus, 1.0);
    }

    private static string DeterminePrimaryContentId(IReadOnlyDictionary<string, SwarmGroup> groupsByContentId)
    {
        if (!groupsByContentId.Any())
            return null;

        // Prefer canonical content, then highest quality score
        return groupsByContentId
            .OrderByDescending(kvp => kvp.Value.Metadata.HasCanonicalSource ? 1 : 0)
            .ThenByDescending(kvp => kvp.Value.QualityScore)
            .First().Key;
    }

    private static SwarmOptimizationStrategy DetermineOptimizationStrategy(
        string primaryContentId, IReadOnlyDictionary<string, SwarmGroup> groupsByContentId)
    {
        if (string.IsNullOrEmpty(primaryContentId) || !groupsByContentId.TryGetValue(primaryContentId, out var primaryGroup))
            return SwarmOptimizationStrategy.SpeedFirst;

        var canonicalSources = groupsByContentId.Values.Count(g => g.Metadata.HasCanonicalSource);
        var totalSources = groupsByContentId.Values.Sum(g => g.Metadata.SourceCount);

        // If we have many canonical sources, prioritize quality
        if (canonicalSources > totalSources * 0.7)
            return SwarmOptimizationStrategy.QualityFirst;

        // If we have diverse sources, use intelligent optimization
        if (groupsByContentId.Count > 3)
            return SwarmOptimizationStrategy.Intelligent;

        // Default to balanced approach
        return SwarmOptimizationStrategy.Balanced;
    }

    private static IReadOnlyList<SelectedPeer> SelectPeersFromGroup(SwarmGroup group, PeerRole role, int maxCount)
    {
        return group.Sources
            .Take(maxCount)
            .Select(source => new SelectedPeer(
                Source: source,
                ContentId: group.ContentId,
                ContentSimilarity: group.Metadata.AverageSimilarity,
                Role: role,
                QualityScore: group.QualityScore))
            .ToList();
    }

    private async Task<IReadOnlyList<SelectedPeer>> SelectBackupPeersAsync(
        ContentIdSwarmGrouping swarmGrouping,
        HashSet<string> usedUsernames,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var backupPeers = new List<SelectedPeer>();

        foreach (var contentId in swarmGrouping.RecommendedContentIds.Skip(1)) // Skip primary
        {
            if (backupPeers.Count >= maxCount)
                break;

            if (swarmGrouping.GroupsByContentId.TryGetValue(contentId, out var group))
            {
                var availableSources = group.Sources
                    .Where(s => !usedUsernames.Contains(s.Username))
                    .Take(maxCount - backupPeers.Count);

                foreach (var source in availableSources)
                {
                    backupPeers.Add(new SelectedPeer(
                        Source: source,
                        ContentId: contentId,
                        ContentSimilarity: group.Metadata.AverageSimilarity,
                        Role: PeerRole.Backup,
                        QualityScore: group.QualityScore));

                    usedUsernames.Add(source.Username);
                }
            }
        }

        return backupPeers;
    }

    private static SwarmStrategy DetermineSwarmStrategy(IReadOnlyList<SelectedPeer> peers, ContentIdSwarmGrouping grouping)
    {
        var canonicalPeers = peers.Count(p => p.Role == PeerRole.Primary);
        var totalPeers = peers.Count;

        if (canonicalPeers == totalPeers)
            return SwarmStrategy.CanonicalOnly;

        if (canonicalPeers >= totalPeers * 0.6)
            return SwarmStrategy.QualityOptimized;

        if (totalPeers >= 5)
            return SwarmStrategy.SpeedOptimized;

        return SwarmStrategy.Adaptive;
    }

    private static double CalculateSelectionQuality(IReadOnlyList<SelectedPeer> peers)
    {
        if (!peers.Any()) return 0.0;

        var primaryPeers = peers.Count(p => p.Role == PeerRole.Primary);
        var averageSimilarity = peers.Average(p => p.ContentSimilarity);
        var averageQuality = peers.Average(p => p.QualityScore);

        return (primaryPeers * 0.4 + averageSimilarity * 0.3 + averageQuality * 0.3) / peers.Count;
    }

    private static double CalculateSpeedPotential(IReadOnlyList<SelectedPeer> peers)
    {
        if (!peers.Any()) return 0.0;

        // Speed potential based on peer diversity and count
        var uniqueContentIds = peers.Select(p => p.ContentId).Distinct().Count();
        var contentIdDiversity = (double)uniqueContentIds / peers.Count;

        return Math.Min(peers.Count * 0.2 + contentIdDiversity * 0.8, 1.0);
    }

    private static double CalculateReliabilityScore(IReadOnlyList<SelectedPeer> peers)
    {
        if (!peers.Any()) return 0.0;

        // Reliability based on peer distribution and quality
        var averageQuality = peers.Average(p => p.QualityScore);
        var qualityVariance = peers.Select(p => Math.Pow(p.QualityScore - averageQuality, 2)).Average();
        var qualityConsistency = 1.0 - Math.Min(qualityVariance, 1.0);

        return (averageQuality * 0.6) + (qualityConsistency * 0.4);
    }

    private static string InferCodecFromFilename(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "mp3",
            ".flac" => "flac",
            ".wav" => "wav",
            ".aac" => "aac",
            ".ogg" => "ogg",
            ".m4a" => "m4a",
            _ => "unknown"
        };
    }

    private double CalculateFilenameSimilarity(string filename1, ContentDescriptor descriptor)
    {
        // Simplified similarity calculation - in practice, this would use
        // more sophisticated text similarity algorithms
        var filename2 = GenerateFilenameFromDescriptor(descriptor);
        return _fuzzyMatcher.Score(filename1, "", filename2, "");
    }

    private static string GenerateFilenameFromDescriptor(ContentDescriptor descriptor)
    {
        // Simplified filename generation - in practice, this would be more sophisticated
        var codec = descriptor.Codec ?? "unknown";
        var contentId = descriptor.ContentId.Split(':').Last();
        return $"{contentId}.{codec}";
    }

    private static async Task<ContentIdSwarmGrouping> CreateFallbackGroupingAsync(
        ContentVerificationResult verificationResult, CancellationToken cancellationToken)
    {
        // Fallback grouping when MediaCore integration fails
        var fallbackContentId = $"fallback:{verificationResult.Filename.GetHashCode():X8}";
        var allSources = verificationResult.SourcesByHash.SelectMany(kvp => kvp.Value).ToList();

        var fallbackGroup = new SwarmGroup(
            ContentId: fallbackContentId,
            Sources: allSources,
            QualityScore: 0.5,
            Metadata: new SwarmGroupMetadata(
                SourceCount: allSources.Count,
                AverageSimilarity: 0.5,
                Codecs: new[] { "unknown" },
                Sizes: new[] { verificationResult.FileSize },
                HasCanonicalSource: false));

        return new ContentIdSwarmGrouping(
            PrimaryContentId: fallbackContentId,
            GroupsByContentId: new Dictionary<string, SwarmGroup> { [fallbackContentId] = fallbackGroup },
            RecommendedContentIds: new[] { fallbackContentId },
            OptimizationStrategy: SwarmOptimizationStrategy.SpeedFirst);
    }

    private static async Task<SwarmPeerSelection> CreateFallbackPeerSelectionAsync(
        ContentIdSwarmGrouping swarmGrouping, int maxPeers, CancellationToken cancellationToken)
    {
        // Fallback peer selection when MediaCore integration fails
        var allSources = swarmGrouping.GroupsByContentId
            .SelectMany(kvp => kvp.Value.Sources)
            .Take(maxPeers)
            .Select(source => new SelectedPeer(
                Source: source,
                ContentId: swarmGrouping.PrimaryContentId,
                ContentSimilarity: 0.5,
                Role: PeerRole.Fallback,
                QualityScore: 0.5))
            .ToList();

        return new SwarmPeerSelection(
            SelectedPeers: allSources,
            PrimaryContentId: swarmGrouping.PrimaryContentId,
            Strategy: SwarmStrategy.SpeedOptimized,
            Metrics: new SwarmOptimizationMetrics(0.5, 0.5, 0.5, new Dictionary<string, double>()));
    }
}
