// <copyright file="PeerReputationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Service for managing peer reputation and enforcement.
    /// </summary>
    /// <remarks>
    ///     T-MCP04: Peer Reputation & Enforcement.
    ///     Provides high-level interface for reputation management.
    ///     Integrates with VirtualSoulfind planner and work budget systems.
    /// </remarks>
    public sealed class PeerReputationService
    {
        private readonly ILogger<PeerReputationService> _logger;
        private readonly IPeerReputationStore _store;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerReputationService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="store">The reputation store.</param>
        public PeerReputationService(
            ILogger<PeerReputationService> logger,
            IPeerReputationStore store)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        ///     Records that a peer was associated with blocked content.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="contentId">The content ID that was blocked.</param>
        /// <param name="metadata">Optional metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordBlockedContentAssociationAsync(string peerId, string contentId, string? metadata = null, CancellationToken cancellationToken = default)
        {
            var @event = new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.AssociatedWithBlockedContent,
                contentId: contentId,
                metadata: metadata);

            await _store.RecordEventAsync(@event, cancellationToken);
            _logger.LogInformation("Recorded blocked content association for peer {PeerId} with content {ContentId}", peerId, contentId);
        }

        /// <summary>
        ///     Records that a peer requested blocked content.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="contentId">The content ID that was requested.</param>
        /// <param name="metadata">Optional metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordBlockedContentRequestAsync(string peerId, string contentId, string? metadata = null, CancellationToken cancellationToken = default)
        {
            var @event = new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.RequestedBlockedContent,
                contentId: contentId,
                metadata: metadata);

            await _store.RecordEventAsync(@event, cancellationToken);
            _logger.LogInformation("Recorded blocked content request from peer {PeerId} for content {ContentId}", peerId, contentId);
        }

        /// <summary>
        ///     Records that a peer served a bad copy.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="contentId">The content ID that was bad.</param>
        /// <param name="metadata">Optional metadata (e.g., hash mismatch details).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordBadCopyServedAsync(string peerId, string contentId, string? metadata = null, CancellationToken cancellationToken = default)
        {
            var @event = new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.ServedBadCopy,
                contentId: contentId,
                metadata: metadata);

            await _store.RecordEventAsync(@event, cancellationToken);
            _logger.LogWarning("Recorded bad copy served by peer {PeerId} for content {ContentId}", peerId, contentId);
        }

        /// <summary>
        ///     Records abusive behavior by a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="metadata">Details about the abusive behavior.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordAbusiveBehaviorAsync(string peerId, string? metadata = null, CancellationToken cancellationToken = default)
        {
            var @event = new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.AbusiveBehavior,
                metadata: metadata);

            await _store.RecordEventAsync(@event, cancellationToken);
            _logger.LogWarning("Recorded abusive behavior by peer {PeerId}: {Metadata}", peerId, metadata);
        }

        /// <summary>
        ///     Records a protocol violation by a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="metadata">Details about the protocol violation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordProtocolViolationAsync(string peerId, string? metadata = null, CancellationToken cancellationToken = default)
        {
            var @event = new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.ProtocolViolation,
                metadata: metadata);

            await _store.RecordEventAsync(@event, cancellationToken);
            _logger.LogWarning("Recorded protocol violation by peer {PeerId}: {Metadata}", peerId, metadata);
        }

        /// <summary>
        ///     Checks if a peer should be allowed to participate in planning.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the peer is allowed, false if banned.</returns>
        public async Task<bool> IsPeerAllowedForPlanningAsync(string peerId, CancellationToken cancellationToken = default)
        {
            var isBanned = await _store.IsPeerBannedAsync(peerId, cancellationToken);
            if (isBanned)
            {
                _logger.LogDebug("Peer {PeerId} is banned and will be excluded from planning", peerId);
            }

            return !isBanned;
        }

        /// <summary>
        ///     Gets the reputation score for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reputation score.</returns>
        public async Task<int> GetReputationScoreAsync(string peerId, CancellationToken cancellationToken = default)
        {
            return await _store.GetReputationScoreAsync(peerId, cancellationToken);
        }

        /// <summary>
        ///     Performs maintenance operations (decay and cleanup).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            await _store.DecayAndCleanupAsync(cancellationToken);
        }

        /// <summary>
        ///     Gets reputation statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Reputation statistics.</returns>
        public async Task<PeerReputationStats> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            return await _store.GetStatsAsync(cancellationToken);
        }
    }
}
