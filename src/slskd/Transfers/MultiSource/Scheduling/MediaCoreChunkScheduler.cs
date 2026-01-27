// <copyright file="MediaCoreChunkScheduler.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Scheduling;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.MediaCore;

/// <summary>
/// MediaCore-aware chunk scheduler that uses content similarity for optimal peer selection.
/// </summary>
public class MediaCoreChunkScheduler : IChunkScheduler
{
    private readonly ILogger<MediaCoreChunkScheduler> _logger;
    private readonly IDescriptorRetriever _descriptorRetriever;
    private readonly IFuzzyMatcher _fuzzyMatcher;
    private readonly IContentIdRegistry _contentRegistry;
    
    // T-1405: Track active chunk assignments for reassignment
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _activeAssignments = new(); // chunkIndex -> peerId

    public MediaCoreChunkScheduler(
        ILogger<MediaCoreChunkScheduler> logger,
        IDescriptorRetriever descriptorRetriever,
        IFuzzyMatcher fuzzyMatcher,
        IContentIdRegistry contentRegistry)
    {
        _logger = logger;
        _descriptorRetriever = descriptorRetriever;
        _fuzzyMatcher = fuzzyMatcher;
        _contentRegistry = contentRegistry;
    }

    /// <inheritdoc/>
    public async Task<ChunkAssignment> AssignChunkAsync(
        ChunkRequest request,
        List<string> availablePeers,
        CancellationToken ct = default)
    {
        if (!availablePeers.Any())
        {
            _logger.LogWarning("[MediaCoreChunkScheduler] No available peers for chunk {ChunkIndex}", request.ChunkIndex);
            return new ChunkAssignment
            {
                ChunkIndex = request.ChunkIndex,
                AssignedPeer = null,
                Success = false,
                Reason = "No peers available"
            };
        }

        try
        {
            // Analyze the content to determine optimal peer selection
            var contentAnalysis = await AnalyzeContentForChunkAsync(request, ct);

            // Score peers based on content compatibility and performance
            var peerScores = await ScorePeersForChunkAsync(
                request, availablePeers, contentAnalysis, ct);

            // Select the best peer for this chunk
            var selectedPeer = peerScores
                .OrderByDescending(ps => ps.Score)
                .ThenBy(ps => ps.Username) // Deterministic tie-breaking
                .First();

            _logger.LogDebug(
                "[MediaCoreChunkScheduler] Assigned chunk {ChunkIndex} to {Peer} (score: {Score:F2})",
                request.ChunkIndex, selectedPeer.Username, selectedPeer.Score);

            // T-1405: Register assignment for tracking
            RegisterAssignment(request.ChunkIndex, selectedPeer.Username);

            return new ChunkAssignment
            {
                ChunkIndex = request.ChunkIndex,
                AssignedPeer = selectedPeer.Username,
                Success = true,
                Reason = $"Selected peer {selectedPeer.Username} with score {selectedPeer.Score:F2}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreChunkScheduler] Error assigning chunk {ChunkIndex}", request.ChunkIndex);

            // Fallback to simple round-robin selection
            var fallbackPeer = availablePeers[request.ChunkIndex % availablePeers.Count];
            return new ChunkAssignment
            {
                ChunkIndex = request.ChunkIndex,
                AssignedPeer = fallbackPeer,
                Success = true,
                Reason = "Fallback selection"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<ChunkAssignment>> AssignMultipleChunksAsync(
        List<ChunkRequest> requests,
        List<string> availablePeers,
        CancellationToken ct = default)
    {
        if (!availablePeers.Any())
        {
            _logger.LogWarning("[MediaCoreChunkScheduler] No available peers for {RequestCount} chunks", requests.Count);
            return requests.Select(r => new ChunkAssignment
            {
                ChunkIndex = r.ChunkIndex,
                AssignedPeer = null,
                Success = false,
                Reason = "No peers available"
            }).ToList();
        }

        try
        {
            var assignments = new List<ChunkAssignment>();
            var peerWorkload = new Dictionary<string, int>();

            // Initialize peer workloads
            foreach (var peer in availablePeers)
            {
                peerWorkload[peer] = 0;
            }

            // Analyze content for all requests (batch optimization)
            var contentAnalyses = new Dictionary<int, ContentAnalysis>();
            foreach (var request in requests)
            {
                var analysis = await AnalyzeContentForChunkAsync(request, ct);
                contentAnalyses[request.ChunkIndex] = analysis;
            }

            // Process requests in priority order (earlier chunks first for faster completion)
            var orderedRequests = requests.OrderBy(r => r.ChunkIndex).ToList();

            foreach (var request in orderedRequests)
            {
                // Score peers considering current workload and content compatibility
                var peerScores = await ScorePeersForChunkAsync(
                    request, availablePeers, contentAnalyses[request.ChunkIndex], ct);

                // Adjust scores based on current workload (prefer less loaded peers)
                var adjustedScores = peerScores.Select(ps => new PeerScore(
                    Username: ps.Username,
                    Score: ps.Score * CalculateWorkloadMultiplier(peerWorkload[ps.Username]),
                    ContentSimilarity: ps.ContentSimilarity,
                    PerformanceScore: ps.PerformanceScore,
                    Reason: $"{ps.Reason}, workload: {peerWorkload[ps.Username]}")).ToList();

                // Select the best peer
                var selectedPeer = adjustedScores.OrderByDescending(ps => ps.Score).First();
                
                // T-1405: Register assignment for tracking
                RegisterAssignment(request.ChunkIndex, selectedPeer.Username);
                
                assignments.Add(new ChunkAssignment
                {
                    ChunkIndex = request.ChunkIndex,
                    AssignedPeer = selectedPeer.Username,
                    Success = true,
                    Reason = $"Batch assigned peer {selectedPeer.Username}"
                });

                // Update workload
                peerWorkload[selectedPeer.Username]++;

                _logger.LogDebug(
                    "[MediaCoreChunkScheduler] Assigned chunk {ChunkIndex} to {Peer} (adjusted score: {Score:F2})",
                    request.ChunkIndex, selectedPeer.Username, selectedPeer.Score);
            }

            _logger.LogInformation(
                "[MediaCoreChunkScheduler] Assigned {AssignmentCount} chunks to {PeerCount} peers",
                assignments.Count, availablePeers.Count);

            return assignments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreChunkScheduler] Error in batch assignment, using fallback");

            // Fallback to simple round-robin distribution
            var assignments = new List<ChunkAssignment>();
            for (int i = 0; i < requests.Count; i++)
            {
                var peer = availablePeers[i % availablePeers.Count];
                assignments.Add(new ChunkAssignment
                {
                    ChunkIndex = requests[i].ChunkIndex,
                    AssignedPeer = peer,
                    Success = true,
                    Reason = "Fallback batch assignment"
                });
            }

            return assignments;
        }
    }

    // Helper methods
    private async Task<ContentAnalysis> AnalyzeContentForChunkAsync(ChunkRequest request, CancellationToken ct)
    {
        // Extract content information from the request
        // This is simplified - in practice, the ChunkRequest might contain content metadata
        // For now, we'll work without direct filename access
        var contentId = (string)null; // TODO: Pass contentId through chunk request context

        if (contentId != null)
        {
            // Get content descriptor for analysis
            var descriptor = await _descriptorRetriever.RetrieveAsync(contentId, cancellationToken: ct);
            if (descriptor.Found && descriptor.Descriptor != null)
            {
                return new ContentAnalysis(
                    ContentId: contentId,
                    Descriptor: descriptor.Descriptor,
                    IsKnownContent: true,
                    ContentType: DetermineContentType(contentId),
                    QualityIndicators: ExtractQualityIndicators(descriptor.Descriptor));
            }
        }

        // Fallback for unknown content
        return new ContentAnalysis(
            ContentId: null,
            Descriptor: null,
            IsKnownContent: false,
            ContentType: ContentType.Unknown,
            QualityIndicators: new QualityIndicators(0.5, 0.5, 0.5, 0.5));
    }

    private async Task<IReadOnlyList<PeerScore>> ScorePeersForChunkAsync(
        ChunkRequest request,
        IReadOnlyList<string> availablePeers,
        ContentAnalysis contentAnalysis,
        CancellationToken ct)
    {
        var peerScores = new List<PeerScore>();

        foreach (var peer in availablePeers)
        {
            try
            {
                var score = await CalculatePeerScoreAsync(peer, request, contentAnalysis, ct);
                peerScores.Add(score);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MediaCoreChunkScheduler] Error scoring peer {Peer} for chunk {Chunk}", peer, request.ChunkIndex);

                // Assign minimum score for problematic peers
                peerScores.Add(new PeerScore(
                    Username: peer,
                    Score: 0.1,
                    ContentSimilarity: 0.5,
                    PerformanceScore: 0.2,
                    Reason: "Scoring error"));
            }
        }

        return peerScores;
    }

    private async Task<PeerScore> CalculatePeerScoreAsync(
        string username,
        ChunkRequest request,
        ContentAnalysis contentAnalysis,
        CancellationToken ct)
    {
        var contentSimilarity = 0.5; // Default
        var performanceScore = 0.5; // Default
        var reasons = new List<string>();

        // Content similarity analysis
        if (contentAnalysis.IsKnownContent && contentAnalysis.ContentId != null)
        {
            // Analyze how well this peer's content matches the requested content
            contentSimilarity = await AnalyzeContentSimilarityAsync(username, contentAnalysis, ct);
            reasons.Add($"content similarity: {contentSimilarity:F2}");
        }
        else
        {
            reasons.Add("unknown content");
        }

        // Performance analysis (simplified - would use real metrics)
        performanceScore = CalculatePerformanceScore(username, request);
        reasons.Add($"performance: {performanceScore:F2}");

        // Content type compatibility
        var typeCompatibility = CalculateTypeCompatibility(contentAnalysis.ContentType);
        reasons.Add($"type compatibility: {typeCompatibility:F2}");

        // Quality preference
        var qualityPreference = contentAnalysis.QualityIndicators != null ?
            contentAnalysis.QualityIndicators.OverallQuality : 0.5;
        reasons.Add($"quality preference: {qualityPreference:F2}");

        // Combine scores with weights
        var finalScore = (contentSimilarity * 0.4) +
                        (performanceScore * 0.3) +
                        (typeCompatibility * 0.2) +
                        (qualityPreference * 0.1);

        return new PeerScore(
            Username: username,
            Score: finalScore,
            ContentSimilarity: contentSimilarity,
            PerformanceScore: performanceScore,
            Reason: string.Join(", ", reasons));
    }

    private async Task<double> AnalyzeContentSimilarityAsync(
        string username, ContentAnalysis contentAnalysis, CancellationToken ct)
    {
        if (contentAnalysis.ContentId == null || contentAnalysis.Descriptor == null)
            return 0.5;

        try
        {
            // Find content variants this peer might have
            var variants = await _contentRegistry.FindByDomainAsync(
                ContentIdParser.GetDomain(contentAnalysis.ContentId));

            if (!variants.Any())
                return 0.5;

            // Calculate similarity to the canonical content
            var similarities = new List<double>();
            foreach (var variant in variants.Take(5)) // Limit for performance
            {
                var similarity = await _fuzzyMatcher.ScorePerceptualAsync(
                    contentAnalysis.ContentId, variant, _contentRegistry, ct);
                similarities.Add(similarity);
            }

            return similarities.Any() ? similarities.Average() : 0.5;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MediaCoreChunkScheduler] Error analyzing content similarity for {Peer}", username);
            return 0.5; // Neutral score on error
        }
    }

