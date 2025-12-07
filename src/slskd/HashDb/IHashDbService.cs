// <copyright file="IHashDbService.cs" company="slskd Team">
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

namespace slskd.HashDb
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Capabilities;
    using slskd.HashDb.Models;

    /// <summary>
    ///     Service for managing the local hash database.
    /// </summary>
    public interface IHashDbService
    {
        /// <summary>
        ///     Gets the current sequence ID for mesh sync.
        /// </summary>
        long CurrentSeqId { get; }

        /// <summary>
        ///     Gets statistics about the hash database.
        /// </summary>
        HashDbStats GetStats();

        // ========== Peer Management ==========

        /// <summary>
        ///     Gets or creates a peer record.
        /// </summary>
        Task<Peer> GetOrCreatePeerAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates peer capabilities.
        /// </summary>
        Task UpdatePeerCapabilitiesAsync(string username, PeerCapabilityFlags caps, string clientVersion = null, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets all slskdn-capable peers.
        /// </summary>
        Task<IEnumerable<Peer>> GetSlskdnPeersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates peer's last seen timestamp.
        /// </summary>
        Task TouchPeerAsync(string username, CancellationToken cancellationToken = default);

        // ========== FLAC Inventory Management ==========

        /// <summary>
        ///     Upserts a FLAC inventory entry.
        /// </summary>
        Task UpsertFlacEntryAsync(FlacInventoryEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets a FLAC entry by file ID.
        /// </summary>
        Task<FlacInventoryEntry> GetFlacEntryAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets FLAC entries by size (for multi-source matching).
        /// </summary>
        Task<IEnumerable<FlacInventoryEntry>> GetFlacEntriesBySizeAsync(long size, int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets FLAC entries that need hash verification.
        /// </summary>
        Task<IEnumerable<FlacInventoryEntry>> GetUnhashedFlacFilesAsync(int limit = 50, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates the hash value for a FLAC entry.
        /// </summary>
        Task UpdateFlacHashAsync(string fileId, string hashValue, HashSource source, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Marks a FLAC entry's hash verification as failed.
        /// </summary>
        Task MarkFlacHashFailedAsync(string fileId, CancellationToken cancellationToken = default);

        // ========== Hash Database (Content-Addressed) ==========

        /// <summary>
        ///     Looks up a hash by FLAC key.
        /// </summary>
        Task<HashDbEntry> LookupHashAsync(string flacKey, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Looks up hashes by file size.
        /// </summary>
        Task<IEnumerable<HashDbEntry>> LookupHashesBySizeAsync(long size, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stores a hash entry.
        /// </summary>
        Task StoreHashAsync(HashDbEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stores a hash from verification result.
        /// </summary>
        Task StoreHashFromVerificationAsync(string filename, long size, string byteHash, int? sampleRate = null, int? channels = null, int? bitDepth = null, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Increments the use count for a hash entry.
        /// </summary>
        Task IncrementHashUseCountAsync(string flacKey, CancellationToken cancellationToken = default);

        // ========== Mesh Sync ==========

        /// <summary>
        ///     Gets the latest sequence ID.
        /// </summary>
        Task<long> GetLatestSeqIdAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets entries since a sequence ID (for delta sync).
        /// </summary>
        Task<IEnumerable<HashDbEntry>> GetEntriesSinceSeqAsync(long sinceSeq, int limit = 1000, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Merges entries received from mesh sync.
        /// </summary>
        Task<int> MergeEntriesFromMeshAsync(IEnumerable<HashDbEntry> entries, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the last sequence ID seen from a peer.
        /// </summary>
        Task<long> GetPeerLastSeqSeenAsync(string peerId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates the last sequence ID seen from a peer.
        /// </summary>
        Task UpdatePeerLastSeqSeenAsync(string peerId, long seqId, CancellationToken cancellationToken = default);

        // ========== Backfill Scheduling ==========

        /// <summary>
        ///     Gets backfill candidates (files that need hash probing).
        /// </summary>
        Task<IEnumerable<FlacInventoryEntry>> GetBackfillCandidatesAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Increments a peer's daily backfill count.
        /// </summary>
        Task IncrementPeerBackfillCountAsync(string peerId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets a peer's backfill count for today.
        /// </summary>
        Task<int> GetPeerBackfillCountTodayAsync(string peerId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Resets daily backfill counters (called at midnight).
        /// </summary>
        Task ResetDailyBackfillCountersAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Statistics about the hash database.
    /// </summary>
    public class HashDbStats
    {
        /// <summary>Gets or sets total peers tracked.</summary>
        public int TotalPeers { get; set; }

        /// <summary>Gets or sets slskdn-capable peers.</summary>
        public int SlskdnPeers { get; set; }

        /// <summary>Gets or sets total FLAC inventory entries.</summary>
        public int TotalFlacEntries { get; set; }

        /// <summary>Gets or sets FLAC entries with known hashes.</summary>
        public int HashedFlacEntries { get; set; }

        /// <summary>Gets or sets total hash database entries.</summary>
        public int TotalHashEntries { get; set; }

        /// <summary>Gets or sets current sequence ID.</summary>
        public long CurrentSeqId { get; set; }

        /// <summary>Gets or sets database file size in bytes.</summary>
        public long DatabaseSizeBytes { get; set; }
    }
}

