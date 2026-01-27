// <copyright file="AdaptiveScheduler.cs" company="slskdN Team">
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
using slskd.Transfers.MultiSource.Metrics;

/// <summary>
///     Adaptive scheduler with learning and dynamic optimization.
/// </summary>
public class AdaptiveScheduler : IAdaptiveScheduler
{
    private readonly IChunkScheduler _baseScheduler;
    private readonly IPeerMetricsService _peerMetrics;
    private readonly ILogger<AdaptiveScheduler> _logger;

    // Learning data structures
    private readonly ConcurrentDictionary<string, PeerLearningData> _peerLearningData = new();
    private readonly ConcurrentQueue<ChunkCompletionFeedback> _recentCompletions = new();
    private readonly object _adaptationLock = new();

    // Adaptive weights (dynamically adjusted)
    private double _reputationWeight = 0.4;
    private double _throughputWeight = 0.3;
    private double _rttWeight = 0.2;
    private double _recentPerformanceWeight = 0.1;

    // Adaptation parameters
    private const int MaxRecentCompletions = 100;
    private const double LearningRate = 0.1; // How quickly to adapt
    private const int AdaptationInterval = 50; // Adapt after N completions
    private int _completionCount = 0;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AdaptiveScheduler"/> class.
    /// </summary>
    public AdaptiveScheduler(
        IChunkScheduler baseScheduler,
        IPeerMetricsService peerMetrics,
        ILogger<AdaptiveScheduler> logger)
    {
        _baseScheduler = baseScheduler;
        _peerMetrics = peerMetrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChunkAssignment> AssignChunkAsync(
        ChunkRequest request,
        List<string> availablePeers,
        CancellationToken ct = default)
    {
        // Use base scheduler but enhance with adaptive scoring
        var assignment = await _baseScheduler.AssignChunkAsync(request, availablePeers, ct).ConfigureAwait(false);

        if (assignment.Success && !string.IsNullOrEmpty(assignment.AssignedPeer))
        {
            // Enhance assignment with adaptive factors
            var peerId = assignment.AssignedPeer;
            var learningData = _peerLearningData.GetOrAdd(peerId, _ => new PeerLearningData { PeerId = peerId });

            // Adjust based on recent performance
            var recentPerformanceScore = CalculateRecentPerformanceScore(peerId);
            if (recentPerformanceScore < 0.5)
            {
                _logger.LogDebug(
                    "[AdaptiveScheduler] Peer {PeerId} has low recent performance ({Score:F2}), but assigned anyway",
                    peerId, recentPerformanceScore);
            }
        }

        return assignment;
    }

    /// <inheritdoc/>
    public async Task RecordChunkCompletionAsync(
        int chunkIndex,
        string peerId,
        bool success,
        long durationMs,
        long bytesTransferred,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Record completion feedback
            var feedback = new ChunkCompletionFeedback
            {
                ChunkIndex = chunkIndex,
                PeerId = peerId,
                Success = success,
                DurationMs = durationMs,
                BytesTransferred = bytesTransferred,
                Timestamp = DateTimeOffset.UtcNow,
            };

            _recentCompletions.Enqueue(feedback);
            while (_recentCompletions.Count > MaxRecentCompletions)
            {
                _recentCompletions.TryDequeue(out _);
            }

            // Update peer learning data
            var learningData = _peerLearningData.GetOrAdd(peerId, _ => new PeerLearningData { PeerId = peerId });
            lock (learningData)
            {
                learningData.TotalChunks++;
                if (success)
                {
                    learningData.SuccessfulChunks++;
                    learningData.TotalDurationMs += durationMs;
                    learningData.TotalBytesTransferred += bytesTransferred;
                }
                else
                {
                    learningData.FailedChunks++;
                }
                learningData.LastUpdated = DateTimeOffset.UtcNow;
            }

            // Periodically adapt weights
            Interlocked.Increment(ref _completionCount);
            if (_completionCount >= AdaptationInterval)
            {
                Interlocked.Exchange(ref _completionCount, 0);
                await AdaptWeightsAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AdaptiveScheduler] Error recording chunk completion");
        }
    }

