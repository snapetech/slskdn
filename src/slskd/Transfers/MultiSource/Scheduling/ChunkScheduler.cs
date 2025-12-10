// <copyright file="ChunkScheduler.cs" company="slskdn Team">
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

namespace slskd.Transfers.MultiSource.Scheduling
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Transfers.MultiSource.Metrics;

    /// <summary>
    ///     Intelligent chunk scheduler that assigns chunks to peers based on cost function.
    /// </summary>
    public class ChunkScheduler : IChunkScheduler
    {
        private readonly IPeerMetricsService metricsService;
        private readonly PeerCostFunction costFunction;
        private readonly ILogger log = Log.ForContext<ChunkScheduler>();
        private readonly bool enableCostBasedScheduling;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ChunkScheduler"/> class.
        /// </summary>
        public ChunkScheduler(
            IPeerMetricsService metricsService,
            bool enableCostBasedScheduling = true)
        {
            this.metricsService = metricsService;
            this.enableCostBasedScheduling = enableCostBasedScheduling;
            costFunction = new PeerCostFunction();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ChunkScheduler"/> class with custom cost function.
        /// </summary>
        public ChunkScheduler(
            IPeerMetricsService metricsService,
            PeerCostFunction customCostFunction,
            bool enableCostBasedScheduling = true)
        {
            this.metricsService = metricsService;
            this.enableCostBasedScheduling = enableCostBasedScheduling;
            costFunction = customCostFunction ?? new PeerCostFunction();
        }

        /// <inheritdoc/>
        public async Task<ChunkAssignment> AssignChunkAsync(
            ChunkRequest request,
            List<string> availablePeers,
            CancellationToken ct = default)
        {
            if (availablePeers == null || availablePeers.Count == 0)
            {
                log.Warning("[ChunkScheduler] No peers available for chunk {ChunkIndex}", request.ChunkIndex);
                return new ChunkAssignment
                {
                    ChunkIndex = request.ChunkIndex,
                    AssignedPeer = null,
                    Success = false,
                    Reason = "No peers available",
                };
            }

            // If cost-based scheduling is disabled, use simple round-robin
            if (!enableCostBasedScheduling)
            {
                return new ChunkAssignment
                {
                    ChunkIndex = request.ChunkIndex,
                    AssignedPeer = availablePeers.First(),
                    Success = true,
                    Reason = "Round-robin (cost-based scheduling disabled)",
                };
            }

            // Get metrics for all available peers
            var peerMetrics = new List<PeerPerformanceMetrics>();
            foreach (var peerId in availablePeers)
            {
                var metrics = await metricsService.GetMetricsAsync(peerId, PeerSource.Soulseek, ct);
                peerMetrics.Add(metrics);
            }

            // Rank peers by cost
            var rankedPeers = costFunction.RankPeers(peerMetrics);

            if (rankedPeers.Count == 0)
            {
                return new ChunkAssignment
                {
                    ChunkIndex = request.ChunkIndex,
                    AssignedPeer = null,
                    Success = false,
                    Reason = "No peers could be ranked",
                };
            }

            // Assign to best peer (lowest cost = rank 1)
            var bestPeer = rankedPeers.First();

            log.Debug(
                "[ChunkScheduler] Assigned chunk {ChunkIndex} to peer {PeerId} (rank {Rank}, cost {Cost:F3})",
                request.ChunkIndex,
                bestPeer.PeerId,
                bestPeer.Rank,
                bestPeer.Cost);

            return new ChunkAssignment
            {
                ChunkIndex = request.ChunkIndex,
                AssignedPeer = bestPeer.PeerId,
                Success = true,
                Reason = $"Best peer (rank {bestPeer.Rank}, cost {bestPeer.Cost:F3})",
                Cost = bestPeer.Cost,
                Rank = bestPeer.Rank,
            };
        }

        /// <inheritdoc/>
        public async Task<List<ChunkAssignment>> AssignMultipleChunksAsync(
            List<ChunkRequest> requests,
            List<string> availablePeers,
            CancellationToken ct = default)
        {
            var assignments = new List<ChunkAssignment>();

            if (availablePeers == null || availablePeers.Count == 0)
            {
                log.Warning("[ChunkScheduler] No peers available for batch chunk assignment");
                foreach (var req in requests)
                {
                    assignments.Add(new ChunkAssignment
                    {
                        ChunkIndex = req.ChunkIndex,
                        AssignedPeer = null,
                        Success = false,
                        Reason = "No peers available",
                    });
                }

                return assignments;
            }

            // If cost-based scheduling is disabled, use simple distribution
            if (!enableCostBasedScheduling)
            {
                for (int i = 0; i < requests.Count; i++)
                {
                    assignments.Add(new ChunkAssignment
                    {
                        ChunkIndex = requests[i].ChunkIndex,
                        AssignedPeer = availablePeers[i % availablePeers.Count],
                        Success = true,
                        Reason = "Round-robin (cost-based scheduling disabled)",
                    });
                }

                return assignments;
            }

            // Get metrics and rank peers once
            var peerMetrics = new List<PeerPerformanceMetrics>();
            foreach (var peerId in availablePeers)
            {
                var metrics = await metricsService.GetMetricsAsync(peerId, PeerSource.Soulseek, ct);
                peerMetrics.Add(metrics);
            }

            var rankedPeers = costFunction.RankPeers(peerMetrics);

            if (rankedPeers.Count == 0)
            {
                log.Warning("[ChunkScheduler] No peers could be ranked for batch assignment");
                foreach (var req in requests)
                {
                    assignments.Add(new ChunkAssignment
                    {
                        ChunkIndex = req.ChunkIndex,
                        AssignedPeer = null,
                        Success = false,
                        Reason = "No peers could be ranked",
                    });
                }

                return assignments;
            }

            // Strategy: Assign high-priority chunks to best peers
            // Sort requests by priority (descending), then assign to ranked peers
            var sortedRequests = requests.OrderByDescending(r => r.Priority).ToList();

            for (int i = 0; i < sortedRequests.Count; i++)
            {
                var request = sortedRequests[i];

                // Use modulo to cycle through ranked peers if we have more chunks than peers
                var peerIndex = i % rankedPeers.Count;
                var assignedPeer = rankedPeers[peerIndex];

                assignments.Add(new ChunkAssignment
                {
                    ChunkIndex = request.ChunkIndex,
                    AssignedPeer = assignedPeer.PeerId,
                    Success = true,
                    Reason = $"Priority-based (rank {assignedPeer.Rank}, cost {assignedPeer.Cost:F3})",
                    Cost = assignedPeer.Cost,
                    Rank = assignedPeer.Rank,
                });
            }

            log.Information(
                "[ChunkScheduler] Assigned {Count} chunks across {PeerCount} ranked peers",
                assignments.Count,
                rankedPeers.Count);

            return assignments;
        }

        /// <inheritdoc/>
        public async Task HandlePeerDegradationAsync(
            string peerId,
            DegradationReason reason,
            CancellationToken ct = default)
        {
            log.Warning("[ChunkScheduler] Peer {PeerId} degraded: {Reason}", peerId, reason);

            // Get current metrics
            var metrics = await metricsService.GetMetricsAsync(peerId, PeerSource.Soulseek, ct);

            // Strategy: Re-rank and suggest shifting work away from this peer
            // For now, we just log the degradation. In a full swarm manager,
            // this would trigger chunk reassignment.

            var currentCost = costFunction.ComputeCost(metrics);
            log.Information(
                "[ChunkScheduler] Peer {PeerId} current cost: {Cost:F3} (error rate: {ErrorRate:F2}%, timeout rate: {TimeoutRate:F2}%)",
                peerId,
                currentCost,
                metrics.ErrorRate * 100,
                metrics.TimeoutRate * 100);

            // TODO: In full implementation, trigger chunk reassignment to better peers
            await Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Reasons for peer degradation.
    /// </summary>
    public enum DegradationReason
    {
        /// <summary>
        ///     High error rate detected.
        /// </summary>
        HighErrorRate,

        /// <summary>
        ///     High timeout rate detected.
        /// </summary>
        HighTimeoutRate,

        /// <summary>
        ///     Throughput dropped significantly.
        /// </summary>
        ThroughputDrop,

        /// <summary>
        ///     RTT increased significantly.
        /// </summary>
        RttIncrease,

        /// <summary>
        ///     Peer disconnected.
        /// </summary>
        Disconnected,
    }

    /// <summary>
    ///     Request for chunk assignment.
    /// </summary>
    public class ChunkRequest
    {
        /// <summary>
        ///     Gets or sets the chunk index.
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        ///     Gets or sets the priority (higher = more important).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        ///     Gets or sets the chunk size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets the expected hash (for verification).
        /// </summary>
        public string ExpectedHash { get; set; }
    }

    /// <summary>
    ///     Result of chunk assignment.
    /// </summary>
    public class ChunkAssignment
    {
        /// <summary>
        ///     Gets or sets the chunk index.
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        ///     Gets or sets the assigned peer ID (null if assignment failed).
        /// </summary>
        public string AssignedPeer { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether assignment was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Gets or sets the reason for assignment result.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        ///     Gets or sets the computed cost of the assigned peer.
        /// </summary>
        public double? Cost { get; set; }

        /// <summary>
        ///     Gets or sets the rank of the assigned peer.
        /// </summary>
        public int? Rank { get; set; }
    }
}
