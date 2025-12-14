// <copyright file="PeerReputationEventType.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    /// <summary>
    ///     Types of peer reputation events that affect reputation scoring.
    /// </summary>
    /// <remarks>
    ///     T-MCP04: Peer Reputation & Enforcement.
    ///     Each event type has a negative impact on peer reputation.
    ///     Multiple events can lead to temporary or permanent bans.
    /// </remarks>
    public enum PeerReputationEventType
    {
        /// <summary>
        ///     Peer was associated with blocked content (e.g., served or advertised blocked files).
        /// </summary>
        AssociatedWithBlockedContent,

        /// <summary>
        ///     Peer requested blocked content from us.
        /// </summary>
        RequestedBlockedContent,

        /// <summary>
        ///     Peer served a bad copy (hash mismatch, corrupted file).
        /// </summary>
        ServedBadCopy,

        /// <summary>
        ///     Peer exhibited abusive behavior (excessive requests, harassment).
        /// </summary>
        AbusiveBehavior,

        /// <summary>
        ///     Peer violated protocol expectations (malformed messages, etc.).
        /// </summary>
        ProtocolViolation
    }
}