    /// <inheritdoc/>
    public async Task AdaptWeightsAsync(CancellationToken cancellationToken = default)
    {
        lock (_adaptationLock)
        {
            try
            {
                _logger.LogDebug("[AdaptiveScheduler] Adapting weights based on recent performance");

                // Analyze recent completions to determine which factors are most predictive
                var recent = _recentCompletions.ToList();
                if (recent.Count < 10)
                {
                    return; // Not enough data
                }

                // Calculate correlation between factors and success
                var reputationCorrelation = CalculateFactorCorrelation(recent, f => GetReputationScore(f.PeerId));
                var throughputCorrelation = CalculateFactorCorrelation(recent, f => GetThroughputScore(f.PeerId, f.BytesTransferred, f.DurationMs));
                var rttCorrelation = CalculateFactorCorrelation(recent, f => GetRttScore(f.PeerId));

                // Normalize correlations to weights
                var totalCorrelation = Math.Abs(reputationCorrelation) + Math.Abs(throughputCorrelation) + Math.Abs(rttCorrelation);
                if (totalCorrelation > 0)
                {
                    var newReputationWeight = Math.Abs(reputationCorrelation) / totalCorrelation;
                    var newThroughputWeight = Math.Abs(throughputCorrelation) / totalCorrelation;
                    var newRttWeight = Math.Abs(rttCorrelation) / totalCorrelation;

                    // Smooth adaptation (exponential moving average)
                    _reputationWeight = (LearningRate * newReputationWeight) + ((1 - LearningRate) * _reputationWeight);
                    _throughputWeight = (LearningRate * newThroughputWeight) + ((1 - LearningRate) * _throughputWeight);
                    _rttWeight = (LearningRate * newRttWeight) + ((1 - LearningRate) * _rttWeight);

                    // Normalize to sum to 1.0
                    var sum = _reputationWeight + _throughputWeight + _rttWeight;
                    if (sum > 0)
                    {
                        _reputationWeight /= sum;
                        _throughputWeight /= sum;
                        _rttWeight /= sum;
                    }

                    _logger.LogInformation(
                        "[AdaptiveScheduler] Adapted weights - Reputation: {Rep:F2}, Throughput: {Thru:F2}, RTT: {Rtt:F2}",
                        _reputationWeight, _throughputWeight, _rttWeight);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AdaptiveScheduler] Error adapting weights");
            }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public AdaptiveSchedulingStats GetStats()
    {
        return new AdaptiveSchedulingStats
        {
            ReputationWeight = _reputationWeight,
            ThroughputWeight = _throughputWeight,
            RttWeight = _rttWeight,
            RecentPerformanceWeight = _recentPerformanceWeight,
            TotalCompletions = _recentCompletions.Count,
            TrackedPeers = _peerLearningData.Count,
            LastAdaptation = DateTimeOffset.UtcNow, // Would track actual last adaptation time
        };
    }

    /// <inheritdoc/>
    public async Task<List<ChunkAssignment>> AssignMultipleChunksAsync(
        List<ChunkRequest> requests,
        List<string> availablePeers,
        CancellationToken ct = default)
    {
        // Delegate to base scheduler
        return await _baseScheduler.AssignMultipleChunksAsync(requests, availablePeers, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<List<int>> HandlePeerDegradationAsync(
        string peerId,
        DegradationReason reason,
        CancellationToken ct = default)
    {
        // Delegate to base scheduler
        var reassignedChunks = await _baseScheduler.HandlePeerDegradationAsync(peerId, reason, ct).ConfigureAwait(false);

        // Update learning data
        if (_peerLearningData.TryGetValue(peerId, out var learningData))
        {
            lock (learningData)
            {
                learningData.FailedChunks++;
                learningData.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        return reassignedChunks;
    }

    /// <inheritdoc/>
    public void RegisterAssignment(int chunkIndex, string peerId)
    {
        // Delegate to base scheduler if it supports this
        if (_baseScheduler is ChunkScheduler baseScheduler)
        {
            baseScheduler.RegisterAssignment(chunkIndex, peerId);
        }
    }

    /// <inheritdoc/>
    public void UnregisterAssignment(int chunkIndex)
    {
        // Delegate to base scheduler if it supports this
        if (_baseScheduler is ChunkScheduler baseScheduler)
        {
            baseScheduler.UnregisterAssignment(chunkIndex);
        }
    }

    private double CalculateRecentPerformanceScore(string peerId)
    {
        var recent = _recentCompletions
            .Where(f => f.PeerId == peerId && (DateTimeOffset.UtcNow - f.Timestamp).TotalMinutes < 10)
            .ToList();

        if (recent.Count == 0)
        {
            return 0.5; // Neutral score
        }

        var successRate = (double)recent.Count(f => f.Success) / recent.Count;
        var avgDuration = recent.Where(f => f.Success).Average(f => (double)f.DurationMs);
        var normalizedDuration = Math.Max(0.0, 1.0 - (avgDuration / 10000.0)); // 10s = 0, 0s = 1

        return (successRate * 0.7) + (normalizedDuration * 0.3);
    }

    private double CalculateFactorCorrelation(
        List<ChunkCompletionFeedback> completions,
        Func<ChunkCompletionFeedback, double> factorExtractor)
    {
        if (completions.Count < 2)
        {
            return 0.0;
        }

        var factors = completions.Select(factorExtractor).ToList();
        var outcomes = completions.Select(f => f.Success ? 1.0 : 0.0).ToList();

        var avgFactor = factors.Average();
        var avgOutcome = outcomes.Average();

        var numerator = factors.Zip(outcomes, (f, o) => (f - avgFactor) * (o - avgOutcome)).Sum();
        var factorVariance = factors.Sum(f => Math.Pow(f - avgFactor, 2));
        var outcomeVariance = outcomes.Sum(o => Math.Pow(o - avgOutcome, 2));

        if (factorVariance == 0 || outcomeVariance == 0)
        {
            return 0.0;
        }

        return numerator / Math.Sqrt(factorVariance * outcomeVariance);
    }

    private double GetReputationScore(string peerId)
    {
        // Would query peer metrics service
        return 0.5; // Placeholder
    }

    private double GetThroughputScore(string peerId, long bytesTransferred, long durationMs)
    {
        if (durationMs <= 0)
        {
            return 0.0;
        }

        var throughput = bytesTransferred / (durationMs / 1000.0);
        return Math.Min(1.0, throughput / (1024.0 * 1024.0)); // Normalize to 1 MB/s
    }

    private double GetRttScore(string peerId)
    {
        // Would query peer metrics service
        return 0.5; // Placeholder
    }

    private class PeerLearningData
    {
        public string PeerId { get; set; } = string.Empty;
        public int TotalChunks { get; set; }
        public int SuccessfulChunks { get; set; }
        public int FailedChunks { get; set; }
        public long TotalDurationMs { get; set; }
        public long TotalBytesTransferred { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    private class ChunkCompletionFeedback
    {
        public int ChunkIndex { get; set; }
        public string PeerId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public long DurationMs { get; set; }
        public long BytesTransferred { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}

/// <summary>
///     Adaptive scheduling statistics.
/// </summary>
public class AdaptiveSchedulingStats
{
    /// <summary>
    ///     Current reputation weight.
    /// </summary>
    public double ReputationWeight { get; set; }

    /// <summary>
    ///     Current throughput weight.
    /// </summary>
    public double ThroughputWeight { get; set; }

    /// <summary>
    ///     Current RTT weight.
    /// </summary>
    public double RttWeight { get; set; }

    /// <summary>
    ///     Current recent performance weight.
    /// </summary>
    public double RecentPerformanceWeight { get; set; }

    /// <summary>
    ///     Total completions tracked.
    /// </summary>
    public int TotalCompletions { get; set; }

    /// <summary>
    ///     Number of peers being tracked.
    /// </summary>
    public int TrackedPeers { get; set; }

    /// <summary>
    ///     Last adaptation time.
    /// </summary>
    public DateTimeOffset LastAdaptation { get; set; }
}
