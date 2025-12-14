// <copyright file="IPeerReputationStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for storing and retrieving peer reputation data.
    /// </summary>
    /// <remarks>
    ///     T-MCP04: Peer Reputation & Enforcement.
    ///     Provides encrypted persistence of peer reputation events and scoring.
    ///     Implements ban threshold logic and reputation decay.
    /// </remarks>
    public interface IPeerReputationStore
    {
        /// <summary>
        ///     Records a reputation event for a peer.
        /// </summary>
        /// <param name="event">The reputation event to record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordEventAsync(PeerReputationEvent @event, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the reputation score for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reputation score (negative values indicate poor reputation).</returns>
        Task<int> GetReputationScoreAsync(string peerId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Checks if a peer is currently banned based on reputation.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the peer is banned, false otherwise.</returns>
        Task<bool> IsPeerBannedAsync(string peerId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets recent reputation events for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="maxEvents">Maximum number of events to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of recent reputation events for the peer.</returns>
        Task<IEnumerable<PeerReputationEvent>> GetRecentEventsAsync(string peerId, int maxEvents = 50, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Performs reputation decay and cleanup of old events.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DecayAndCleanupAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets reputation statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics about peer reputation.</returns>
        Task<PeerReputationStats> GetStatsAsync(CancellationToken cancellationToken = default);
    }
}
