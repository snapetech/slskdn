// <copyright file="PeerReputationEvent.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;

    /// <summary>
    ///     Represents a reputation event for a peer.
    /// </summary>
    /// <remarks>
    ///     T-MCP04: Peer Reputation & Enforcement.
    ///     Tracks negative events that affect peer reputation and lead to bans.
    /// </remarks>
    public sealed class PeerReputationEvent
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerReputationEvent"/> class.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="eventType">The type of reputation event.</param>
        /// <param name="contentId">Optional content ID associated with the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        /// <param name="metadata">Optional metadata about the event.</param>
        public PeerReputationEvent(
            string peerId,
            PeerReputationEventType eventType,
            string? contentId = null,
            DateTimeOffset? timestamp = null,
            string? metadata = null)
        {
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            EventType = eventType;
            ContentId = contentId;
            Timestamp = timestamp ?? DateTimeOffset.UtcNow;
            Metadata = metadata;
        }

        /// <summary>
        ///     Gets the peer identifier.
        /// </summary>
        public string PeerId { get; }

        /// <summary>
        ///     Gets the type of reputation event.
        /// </summary>
        public PeerReputationEventType EventType { get; }

        /// <summary>
        ///     Gets the optional content ID associated with the event.
        /// </summary>
        public string? ContentId { get; }

        /// <summary>
        ///     Gets the timestamp of the event.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        ///     Gets optional metadata about the event (e.g., reason details).
        /// </summary>
        public string? Metadata { get; }
    }
}