    private static double CalculatePerformanceScore(string username, ChunkRequest request)
    {
        // Simplified performance scoring - in practice, this would use:
        // - Historical download speeds from this peer
        // - Current network conditions
        // - Peer reliability metrics
        // - Geographic proximity

        // For now, use a hash-based pseudo-random but deterministic score
        var hash = username.GetHashCode() + request.ChunkIndex.GetHashCode();
        var random = new Random(hash);
        return 0.3 + (random.NextDouble() * 0.7); // 0.3-1.0 range
    }

    private static double CalculateTypeCompatibility(ContentType contentType)
    {
        // Content type compatibility scoring
        return contentType switch
        {
            ContentType.Audio => 0.9, // Audio is well-supported
            ContentType.Video => 0.8, // Video is supported but more complex
            ContentType.Image => 0.7, // Images are simpler
            ContentType.Unknown => 0.5, // Unknown content gets neutral score
            _ => 0.5
        };
    }

    private static double CalculateWorkloadMultiplier(int currentWorkload)
    {
        // Prefer less loaded peers with diminishing returns
        // Workload 0: 1.2x multiplier
        // Workload 1: 1.0x multiplier
        // Workload 2: 0.9x multiplier
        // Workload 3+: 0.8x multiplier
        return currentWorkload switch
        {
            0 => 1.2,
            1 => 1.0,
            2 => 0.9,
            _ => 0.8
        };
    }

