// <copyright file="IHashDbService.cs" company="slskdn Team">
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

namespace slskd.HashDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Audio;
    using slskd.Capabilities;
    using slskd.HashDb.Models;
    using slskd.Integrations.MusicBrainz.Models;

    /// <summary>
    ///     Service for managing the local hash database.
    /// </summary>
    public partial interface IHashDbService
    {
        /// <summary>
        ///     Gets the current sequence ID for mesh sync.
        /// </summary>
        long CurrentSeqId { get; }

        /// <summary>
        ///     Gets statistics about the hash database.
        /// </summary>
        HashDbStats GetStats();

        /// <summary>
        ///     Gets the current schema version of the database.
        /// </summary>
        int GetSchemaVersion();

        /// <summary>
        ///     Stores or updates an album target in the database.
        /// </summary>
        Task UpsertAlbumTargetAsync(AlbumTarget target, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets an album target by release id.
        /// </summary>
        Task<AlbumTargetEntry?> GetAlbumTargetAsync(string releaseId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the stored track list for a release.
        /// </summary>
        Task<IEnumerable<AlbumTargetTrackEntry>> GetAlbumTracksAsync(string releaseId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the stored album targets.
        /// </summary>
        Task<IEnumerable<AlbumTargetEntry>> GetAlbumTargetsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Looks up hash entries by MusicBrainz recording identifier.
        /// </summary>
        Task<IEnumerable<HashDbEntry>> LookupHashesByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default);

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

        /// <summary>
        ///     Updates the stored fingerprint for a hash entry.
        /// </summary>
        Task UpdateHashFingerprintAsync(string flacKey, string fingerprint, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates variant metadata (quality scoring, codec profile, etc.).
        /// </summary>
        Task UpdateVariantMetadataAsync(string flacKey, AudioVariant variant, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get all variants for a recording.
        /// </summary>
        Task<List<AudioVariant>> GetVariantsByRecordingAsync(string recordingId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get variants for a recording and codec profile key.
        /// </summary>
        Task<List<AudioVariant>> GetVariantsByRecordingAndProfileAsync(string recordingId, string codecProfileKey, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Upsert canonical stats entry.
        /// </summary>
        Task UpsertCanonicalStatsAsync(CanonicalStats stats, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get canonical stats for recording/profile.
        /// </summary>
        Task<CanonicalStats?> GetCanonicalStatsAsync(string recordingId, string codecProfileKey, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get recording IDs that have variants.
        /// </summary>
        Task<List<string>> GetRecordingIdsWithVariantsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get codec profile keys present for a recording.
        /// </summary>
        Task<List<string>> GetCodecProfilesForRecordingAsync(string recordingId, CancellationToken cancellationToken = default);

        // ========== MusicBrainz Release Graph Cache ==========

        /// <summary>
        ///     Gets a cached artist release graph (if any).
        /// </summary>
        Task<Integrations.MusicBrainz.Models.ArtistReleaseGraph?> GetArtistReleaseGraphAsync(string artistId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Upserts a cached artist release graph.
        /// </summary>
        Task UpsertArtistReleaseGraphAsync(Integrations.MusicBrainz.Models.ArtistReleaseGraph graph, CancellationToken cancellationToken = default);

        // ========== Discography Jobs ==========

        Task<Jobs.DiscographyJob?> GetDiscographyJobAsync(string jobId, CancellationToken cancellationToken = default);

        Task UpsertDiscographyJobAsync(Jobs.DiscographyJob job, CancellationToken cancellationToken = default);

        Task<List<Jobs.DiscographyReleaseJobStatus>> GetDiscographyReleaseJobsAsync(string jobId, CancellationToken cancellationToken = default);

        Task UpsertDiscographyReleaseJobsAsync(string jobId, IEnumerable<Jobs.DiscographyReleaseJobStatus> releases, CancellationToken cancellationToken = default);

        Task SetDiscographyReleaseJobStatusAsync(string jobId, string releaseId, Jobs.JobStatus status, CancellationToken cancellationToken = default);

        // ========== Label Presence ==========

        /// <summary>
        ///     Aggregates local label presence counts from AlbumTargets.
        /// </summary>
        Task<IReadOnlyList<slskd.Integrations.MusicBrainz.Models.LabelPresence>> GetLabelPresenceAsync(CancellationToken cancellationToken = default);

        // ========== Label Crate Jobs ==========

        Task<Jobs.LabelCrateJob?> GetLabelCrateJobAsync(string jobId, CancellationToken cancellationToken = default);

        Task UpsertLabelCrateJobAsync(Jobs.LabelCrateJob job, CancellationToken cancellationToken = default);

        Task<List<Jobs.DiscographyReleaseJobStatus>> GetLabelCrateReleaseJobsAsync(string jobId, CancellationToken cancellationToken = default);

        Task UpsertLabelCrateReleaseJobsAsync(string jobId, IEnumerable<Jobs.DiscographyReleaseJobStatus> releases, CancellationToken cancellationToken = default);

        Task SetLabelCrateReleaseJobStatusAsync(string jobId, string releaseId, Jobs.JobStatus status, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetReleaseIdsByLabelAsync(string labelNameOrId, int limit, CancellationToken cancellationToken = default);

        // ========== Traffic Accounting ==========

        /// <summary>
        ///     Gets aggregate traffic counters for overlay and Soulseek.
        /// </summary>
        Task<Models.TrafficTotals> GetTrafficTotalsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Adds to traffic counters (can pass zero for unused directions).
        /// </summary>
        Task AddTrafficAsync(long overlayUpload, long overlayDownload, long soulseekUpload, long soulseekDownload, CancellationToken cancellationToken = default);

        // ========== Warm Cache Popularity ==========

        /// <summary>
        ///     Increment popularity for a content id (e.g., MB release or recording).
        /// </summary>
        Task IncrementPopularityAsync(string contentId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get top popular content IDs above threshold.
        /// </summary>
        Task<IReadOnlyList<(string ContentId, long Hits)>> GetTopPopularAsync(int limit, long minHits = 1, CancellationToken cancellationToken = default);

        // ========== Warm Cache Entries ==========

        Task UpsertWarmCacheEntryAsync(Models.WarmCacheEntry entry, CancellationToken cancellationToken = default);

        Task DeleteWarmCacheEntryAsync(string contentId, CancellationToken cancellationToken = default);

        Task<Models.WarmCacheEntry?> GetWarmCacheEntryAsync(string contentId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Models.WarmCacheEntry>> ListWarmCacheEntriesAsync(CancellationToken cancellationToken = default);

        Task<long> GetWarmCacheTotalSizeAsync(CancellationToken cancellationToken = default);

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

        // ========== History Backfill ==========

        /// <summary>
        ///     Backfills FLAC inventory from search history responses.
        /// </summary>
        /// <param name="responses">Search responses to process.</param>
        /// <param name="maxFiles">Maximum files to process (default unlimited).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of FLAC files discovered.</returns>
        Task<int> BackfillFromSearchResponsesAsync(IEnumerable<Search.Response> responses, int maxFiles = int.MaxValue, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the backfill progress marker (timestamp of last processed search).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The timestamp of the oldest processed search, or null if no progress.</returns>
        Task<DateTimeOffset?> GetBackfillProgressAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Sets the backfill progress marker.
        /// </summary>
        /// <param name="oldestProcessed">Timestamp of the oldest search processed in this batch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetBackfillProgressAsync(DateTimeOffset oldestProcessed, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates a hash entry with an AcoustID-resolution MusicBrainz recording ID.
        /// </summary>
        Task UpdateHashRecordingIdAsync(string flacKey, string musicBrainzId, CancellationToken cancellationToken = default);

        // ========== Library Health ==========
        Task UpsertLibraryHealthScanAsync(LibraryHealth.LibraryHealthScan scan, CancellationToken cancellationToken = default);
        Task<LibraryHealth.LibraryHealthScan?> GetLibraryHealthScanAsync(string scanId, CancellationToken cancellationToken = default);
        Task<List<LibraryHealth.LibraryIssue>> GetLibraryIssuesAsync(LibraryHealth.LibraryHealthIssueFilter filter, CancellationToken cancellationToken = default);
        Task UpdateLibraryIssueStatusAsync(string issueId, LibraryHealth.LibraryIssueStatus status, CancellationToken cancellationToken = default);
        Task InsertLibraryIssueAsync(LibraryHealth.LibraryIssue issue, CancellationToken cancellationToken = default);

        // Peer metrics
        Task<Transfers.MultiSource.Metrics.PeerPerformanceMetrics> GetPeerMetricsAsync(string peerId, CancellationToken cancellationToken = default);
        Task UpsertPeerMetricsAsync(Transfers.MultiSource.Metrics.PeerPerformanceMetrics metrics, CancellationToken cancellationToken = default);
        Task<List<Transfers.MultiSource.Metrics.PeerPerformanceMetrics>> GetAllPeerMetricsAsync(CancellationToken cancellationToken = default);
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


