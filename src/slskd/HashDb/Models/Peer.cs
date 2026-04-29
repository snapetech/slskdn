// <copyright file="Peer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.HashDb.Models
{
    using System;
    using slskd.Capabilities;

    /// <summary>
    ///     Represents a tracked peer in the hash database.
    /// </summary>
    public class Peer
    {
        /// <summary>
        ///     Gets or sets the peer's Soulseek username (primary key).
        /// </summary>
        public string PeerId { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the capability flags bitfield.
        /// </summary>
        public int Caps { get; set; }

        /// <summary>
        ///     Gets or sets the detected client version.
        /// </summary>
        public string? ClientVersion { get; set; }

        /// <summary>
        ///     Gets or sets when this peer was last seen (Unix timestamp).
        /// </summary>
        public long LastSeen { get; set; }

        /// <summary>
        ///     Gets or sets when capabilities were last checked (Unix timestamp).
        /// </summary>
        public long? LastCapCheck { get; set; }

        /// <summary>
        ///     Gets or sets the number of backfill probes done today.
        /// </summary>
        public int BackfillsToday { get; set; }

        /// <summary>
        ///     Gets or sets the date for backfill counter reset (Unix timestamp).
        /// </summary>
        public long? BackfillResetDate { get; set; }

        /// <summary>
        ///     Gets the capability flags as enum.
        /// </summary>
        public PeerCapabilityFlags CapabilityFlags => (PeerCapabilityFlags)Caps;

        /// <summary>
        ///     Gets the last seen time as DateTime.
        /// </summary>
        public DateTime LastSeenUtc => DateTimeOffset.FromUnixTimeSeconds(LastSeen).UtcDateTime;

        /// <summary>
        ///     Gets a value indicating whether this is a slskdn peer.
        /// </summary>
        public bool IsSlskdnPeer => Caps != 0;
    }
}