    private static string ExtractContentIdFromFilename(string filename)
    {
        // Simple extraction - look for content:domain:type:id pattern
        var contentPrefix = "content:";
        var startIndex = filename.IndexOf(contentPrefix, StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0)
        {
            var potentialId = filename.Substring(startIndex);
            if (ContentIdParser.IsValid(potentialId))
            {
                return potentialId;
            }
        }
        return null;
    }

    private static ContentType DetermineContentType(string contentId)
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
            if (parsed.IsText)
                return ContentType.Text;
        }

        return ContentType.Unknown;
    }

    private static QualityIndicators ExtractQualityIndicators(ContentDescriptor descriptor)
    {
        var codecScore = descriptor.Codec != null ? 0.8 : 0.4;
        var hashScore = descriptor.Hashes.Count > 0 ? 0.9 : 0.3;
        var perceptualScore = descriptor.PerceptualHashes.Count > 0 ? 0.9 : 0.3;

        var overallQuality = (codecScore + hashScore + perceptualScore) / 3.0;

        return new QualityIndicators(
            CodecQuality: codecScore,
            HashCompleteness: hashScore,
            PerceptualQuality: perceptualScore,
            OverallQuality: overallQuality);
    }

    // Nested types
    private enum ContentType { Audio, Video, Image, Text, Unknown }

    private record ContentAnalysis(
        string ContentId,
        ContentDescriptor Descriptor,
        bool IsKnownContent,
        ContentType ContentType,
        QualityIndicators QualityIndicators);

    private record QualityIndicators(
        double CodecQuality,
        double HashCompleteness,
        double PerceptualQuality,
        double OverallQuality);

    private record PeerScore(
        string Username,
        double Score,
        double ContentSimilarity,
        double PerformanceScore,
        string Reason);

    /// <inheritdoc/>
    public async Task<List<int>> HandlePeerDegradationAsync(
        string peerId,
        DegradationReason reason,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MediaCoreChunkScheduler] Handling peer degradation: {PeerId}, Reason: {Reason}",
            peerId, reason);

        // T-1405: Find chunks assigned to this peer for reassignment
        var chunksToReassign = new List<int>();
        foreach (var kvp in _activeAssignments)
        {
            if (kvp.Value == peerId)
            {
                chunksToReassign.Add(kvp.Key);
            }
        }

        if (chunksToReassign.Count > 0)
        {
            _logger.LogInformation(
                "[MediaCoreChunkScheduler] Marking {Count} chunks for reassignment from degraded peer {PeerId}",
                chunksToReassign.Count, peerId);

            // Unregister assignments
            foreach (var chunkIndex in chunksToReassign)
            {
                _activeAssignments.TryRemove(chunkIndex, out _);
            }
        }

        // In a MediaCore-aware scheduler, we could:
        // 1. Update peer reliability metrics in swarm intelligence
        // 2. Adjust future peer scoring based on degradation history
        // 3. Trigger re-evaluation of optimal swarm configuration
        // 4. Log degradation patterns for content-specific optimization

        return chunksToReassign;
    }

    public void RegisterAssignment(int chunkIndex, string peerId)
    {
        _activeAssignments[chunkIndex] = peerId;
    }

    public void UnregisterAssignment(int chunkIndex)
    {
        _activeAssignments.TryRemove(chunkIndex, out _);
    }
}
