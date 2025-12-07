// <copyright file="Peer.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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
        public string PeerId { get; set; }

        /// <summary>
        ///     Gets or sets the capability flags bitfield.
        /// </summary>
        public int Caps { get; set; }

        /// <summary>
        ///     Gets or sets the detected client version.
        /// </summary>
        public string ClientVersion { get; set; }

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

