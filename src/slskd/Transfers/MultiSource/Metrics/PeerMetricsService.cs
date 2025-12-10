// <copyright file="PeerMetricsService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Transfers.MultiSource.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;

    /// <summary>
    ///     Service for tracking per-peer performance metrics with exponential moving averages.
    /// </summary>
    public class PeerMetricsService : IPeerMetricsService
    {
        private readonly ConcurrentDictionary<string, PeerPerformanceMetrics> metricsCache = new();
        private readonly IHashDbService hashDb;
        private readonly ILogger<PeerMetricsService> log;

        // Configuration
        private const int MaxRecentSamples = 30;  // Sliding window size
        private const double EmaAlpha = 0.3;  // Exponential moving average weight (0-1, higher = more weight to recent samples)

        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerMetricsService"/> class.
        /// </summary>
        public PeerMetricsService(
            IHashDbService hashDb,
            ILogger<PeerMetricsService> log)
        {
            this.hashDb = hashDb;
            this.log = log;
        }

        /// <inheritdoc/>
        public async Task<PeerPerformanceMetrics> GetMetricsAsync(string peerId, PeerSource source, CancellationToken ct = default)
        {
            return await GetOrCreateMetricsAsync(peerId, source, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RecordRttSampleAsync(string peerId, double rttMs, CancellationToken ct = default)
        {
            var metrics = await GetOrCreateMetricsAsync(peerId, PeerSource.Soulseek, ct).ConfigureAwait(false);

            lock (metrics)
            {
                // Add to recent samples
                metrics.RecentRttSamples.Enqueue(new RttSample
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    RttMs = rttMs,
                });

                // Trim sliding window
                while (metrics.RecentRttSamples.Count > MaxRecentSamples)
                {
                    metrics.RecentRttSamples.Dequeue();
                }

                // Update exponential moving average
                if (metrics.SampleCount == 0)
                {
                    metrics.RttAvgMs = rttMs;
                }
                else
                {
                    metrics.RttAvgMs = (EmaAlpha * rttMs) + ((1 - EmaAlpha) * metrics.RttAvgMs);
                }

                // Compute standard deviation from recent samples
                metrics.RttStdDevMs = ComputeStdDev(metrics.RecentRttSamples.Select(s => s.RttMs));

                metrics.LastRttSample = DateTimeOffset.UtcNow;
                metrics.SampleCount++;
                metrics.LastUpdated = DateTimeOffset.UtcNow;
            }

            await PersistMetricsAsync(metrics, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RecordThroughputSampleAsync(string peerId, long bytesTransferred, TimeSpan duration, CancellationToken ct = default)
        {
            if (duration.TotalSeconds <= 0)
            {
                return; // Invalid duration
            }

            var metrics = await GetOrCreateMetricsAsync(peerId, PeerSource.Soulseek, ct).ConfigureAwait(false);

            double bytesPerSec = bytesTransferred / duration.TotalSeconds;

            lock (metrics)
            {
                // Add to recent samples
                metrics.RecentThroughputSamples.Enqueue(new ThroughputSample
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    BytesPerSec = bytesPerSec,
                    BytesTransferred = bytesTransferred,
                    Duration = duration,
                });

                // Trim sliding window
                while (metrics.RecentThroughputSamples.Count > MaxRecentSamples)
                {
                    metrics.RecentThroughputSamples.Dequeue();
                }

                // Update EMA
                if (metrics.TotalBytesTransferred == 0)
                {
                    metrics.ThroughputAvgBytesPerSec = bytesPerSec;
                }
                else
                {
                    metrics.ThroughputAvgBytesPerSec = (EmaAlpha * bytesPerSec) + ((1 - EmaAlpha) * metrics.ThroughputAvgBytesPerSec);
                }

                // Compute standard deviation
                metrics.ThroughputStdDevBytesPerSec = ComputeStdDev(metrics.RecentThroughputSamples.Select(s => s.BytesPerSec));

                metrics.TotalBytesTransferred += bytesTransferred;
                metrics.LastThroughputSample = DateTimeOffset.UtcNow;
                metrics.LastUpdated = DateTimeOffset.UtcNow;
            }

            await PersistMetricsAsync(metrics, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RecordChunkCompletionAsync(string peerId, ChunkCompletionResult result, CancellationToken ct = default)
        {
            var metrics = await GetOrCreateMetricsAsync(peerId, PeerSource.Soulseek, ct).ConfigureAwait(false);

            lock (metrics)
            {
                metrics.ChunksRequested++;

                switch (result)
                {
                    case ChunkCompletionResult.Success:
                        metrics.ChunksCompleted++;
                        break;
                    case ChunkCompletionResult.Failed:
                        metrics.ChunksFailed++;
                        break;
                    case ChunkCompletionResult.TimedOut:
                        metrics.ChunksTimedOut++;
                        break;
                    case ChunkCompletionResult.Corrupted:
                        metrics.ChunksCorrupted++;
                        break;
                }

                metrics.LastUpdated = DateTimeOffset.UtcNow;
            }

            await PersistMetricsAsync(metrics, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<PeerPerformanceMetrics>> GetRankedPeersAsync(int limit = 100, CancellationToken ct = default)
        {
            // Get all peers from cache and database
            var allMetrics = await hashDb.GetAllPeerMetricsAsync(ct).ConfigureAwait(false);

            // Use cost function to rank peers
            var costFunction = new PeerCostFunction();
            var rankedPeers = costFunction.RankPeers(allMetrics);

            // Return top peers with metrics
            return rankedPeers
                .Take(limit)
                .Select(rp => rp.Metrics)
                .ToList();
        }

        private double ComputeStdDev(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2)
            {
                return 0.0;
            }

            double avg = valuesList.Average();
            double sumSquaredDiffs = valuesList.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquaredDiffs / valuesList.Count);
        }

        private async Task<PeerPerformanceMetrics> GetOrCreateMetricsAsync(string peerId, PeerSource source, CancellationToken ct)
        {
            if (metricsCache.TryGetValue(peerId, out var cached))
            {
                return cached;
            }

            // Load from database or create new
            var metrics = await hashDb.GetPeerMetricsAsync(peerId, ct).ConfigureAwait(false) ?? new PeerPerformanceMetrics
            {
                PeerId = peerId,
                Source = source,
                FirstSeen = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
            };

            metricsCache[peerId] = metrics;
            return metrics;
        }

        private async Task PersistMetricsAsync(PeerPerformanceMetrics metrics, CancellationToken ct)
        {
            try
            {
                await hashDb.UpsertPeerMetricsAsync(metrics, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "[PeerMetrics] Failed to persist metrics for peer {PeerId}", metrics.PeerId);
            }
        }
    }
}
