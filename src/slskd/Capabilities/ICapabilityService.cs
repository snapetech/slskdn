// <copyright file="ICapabilityService.cs" company="slskdn Team">
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

namespace slskd.Capabilities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Capability flags for slskdn peers.
    /// </summary>
    [Flags]
    public enum PeerCapabilityFlags
    {
        /// <summary>No capabilities.</summary>
        None = 0,

        /// <summary>Supports DHT hash database.</summary>
        SupportsDHT = 1 << 0,

        /// <summary>Supports hash exchange protocol.</summary>
        SupportsHashExchange = 1 << 1,

        /// <summary>Supports partial/chunked downloads.</summary>
        SupportsPartialDownload = 1 << 2,

        /// <summary>Supports mesh sync protocol.</summary>
        SupportsMeshSync = 1 << 3,

        /// <summary>Supports FLAC hash database.</summary>
        SupportsFlacHashDb = 1 << 4,

        /// <summary>Supports multi-source swarm downloads.</summary>
        SupportsSwarm = 1 << 5,
    }

    /// <summary>
    ///     Capabilities information for a peer.
    /// </summary>
    public class PeerCapabilities
    {
        /// <summary>
        ///     Gets or sets the peer's username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the capability flags.
        /// </summary>
        public PeerCapabilityFlags Flags { get; set; }

        /// <summary>
        ///     Gets or sets the client version string.
        /// </summary>
        public string ClientVersion { get; set; }

        /// <summary>
        ///     Gets or sets the protocol version supported.
        /// </summary>
        public int ProtocolVersion { get; set; } = 1;

        /// <summary>
        ///     Gets or sets when this peer was last seen.
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        ///     Gets or sets when capabilities were last checked.
        /// </summary>
        public DateTime LastCapCheck { get; set; }

        /// <summary>
        ///     Gets or sets the mesh sequence ID (for delta sync).
        /// </summary>
        public long MeshSeqId { get; set; }

        /// <summary>
        ///     Gets a value indicating whether this peer is a slskdn client.
        /// </summary>
        public bool IsSlskdnClient => Flags != PeerCapabilityFlags.None;

        /// <summary>
        ///     Gets a value indicating whether this peer supports swarm downloads.
        /// </summary>
        public bool CanSwarm => Flags.HasFlag(PeerCapabilityFlags.SupportsSwarm);

        /// <summary>
        ///     Gets a value indicating whether this peer supports mesh sync.
        /// </summary>
        public bool CanMeshSync => Flags.HasFlag(PeerCapabilityFlags.SupportsMeshSync);
    }

    /// <summary>
    ///     Service for managing slskdn peer capabilities.
    /// </summary>
    public interface ICapabilityService
    {
        /// <summary>
        ///     Gets the current slskdn version string with capability tokens.
        /// </summary>
        string VersionString { get; }

        /// <summary>
        ///     Generates the capability tag string for UserInfo description.
        /// </summary>
        /// <returns>Capability tag string (e.g., "slskdn_caps:v1;dht=1;mesh=1").</returns>
        string GetCapabilityTag();

        /// <summary>
        ///     Generates the full UserInfo description with capability tag appended.
        /// </summary>
        /// <param name="baseDescription">The user's base description.</param>
        /// <returns>Description with capability tag appended.</returns>
        string GetDescriptionWithCapabilities(string baseDescription);

        /// <summary>
        ///     Parses capability tag from a peer's UserInfo description.
        /// </summary>
        /// <param name="description">The peer's description string.</param>
        /// <returns>Parsed capabilities, or null if no slskdn tag found.</returns>
        PeerCapabilities ParseCapabilityTag(string description);

        /// <summary>
        ///     Parses capability tokens from a client version string.
        /// </summary>
        /// <param name="versionString">The client version string.</param>
        /// <returns>Parsed capabilities, or null if not a slskdn client.</returns>
        PeerCapabilities ParseVersionString(string versionString);

        /// <summary>
        ///     Gets cached capabilities for a known peer.
        /// </summary>
        /// <param name="username">The peer's username.</param>
        /// <returns>Cached capabilities, or null if unknown.</returns>
        PeerCapabilities GetPeerCapabilities(string username);

        /// <summary>
        ///     Records discovered capabilities for a peer.
        /// </summary>
        /// <param name="username">The peer's username.</param>
        /// <param name="capabilities">The discovered capabilities.</param>
        void SetPeerCapabilities(string username, PeerCapabilities capabilities);

        /// <summary>
        ///     Gets all known slskdn peers.
        /// </summary>
        /// <returns>Collection of peers with slskdn capabilities.</returns>
        IEnumerable<PeerCapabilities> GetAllSlskdnPeers();

        /// <summary>
        ///     Gets peers that support mesh sync.
        /// </summary>
        /// <returns>Collection of mesh-capable peers.</returns>
        IEnumerable<PeerCapabilities> GetMeshCapablePeers();

        /// <summary>
        ///     Generates the JSON content for the virtual capabilities file.
        /// </summary>
        /// <returns>JSON string with capability information.</returns>
        string GetCapabilityFileContent();
    }
}


