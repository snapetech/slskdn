// <copyright file="PeerReputationStats.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    /// <summary>
    ///     Statistics about peer reputation system.
    /// </summary>
    /// <remarks>
    ///     T-MCP04: Peer Reputation & Enforcement.
    ///     Provides insights into reputation system health and effectiveness.
    /// </remarks>
    public sealed class PeerReputationStats
    {
        /// <summary>
        ///     Gets the total number of reputation events recorded.
        /// </summary>
        public long TotalEvents { get; init; }

        /// <summary>
        ///     Gets the number of unique peers with reputation events.
        /// </summary>
        public int UniquePeers { get; init; }

        /// <summary>
        ///     Gets the number of currently banned peers.
        /// </summary>
        public int BannedPeers { get; init; }

        /// <summary>
        ///     Gets the number of events by type.
        /// </summary>
        public System.Collections.Generic.Dictionary<PeerReputationEventType, long> EventsByType { get; init; } = new();

        /// <summary>
        ///     Gets the average reputation score across all peers.
        /// </summary>
        public double AverageReputationScore { get; init; }
    }
}

