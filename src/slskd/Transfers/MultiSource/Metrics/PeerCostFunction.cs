// <copyright file="PeerCostFunction.cs" company="slskdn Team">
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
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Cost function for ranking peers in swarm scheduling.
    ///     Lower cost = better peer.
    /// </summary>
    public class PeerCostFunction
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerCostFunction"/> class.
        /// </summary>
        public PeerCostFunction()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerCostFunction"/> class with custom weights.
        /// </summary>
        public PeerCostFunction(
            double throughputWeight,
            double errorRateWeight,
            double timeoutRateWeight,
            double rttWeight)
        {
            ThroughputWeight = throughputWeight;
            ErrorRateWeight = errorRateWeight;
            TimeoutRateWeight = timeoutRateWeight;
            RttWeight = rttWeight;
        }

        // Configurable weights (Greek letters as per design doc)
        /// <summary>
        ///     Gets or sets the throughput weight (α - alpha).
        ///     Higher value = prefer fast peers more strongly.
        /// </summary>
        public double ThroughputWeight { get; set; } = 1.0;

        /// <summary>
        ///     Gets or sets the error rate weight (β - beta).
        ///     Higher value = avoid unreliable peers more strongly.
        /// </summary>
        public double ErrorRateWeight { get; set; } = 0.5;

        /// <summary>
        ///     Gets or sets the timeout rate weight (γ - gamma).
        ///     Higher value = avoid slow/stalled peers more strongly.
        /// </summary>
        public double TimeoutRateWeight { get; set; } = 0.3;

        /// <summary>
        ///     Gets or sets the RTT weight (δ - delta).
        ///     Higher value = prefer low-latency peers more strongly.
        /// </summary>
        public double RttWeight { get; set; } = 0.2;

        /// <summary>
        ///     Gets or sets the reputation weight (ρ - rho).
        ///     Higher value = penalize low reputation more strongly.
        /// </summary>
        public double ReputationWeight { get; set; } = 1.0;

        // Penalty multipliers
        /// <summary>
        ///     Gets or sets the penalty for peers with no throughput data.
        /// </summary>
        public double ZeroThroughputPenalty { get; set; } = 1000.0;

        /// <summary>
        ///     Gets or sets the penalty multiplier for high error/timeout rates.
        /// </summary>
        public double HighErrorRatePenalty { get; set; } = 10.0;

        /// <summary>
        ///     Gets or sets the penalty multiplier for low reputation (applied to (1 - reputation)).
        /// </summary>
        public double ReputationPenalty { get; set; } = 5.0;

        /// <summary>
        ///     Compute cost for a peer. Lower cost = better peer.
        /// </summary>
        /// <param name="metrics">The peer performance metrics.</param>
        /// <returns>The computed cost (lower is better).</returns>
        public double ComputeCost(PeerPerformanceMetrics metrics)
        {
            double cost = 0.0;

            // Component 1: Inverse throughput (lower throughput = higher cost)
            if (metrics.ThroughputAvgBytesPerSec > 0)
            {
                // Normalize to MB/s for readability
                double throughputMBps = metrics.ThroughputAvgBytesPerSec / (1024.0 * 1024.0);
                cost += ThroughputWeight / throughputMBps;
            }
            else
            {
                // No throughput data = very high cost
                cost += ZeroThroughputPenalty;
            }

            // Component 2: Error rate penalty
            cost += ErrorRateWeight * metrics.ErrorRate * HighErrorRatePenalty;

            // Component 3: Timeout rate penalty
            cost += TimeoutRateWeight * metrics.TimeoutRate * HighErrorRatePenalty;

            // Component 4: RTT penalty (higher RTT = higher cost)
            if (metrics.RttAvgMs > 0)
            {
                // Normalize RTT to seconds
                double rttSec = metrics.RttAvgMs / 1000.0;
                cost += RttWeight * rttSec;
            }

            // Component 5: Variance penalty (unstable peers get penalized)
            if (metrics.ThroughputStdDevBytesPerSec > 0 && metrics.ThroughputAvgBytesPerSec > 0)
            {
                double coefficientOfVariation = metrics.ThroughputStdDevBytesPerSec / metrics.ThroughputAvgBytesPerSec;
                cost += 0.1 * coefficientOfVariation;
            }

            // Component 6: Reputation penalty (lower reputation => higher cost)
            // Reputation is in [0,1]; neutral = 0.5
            var rep = Math.Clamp(metrics.ReputationScore, 0.0, 1.0);
            cost += ReputationWeight * (1.0 - rep) * ReputationPenalty;

            return cost;
        }

        /// <summary>
        ///     Rank peers by cost (best peers first).
        /// </summary>
        /// <param name="peers">List of peers to rank.</param>
        /// <returns>List of ranked peers ordered by cost (ascending).</returns>
        public List<RankedPeer> RankPeers(List<PeerPerformanceMetrics> peers)
        {
            var ranked = peers
                .Select(p => new RankedPeer
                {
                    PeerId = p.PeerId,
                    Metrics = p,
                    Cost = ComputeCost(p),
                })
                .OrderBy(rp => rp.Cost)
                .ToList();

            // Assign 1-based ranks
            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].Rank = i + 1;
            }

            return ranked;
        }
    }

    /// <summary>
    ///     A peer with computed cost and rank.
    /// </summary>
    public class RankedPeer
    {
        /// <summary>
        ///     Gets or sets the peer identifier.
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        ///     Gets or sets the peer performance metrics.
        /// </summary>
        public PeerPerformanceMetrics Metrics { get; set; }

        /// <summary>
        ///     Gets or sets the computed cost (lower is better).
        /// </summary>
        public double Cost { get; set; }

        /// <summary>
        ///     Gets or sets the rank (1-based, 1 = best).
        /// </summary>
        public int Rank { get; set; }
    }
}
