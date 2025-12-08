// <copyright file="IMeshSyncService.cs" company="slskdn Team">
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

namespace slskd.Mesh
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Mesh.Messages;

    /// <summary>
    ///     Service for epidemic mesh synchronization of hash databases.
    /// </summary>
    public interface IMeshSyncService
    {
        /// <summary>
        ///     Gets statistics about mesh sync operations.
        /// </summary>
        MeshSyncStats Stats { get; }

        /// <summary>
        ///     Initiates mesh sync with a peer if they support it.
        /// </summary>
        /// <param name="username">The peer's username.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Sync result.</returns>
        Task<MeshSyncResult> TrySyncWithPeerAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Handles an incoming mesh message from a peer.
        /// </summary>
        /// <param name="fromUser">The sender's username.</param>
        /// <param name="message">The mesh message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response message, or null if no response needed.</returns>
        Task<MeshMessage> HandleMessageAsync(string fromUser, MeshMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Looks up hash in local DB first, then queries mesh neighbors.
        /// </summary>
        /// <param name="flacKey">The FLAC key to look up.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Hash entry if found, null otherwise.</returns>
        Task<MeshHashEntry> LookupHashAsync(string flacKey, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Publishes a newly discovered hash to the mesh.
        /// </summary>
        /// <param name="flacKey">The FLAC key.</param>
        /// <param name="byteHash">SHA256 hash of first 32KB.</param>
        /// <param name="size">File size in bytes.</param>
        /// <param name="metaFlags">Optional metadata flags.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PublishHashAsync(string flacKey, string byteHash, long size, int? metaFlags = null, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the list of mesh-capable peers.
        /// </summary>
        /// <returns>List of mesh peer info.</returns>
        IEnumerable<MeshPeerInfo> GetMeshPeers();

        /// <summary>
        ///     Generates a HELLO message for initiating sync.
        /// </summary>
        /// <returns>Hello message with current state.</returns>
        MeshHelloMessage GenerateHelloMessage();

        /// <summary>
        ///     Generates a delta response for a peer's request.
        /// </summary>
        /// <param name="sinceSeqId">Sequence ID to start from.</param>
        /// <param name="maxEntries">Maximum entries to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Push delta message with entries.</returns>
        Task<MeshPushDeltaMessage> GenerateDeltaResponseAsync(long sinceSeqId, int maxEntries, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Merges entries received from a peer.
        /// </summary>
        /// <param name="fromUser">The sender's username.</param>
        /// <param name="entries">Entries to merge.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of new entries merged.</returns>
        Task<int> MergeEntriesAsync(string fromUser, IEnumerable<MeshHashEntry> entries, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Result of a mesh sync operation.
    /// </summary>
    public class MeshSyncResult
    {
        /// <summary>Gets or sets a value indicating whether sync was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the peer's username.</summary>
        public string PeerUsername { get; set; }

        /// <summary>Gets or sets the number of entries received.</summary>
        public int EntriesReceived { get; set; }

        /// <summary>Gets or sets the number of entries sent.</summary>
        public int EntriesSent { get; set; }

        /// <summary>Gets or sets the number of new entries merged.</summary>
        public int EntriesMerged { get; set; }

        /// <summary>Gets or sets the peer's latest sequence ID.</summary>
        public long PeerLatestSeqId { get; set; }

        /// <summary>Gets or sets error message if sync failed.</summary>
        public string Error { get; set; }

        /// <summary>Gets or sets sync duration in milliseconds.</summary>
        public long DurationMs { get; set; }
    }

    /// <summary>
    ///     Statistics about mesh sync operations.
    /// </summary>
    public class MeshSyncStats
    {
        /// <summary>Gets or sets total sync sessions completed.</summary>
        public int TotalSyncs { get; set; }

        /// <summary>Gets or sets successful sync sessions.</summary>
        public int SuccessfulSyncs { get; set; }

        /// <summary>Gets or sets failed sync sessions.</summary>
        public int FailedSyncs { get; set; }

        /// <summary>Gets or sets total entries received via mesh.</summary>
        public long TotalEntriesReceived { get; set; }

        /// <summary>Gets or sets total entries sent via mesh.</summary>
        public long TotalEntriesSent { get; set; }

        /// <summary>Gets or sets total entries merged (new).</summary>
        public long TotalEntriesMerged { get; set; }

        /// <summary>Gets or sets count of known mesh peers.</summary>
        public int KnownMeshPeers { get; set; }

        /// <summary>Gets or sets last sync time.</summary>
        public DateTime? LastSyncTime { get; set; }

        /// <summary>Gets or sets our current sequence ID.</summary>
        public long CurrentSeqId { get; set; }

        /// <summary>Gets or sets count of messages rejected due to validation failures.</summary>
        public long RejectedMessages { get; set; }

        /// <summary>Gets or sets count of entries skipped during merge due to validation failures.</summary>
        public long SkippedEntries { get; set; }
    }

    /// <summary>
    ///     Information about a mesh-capable peer.
    /// </summary>
    public class MeshPeerInfo
    {
        /// <summary>Gets or sets the peer's username.</summary>
        public string Username { get; set; }

        /// <summary>Gets or sets the peer's latest known sequence ID.</summary>
        public long LatestSeqId { get; set; }

        /// <summary>Gets or sets when we last synced with this peer.</summary>
        public DateTime? LastSyncTime { get; set; }

        /// <summary>Gets or sets the last sequence ID we received from them.</summary>
        public long LastSeqSeen { get; set; }

        /// <summary>Gets or sets when we last saw this peer.</summary>
        public DateTime LastSeen { get; set; }

        /// <summary>Gets or sets the peer's client version.</summary>
        public string ClientVersion { get; set; }
    }
}


