// <copyright file="HashDbService.cs" company="slskdn Team">
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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Options;
    using Serilog;
    using slskd;
    using slskd.Audio;
    using slskd.Capabilities;
    using slskd.Events;
    using slskd.HashDb.Models;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.AutoTagging;
    using slskd.Integrations.Chromaprint;
    using slskd.Integrations.MusicBrainz;
    using slskd.Integrations.MusicBrainz.Models;
    using slskdOptions = slskd.Options;
    using slskd.Mesh;
    using TagLib;

    /// <summary>
    ///     SQLite-based hash database service.
    /// </summary>
    public class HashDbService : IHashDbService
    {
        /// <summary>
        ///     Size of verification chunk for hashing (32KB).
        /// </summary>
        private const int HashChunkSize = 32768;

        private readonly string dbPath;
        private readonly ILogger log = Log.ForContext<HashDbService>();
        private readonly IServiceProvider serviceProvider;
        private readonly IFingerprintExtractionService fingerprintExtractionService;
        private readonly IAcoustIdClient acoustIdClient;
        private readonly IAutoTaggingService autoTaggingService;
        private readonly IMusicBrainzClient musicBrainzClient;
        private readonly IOptionsMonitor<slskdOptions> optionsMonitor;
        private readonly QualityScorer qualityScorer = new();
        private readonly TranscodeDetector transcodeDetector = new();
        private long currentSeqId;

        /// <summary>
        ///     Initializes a new instance of the <see cref="HashDbService"/> class.
        /// </summary>
        /// <param name="appDirectory">The application data directory.</param>
        /// <param name="eventBus">The event bus for subscribing to download events (optional).</param>
        /// <param name="serviceProvider">Service provider for lazy resolution of mesh sync (optional).</param>
        public HashDbService(
            string appDirectory,
            EventBus eventBus = null,
            IServiceProvider serviceProvider = null,
            IFingerprintExtractionService fingerprintExtractionService = null,
            IAcoustIdClient acoustIdClient = null,
            IAutoTaggingService autoTaggingService = null,
            IMusicBrainzClient musicBrainzClient = null,
            IOptionsMonitor<slskdOptions> optionsMonitor = null)
        {
            this.serviceProvider = serviceProvider;
            this.fingerprintExtractionService = fingerprintExtractionService;
            this.acoustIdClient = acoustIdClient;
            this.autoTaggingService = autoTaggingService;
            this.musicBrainzClient = musicBrainzClient;
            this.optionsMonitor = optionsMonitor;
            dbPath = Path.Combine(appDirectory, "hashdb.db");
            InitializeDatabase();
            currentSeqId = GetLatestSeqIdSync();
            log.Information("[HashDb] Initialized at {Path}, current seq_id: {SeqId}", dbPath, currentSeqId);

            // Subscribe to events for hash discovery
            if (eventBus != null)
            {
                // Hash downloaded files
                eventBus.Subscribe<DownloadFileCompleteEvent>("HashDbService.DownloadComplete", OnDownloadCompleteAsync);

                // Discover FLAC files from search results for passive hash gathering
                eventBus.Subscribe<SearchResponsesReceivedEvent>("HashDbService.SearchResponses", OnSearchResponsesReceivedAsync);

                // Track peers who interact with us (for future passive browsing)
                eventBus.Subscribe<PeerSearchedUsEvent>("HashDbService.PeerSearchedUs", OnPeerSearchedUsAsync);
                eventBus.Subscribe<PeerDownloadedFromUsEvent>("HashDbService.PeerDownloadedFromUs", OnPeerDownloadedFromUsAsync);

                log.Information("[HashDb] Subscribed to download, search, and peer interaction events for hash discovery");
            }
        }

        /// <summary>
        ///     Handles when a peer searches us - track them for future passive FLAC discovery.
        /// </summary>
        private async Task OnPeerSearchedUsAsync(PeerSearchedUsEvent evt)
        {
            try
            {
                // Track this peer - they're active on the network and might have FLACs we want
                await GetOrCreatePeerAsync(evt.Username);
                await TouchPeerAsync(evt.Username);
                log.Debug("[HashDb] Tracked peer {Username} who searched us (had results: {HadResults})", evt.Username, evt.HadResults);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Error tracking peer {Username} from search", evt.Username);
            }
        }

        /// <summary>
        ///     Handles when a peer downloads from us - track them for future passive FLAC discovery.
        /// </summary>
        private async Task OnPeerDownloadedFromUsAsync(PeerDownloadedFromUsEvent evt)
        {
            try
            {
                // Track this peer - they're active and downloading, good candidate for FLAC discovery
                await GetOrCreatePeerAsync(evt.Username);
                await TouchPeerAsync(evt.Username);
                log.Debug("[HashDb] Tracked peer {Username} who downloaded {File}", evt.Username, Path.GetFileName(evt.Filename));
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Error tracking peer {Username} from download", evt.Username);
            }
        }

        /// <summary>
        ///     Handles search responses by discovering FLAC files for the inventory.
        /// </summary>
        private async Task OnSearchResponsesReceivedAsync(SearchResponsesReceivedEvent evt)
        {
            try
            {
                var flacCount = 0;
                var skippedCount = 0;

                foreach (var response in evt.Responses)
                {
                    foreach (var file in response.Files)
                    {
                        // Only interested in FLAC files large enough to hash
                        if (!file.Filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (file.Size < HashChunkSize)
                        {
                            skippedCount++;
                            continue;
                        }

                        // Add to inventory for backfill probing
                        var entry = new FlacInventoryEntry
                        {
                            PeerId = response.Username,
                            Path = file.Filename,
                            Size = file.Size,
                            HashStatusStr = "none",
                        };

                        await UpsertFlacEntryAsync(entry);
                        flacCount++;
                    }
                }

                if (flacCount > 0)
                {
                    log.Information("[HashDb] Discovered {Count} FLAC files from search results ({Skipped} too small)", flacCount, skippedCount);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Error processing search responses for FLAC discovery");
            }
        }

        /// <summary>
        ///     Handles download completion by hashing the file and storing in the database.
        /// </summary>
        private async Task OnDownloadCompleteAsync(DownloadFileCompleteEvent evt)
        {
            try
            {
                var localFilename = evt.LocalFilename;
                var fileSize = evt.Transfer.Size;

                // Only hash files that are large enough
                if (fileSize < HashChunkSize)
                {
                    log.Debug("[HashDb] Skipping hash for {Filename}: file too small ({Size} bytes)", localFilename, fileSize);
                    return;
                }

                // Check if we already have a hash for this file
                var flacKey = HashDbEntry.GenerateFlacKey(evt.RemoteFilename, fileSize);
                var existing = await LookupHashAsync(flacKey);
                if (existing != null)
                {
                    log.Debug("[HashDb] Hash already exists for {Filename}", localFilename);
                    await IncrementHashUseCountAsync(flacKey);
                    return;
                }

                // Hash the first 32KB of the downloaded file
                var hash = await ComputeFileHashAsync(localFilename);
                if (hash == null)
                {
                    log.Warning("[HashDb] Failed to compute hash for {Filename}", localFilename);
                    return;
                }

                // Store the hash locally
                await StoreHashFromVerificationAsync(evt.RemoteFilename, fileSize, hash);
                log.Information("[HashDb] Stored hash for downloaded file {Filename}: {Hash}", Path.GetFileName(localFilename), hash.Substring(0, 16) + "...");

                // Derive variant metadata + quality score
                try
                {
                    var variant = BuildVariantFromFile(localFilename, flacKey, fileSize);
                    await UpdateVariantMetadataAsync(flacKey, variant).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] Failed to derive audio variant metadata for {Filename}", localFilename);
                }

                if (fingerprintExtractionService != null)
                {
                    try
                    {
                        var fingerprint = await fingerprintExtractionService.ExtractFingerprintAsync(localFilename).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(fingerprint))
                        {
                            await UpdateHashFingerprintAsync(flacKey, fingerprint).ConfigureAwait(false);
                            log.Debug("[HashDb] Stored fingerprint for {Filename}", localFilename);

                            await TryResolveAcoustIdAsync(localFilename, flacKey, fingerprint).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, "[HashDb] Fingerprint extraction failed for {Filename}", localFilename);
                    }
                }

                // Publish to mesh for other slskdn clients (lazy resolution to avoid circular dependency)
                if (serviceProvider != null)
                {
                    var meshSync = serviceProvider.GetService(typeof(IMeshSyncService)) as IMeshSyncService;
                    if (meshSync != null)
                    {
                        await meshSync.PublishHashAsync(flacKey, hash, fileSize);
                        log.Debug("[HashDb] Published hash to mesh: {Key}", flacKey);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "[HashDb] Error hashing downloaded file {Filename}", evt.LocalFilename);
            }
        }

        /// <summary>
        ///     Computes SHA256 hash of the first 32KB of a file.
        /// </summary>
        private async Task<string> ComputeFileHashAsync(string filename)
        {
            try
            {
                if (!System.IO.File.Exists(filename))
                {
                    return null;
                }

                var buffer = new byte[HashChunkSize];
                int bytesRead;

                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, HashChunkSize, useAsync: true))
                {
                    bytesRead = await fs.ReadAsync(buffer, 0, HashChunkSize);
                }

                if (bytesRead == 0)
                {
                    return null;
                }

                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(buffer, 0, bytesRead);
                return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Error reading file for hashing: {Filename}", filename);
                return null;
            }
        }

        /// <inheritdoc/>
        public long CurrentSeqId => currentSeqId;

        private AudioVariant BuildVariantFromFile(string filePath, string flacKey, long fileSize)
        {
            using var tagFile = TagLib.File.Create(filePath);
            var props = tagFile.Properties;
            var (codec, container) = MapCodecAndContainer(filePath, props);

            var variant = new AudioVariant
            {
                VariantId = flacKey,
                FlacKey = flacKey,
                MusicBrainzRecordingId = null,  // May be populated later via AcoustID/MB
                Codec = codec,
                Container = container,
                SampleRateHz = props.AudioSampleRate,
                BitDepth = props.BitsPerSample == 0 ? null : props.BitsPerSample,
                Channels = props.AudioChannels,
                DurationMs = (int)props.Duration.TotalMilliseconds,
                BitrateKbps = props.AudioBitrate,
                FileSizeBytes = fileSize,
                FirstSeenAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                SeenCount = 1,
                EncoderSignature = props.Codecs.FirstOrDefault()?.Description,
            };

            variant.QualityScore = qualityScorer.ComputeQualityScore(variant);
            var (suspect, reason) = transcodeDetector.DetectTranscode(variant);
            variant.TranscodeSuspect = suspect;
            variant.TranscodeReason = reason;

            return variant;
        }

        private static (string Codec, string Container) MapCodecAndContainer(string filePath, TagLib.Properties props)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            var container = string.IsNullOrWhiteSpace(ext) ? "UNKNOWN" : ext.TrimStart('.').ToUpperInvariant();

            var codec = ext switch
            {
                ".flac" => "FLAC",
                ".alac" => "ALAC",
                ".m4a" => "ALAC",
                ".aac" => "AAC",
                ".mp3" => "MP3",
                ".opus" => "Opus",
                ".ogg" => "Vorbis",
                ".wav" => "WAV",
                _ => props.Codecs.FirstOrDefault()?.Description ?? "Unknown",
            };

            return (codec, container);
        }

        private SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            return conn;
        }

        private void InitializeDatabase()
        {
            using var conn = GetConnection();

            // Run versioned migrations
            var migrationsApplied = Migrations.HashDbMigrations.RunMigrations(conn);
            if (migrationsApplied > 0)
            {
                log.Information("[HashDb] Applied {Count} migrations", migrationsApplied);
            }
        }

        private long GetLatestSeqIdSync()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MAX(seq_id), 0) FROM HashDb";
            var result = cmd.ExecuteScalar();
            return result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        /// <inheritdoc/>
        public HashDbStats GetStats()
        {
            using var conn = GetConnection();
            var stats = new HashDbStats { CurrentSeqId = currentSeqId };

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM Peers";
                stats.TotalPeers = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM Peers WHERE caps > 0";
                stats.SlskdnPeers = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM FlacInventory";
                stats.TotalFlacEntries = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM FlacInventory WHERE hash_status = 'known'";
                stats.HashedFlacEntries = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM HashDb";
                stats.TotalHashEntries = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (System.IO.File.Exists(dbPath))
            {
                stats.DatabaseSizeBytes = new FileInfo(dbPath).Length;
            }

            return stats;
        }

        /// <inheritdoc/>
        public int GetSchemaVersion()
        {
            using var conn = GetConnection();
            return Migrations.HashDbMigrations.GetCurrentVersion(conn);
        }

        /// <inheritdoc/>
        public async Task UpsertAlbumTargetAsync(AlbumTarget target, CancellationToken cancellationToken = default)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            using var conn = GetConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                await UpsertAlbumTargetInternalAsync(conn, target, cancellationToken).ConfigureAwait(false);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AlbumTargetEntry?> GetAlbumTargetAsync(string releaseId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT release_id, discogs_release_id, title, artist, metadata_release_date, metadata_country, metadata_label, metadata_status, created_at
                FROM AlbumTargets
                WHERE release_id = @release_id";
            cmd.Parameters.AddWithValue("@release_id", releaseId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ReadAlbumTargetEntry(reader);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AlbumTargetTrackEntry>> GetAlbumTracksAsync(string releaseId, CancellationToken cancellationToken = default)
        {
            var tracks = new List<AlbumTargetTrackEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT release_id, track_position, recording_id, title, artist, duration_ms, isrc
                FROM AlbumTargetTracks
                WHERE release_id = @release_id
                ORDER BY track_position ASC";
            cmd.Parameters.AddWithValue("@release_id", releaseId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                tracks.Add(ReadAlbumTargetTrackEntry(reader));
            }

            return tracks;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AlbumTargetEntry>> GetAlbumTargetsAsync(CancellationToken cancellationToken = default)
        {
            var targets = new List<AlbumTargetEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT release_id, discogs_release_id, title, artist, metadata_release_date, metadata_country, metadata_label, metadata_status, created_at
                FROM AlbumTargets
                ORDER BY created_at DESC";

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                targets.Add(ReadAlbumTargetEntry(reader));
            }

            return targets;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<HashDbEntry>> LookupHashesByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            var matches = new List<HashDbEntry>();
            if (string.IsNullOrEmpty(recordingId))
            {
                return matches;
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT *
                FROM HashDb
                WHERE musicbrainz_id = @mbid
                ORDER BY last_updated_at DESC";
            cmd.Parameters.AddWithValue("@mbid", recordingId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                matches.Add(ReadHashEntry(reader));
            }

            return matches;
        }

        private static AlbumTargetEntry ReadAlbumTargetEntry(SqliteDataReader reader)
        {
            return new AlbumTargetEntry
            {
                ReleaseId = reader.GetString(0),
                DiscogsReleaseId = reader.IsDBNull(1) ? null : reader.GetString(1),
                Title = reader.GetString(2),
                Artist = reader.GetString(3),
                ReleaseDate = reader.IsDBNull(4) ? null : reader.GetString(4),
                Country = reader.IsDBNull(5) ? null : reader.GetString(5),
                Label = reader.IsDBNull(6) ? null : reader.GetString(6),
                Status = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedAt = reader.GetInt64(8),
            };
        }

        private static AlbumTargetTrackEntry ReadAlbumTargetTrackEntry(SqliteDataReader reader)
        {
            return new AlbumTargetTrackEntry
            {
                ReleaseId = reader.GetString(0),
                Position = reader.GetInt32(1),
                RecordingId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Title = reader.GetString(3),
                Artist = reader.GetString(4),
                DurationMs = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Isrc = reader.IsDBNull(6) ? null : reader.GetString(6),
            };
        }

        private async Task UpsertAlbumTargetInternalAsync(SqliteConnection conn, AlbumTarget target, CancellationToken cancellationToken)
        {
            using var albumCmd = conn.CreateCommand();
            albumCmd.CommandText = @"
                INSERT INTO AlbumTargets (
                    release_id,
                    discogs_release_id,
                    title,
                    artist,
                    metadata_release_date,
                    metadata_country,
                    metadata_label,
                    metadata_status
                )
                VALUES (
                    @release_id,
                    @discogs_release_id,
                    @title,
                    @artist,
                    @metadata_release_date,
                    @metadata_country,
                    @metadata_label,
                    @metadata_status
                )
                ON CONFLICT(release_id) DO UPDATE SET
                    discogs_release_id = excluded.discogs_release_id,
                    title = excluded.title,
                    artist = excluded.artist,
                    metadata_release_date = excluded.metadata_release_date,
                    metadata_country = excluded.metadata_country,
                    metadata_label = excluded.metadata_label,
                    metadata_status = excluded.metadata_status;
            ";
            albumCmd.Parameters.AddWithValue("@release_id", target.MusicBrainzReleaseId);
            albumCmd.Parameters.AddWithValue("@discogs_release_id", (object?)target.DiscogsReleaseId ?? DBNull.Value);
            albumCmd.Parameters.AddWithValue("@title", target.Title);
            albumCmd.Parameters.AddWithValue("@artist", target.Artist);
            albumCmd.Parameters.AddWithValue("@metadata_release_date", (object?)FormatReleaseDate(target.Metadata.ReleaseDate) ?? DBNull.Value);
            albumCmd.Parameters.AddWithValue("@metadata_country", (object?)target.Metadata.Country ?? DBNull.Value);
            albumCmd.Parameters.AddWithValue("@metadata_label", (object?)target.Metadata.Label ?? DBNull.Value);
            albumCmd.Parameters.AddWithValue("@metadata_status", (object?)target.Metadata.Status ?? DBNull.Value);
            await albumCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await DeleteAlbumTracksAsync(conn, target.MusicBrainzReleaseId, cancellationToken).ConfigureAwait(false);

            var tracks = target.Tracks ?? Array.Empty<TrackTarget>();
            for (var i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                using var trackCmd = conn.CreateCommand();
                trackCmd.CommandText = @"
                    INSERT INTO AlbumTargetTracks (
                        release_id,
                        track_position,
                        recording_id,
                        title,
                        artist,
                        duration_ms,
                        isrc
                    )
                    VALUES (
                        @release_id,
                        @track_position,
                        @recording_id,
                        @title,
                        @artist,
                        @duration_ms,
                        @isrc
                    )
                    ON CONFLICT(release_id, track_position) DO UPDATE SET
                        recording_id = excluded.recording_id,
                        title = excluded.title,
                        artist = excluded.artist,
                        duration_ms = excluded.duration_ms,
                        isrc = excluded.isrc;
                ";
                trackCmd.Parameters.AddWithValue("@release_id", target.MusicBrainzReleaseId);
                trackCmd.Parameters.AddWithValue("@track_position", track.Position == 0 ? i + 1 : track.Position);
                trackCmd.Parameters.AddWithValue("@recording_id", (object?)track.MusicBrainzRecordingId ?? DBNull.Value);
                trackCmd.Parameters.AddWithValue("@title", track.Title);
                trackCmd.Parameters.AddWithValue("@artist", track.Artist);
                trackCmd.Parameters.AddWithValue("@duration_ms", (object?)DurationToMilliseconds(track.Duration) ?? DBNull.Value);
                trackCmd.Parameters.AddWithValue("@isrc", (object?)track.Isrc ?? DBNull.Value);
                await trackCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static Task DeleteAlbumTracksAsync(SqliteConnection conn, string releaseId, CancellationToken cancellationToken)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM AlbumTargetTracks WHERE release_id = @release_id";
            cmd.Parameters.AddWithValue("@release_id", releaseId);
            return cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string? FormatReleaseDate(DateOnly? releaseDate)
        {
            return releaseDate?.ToString("yyyy-MM-dd");
        }

        private static int? DurationToMilliseconds(TimeSpan duration)
        {
            if (duration == TimeSpan.Zero)
            {
                return null;
            }

            var ms = (int)Math.Min(int.MaxValue, Math.Max(0, (int)Math.Round(duration.TotalMilliseconds)));
            return ms == 0 ? (int?)null : ms;
        }

        // ========== Peer Management ==========

        /// <inheritdoc/>
        public async Task<Peer> GetOrCreatePeerAsync(string username, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Try to get existing
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM Peers WHERE peer_id = @peer_id";
                cmd.Parameters.AddWithValue("@peer_id", username);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return ReadPeer(reader);
                }
            }

            // Create new
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO Peers (peer_id, last_seen) VALUES (@peer_id, @last_seen)";
                cmd.Parameters.AddWithValue("@peer_id", username);
                cmd.Parameters.AddWithValue("@last_seen", now);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return new Peer { PeerId = username, LastSeen = now };
        }

        /// <inheritdoc/>
        public async Task UpdatePeerCapabilitiesAsync(string username, PeerCapabilityFlags caps, string clientVersion = null, CancellationToken cancellationToken = default)
        {
            await GetOrCreatePeerAsync(username, cancellationToken);

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Peers 
                SET caps = @caps, 
                    client_version = COALESCE(@version, client_version),
                    last_cap_check = @now,
                    last_seen = @now
                WHERE peer_id = @peer_id";
            cmd.Parameters.AddWithValue("@peer_id", username);
            cmd.Parameters.AddWithValue("@caps", (int)caps);
            cmd.Parameters.AddWithValue("@version", clientVersion ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Peer>> GetSlskdnPeersAsync(CancellationToken cancellationToken = default)
        {
            var peers = new List<Peer>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Peers WHERE caps > 0 ORDER BY last_seen DESC";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                peers.Add(ReadPeer(reader));
            }

            return peers;
        }

        /// <inheritdoc/>
        public async Task TouchPeerAsync(string username, CancellationToken cancellationToken = default)
        {
            await GetOrCreatePeerAsync(username, cancellationToken);

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Peers SET last_seen = @now WHERE peer_id = @peer_id";
            cmd.Parameters.AddWithValue("@peer_id", username);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ========== FLAC Inventory ==========

        /// <inheritdoc/>
        public async Task UpsertFlacEntryAsync(FlacInventoryEntry entry, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(entry.FileId))
            {
                entry.FileId = FlacInventoryEntry.GenerateFileId(entry.PeerId, entry.Path, entry.Size);
            }

            if (entry.DiscoveredAt == 0)
            {
                entry.DiscoveredAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FlacInventory (file_id, peer_id, path, size, discovered_at, hash_status, hash_value, hash_source, flac_audio_md5, sample_rate, channels, bit_depth, duration_samples)
                VALUES (@file_id, @peer_id, @path, @size, @discovered_at, @hash_status, @hash_value, @hash_source, @flac_audio_md5, @sample_rate, @channels, @bit_depth, @duration_samples)
                ON CONFLICT(file_id) DO UPDATE SET
                    hash_status = COALESCE(@hash_status, hash_status),
                    hash_value = COALESCE(@hash_value, hash_value),
                    hash_source = COALESCE(@hash_source, hash_source),
                    flac_audio_md5 = COALESCE(@flac_audio_md5, flac_audio_md5),
                    sample_rate = COALESCE(@sample_rate, sample_rate),
                    channels = COALESCE(@channels, channels),
                    bit_depth = COALESCE(@bit_depth, bit_depth),
                    duration_samples = COALESCE(@duration_samples, duration_samples)";

            cmd.Parameters.AddWithValue("@file_id", entry.FileId);
            cmd.Parameters.AddWithValue("@peer_id", entry.PeerId);
            cmd.Parameters.AddWithValue("@path", entry.Path);
            cmd.Parameters.AddWithValue("@size", entry.Size);
            cmd.Parameters.AddWithValue("@discovered_at", entry.DiscoveredAt);
            cmd.Parameters.AddWithValue("@hash_status", entry.HashStatusStr ?? "none");
            cmd.Parameters.AddWithValue("@hash_value", entry.HashValue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hash_source", entry.HashSourceStr ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_audio_md5", entry.FlacAudioMd5 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sample_rate", entry.SampleRate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@channels", entry.Channels ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@bit_depth", entry.BitDepth ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@duration_samples", entry.DurationSamples ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FlacInventoryEntry> GetFlacEntryAsync(string fileId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM FlacInventory WHERE file_id = @file_id";
            cmd.Parameters.AddWithValue("@file_id", fileId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadFlacEntry(reader);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<FlacInventoryEntry>> GetFlacEntriesBySizeAsync(long size, int limit = 100, CancellationToken cancellationToken = default)
        {
            var entries = new List<FlacInventoryEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM FlacInventory WHERE size = @size ORDER BY discovered_at DESC LIMIT @limit";
            cmd.Parameters.AddWithValue("@size", size);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadFlacEntry(reader));
            }

            return entries;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<FlacInventoryEntry>> GetUnhashedFlacFilesAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            var entries = new List<FlacInventoryEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM FlacInventory 
                WHERE hash_status = 'none' OR hash_status = 'pending'
                ORDER BY discovered_at DESC 
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadFlacEntry(reader));
            }

            return entries;
        }

        /// <inheritdoc/>
        public async Task UpdateFlacHashAsync(string fileId, string hashValue, HashSource source, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE FlacInventory 
                SET hash_status = 'known', hash_value = @hash_value, hash_source = @hash_source
                WHERE file_id = @file_id";
            cmd.Parameters.AddWithValue("@file_id", fileId);
            cmd.Parameters.AddWithValue("@hash_value", hashValue);
            cmd.Parameters.AddWithValue("@hash_source", source.ToString().ToLowerInvariant());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task MarkFlacHashFailedAsync(string fileId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE FlacInventory SET hash_status = 'failed' WHERE file_id = @file_id";
            cmd.Parameters.AddWithValue("@file_id", fileId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ========== Hash Database ==========

        /// <inheritdoc/>
        public async Task<HashDbEntry> LookupHashAsync(string flacKey, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM HashDb WHERE flac_key = @flac_key";
            cmd.Parameters.AddWithValue("@flac_key", flacKey);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadHashEntry(reader);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<HashDbEntry>> LookupHashesBySizeAsync(long size, CancellationToken cancellationToken = default)
        {
            var entries = new List<HashDbEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM HashDb WHERE size = @size ORDER BY use_count DESC";
            cmd.Parameters.AddWithValue("@size", size);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadHashEntry(reader));
            }

            return entries;
        }

        /// <inheritdoc/>
        public async Task StoreHashAsync(HashDbEntry entry, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var newSeqId = Interlocked.Increment(ref currentSeqId);

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO HashDb (flac_key, byte_hash, size, meta_flags, first_seen_at, last_updated_at, seq_id, use_count)
                VALUES (@flac_key, @byte_hash, @size, @meta_flags, @now, @now, @seq_id, 1)
                ON CONFLICT(flac_key) DO UPDATE SET
                    byte_hash = @byte_hash,
                    last_updated_at = @now,
                    seq_id = @seq_id,
                    use_count = use_count + 1";

            cmd.Parameters.AddWithValue("@flac_key", entry.FlacKey);
            cmd.Parameters.AddWithValue("@byte_hash", entry.ByteHash);
            cmd.Parameters.AddWithValue("@size", entry.Size);
            cmd.Parameters.AddWithValue("@meta_flags", entry.MetaFlags ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.Parameters.AddWithValue("@seq_id", newSeqId);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StoreHashFromVerificationAsync(string filename, long size, string byteHash, int? sampleRate = null, int? channels = null, int? bitDepth = null, CancellationToken cancellationToken = default)
        {
            var flacKey = HashDbEntry.GenerateFlacKey(filename, size);
            int? metaFlags = null;

            if (sampleRate.HasValue && channels.HasValue && bitDepth.HasValue)
            {
                metaFlags = HashDbEntry.PackMetaFlags(sampleRate.Value, channels.Value, bitDepth.Value);
            }

            await StoreHashAsync(new HashDbEntry
            {
                FlacKey = flacKey,
                ByteHash = byteHash,
                Size = size,
                MetaFlags = metaFlags,
            }, cancellationToken);

            log.Debug("[HashDb] Stored hash {Key} -> {Hash} for {File} ({Size} bytes)", flacKey, byteHash.Substring(0, 16) + "...", filename, size);
        }

        /// <inheritdoc/>
        public async Task UpdateHashRecordingIdAsync(string flacKey, string musicBrainzId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(musicBrainzId))
            {
                return;
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE HashDb
                SET musicbrainz_id = @mbid, last_updated_at = @now
                WHERE flac_key = @flac_key";
            cmd.Parameters.AddWithValue("@mbid", musicBrainzId);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@flac_key", flacKey);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task TryResolveAcoustIdAsync(string filePath, string flacKey, string fingerprint, CancellationToken cancellationToken = default)
        {
            if (acoustIdClient == null || optionsMonitor == null || musicBrainzClient == null)
            {
                return;
            }

            var chromaOptions = optionsMonitor.CurrentValue.Integration.Chromaprint;
            var result = await acoustIdClient.LookupAsync(fingerprint, chromaOptions.SampleRate, chromaOptions.DurationSeconds, cancellationToken).ConfigureAwait(false);

            var recordingId = result?.Recordings?.FirstOrDefault()?.Id;

            if (string.IsNullOrWhiteSpace(recordingId))
            {
                log.Debug("[HashDb] AcoustID did not resolve a recording for fingerprint {Fingerprint}", fingerprint);
                return;
            }

            await UpdateHashRecordingIdAsync(flacKey, recordingId, cancellationToken).ConfigureAwait(false);
            log.Information("[HashDb] Resolved AcoustID fingerprint to recording {RecordingId} for key {FlacKey}", recordingId, flacKey);

            var track = await musicBrainzClient.GetRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);

            if (track is not null && autoTaggingService != null)
            {
                try
                {
                    var tagResult = await autoTaggingService.TagAsync(filePath, track, cancellationToken).ConfigureAwait(false);
                    if (tagResult?.Updated == true)
                    {
                        log.Information("[HashDb] Auto-tagged {File}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] Auto-tagging failed for {File}", filePath);
                }
            }
        }

        /// <inheritdoc/>
        public async Task UpdateHashFingerprintAsync(string flacKey, string fingerprint, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return;
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE HashDb
                SET audio_fingerprint = @fingerprint,
                    last_updated_at = @now
                WHERE flac_key = @flac_key";
            cmd.Parameters.AddWithValue("@fingerprint", fingerprint);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@flac_key", flacKey);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task UpdateVariantMetadataAsync(string flacKey, AudioVariant variant, CancellationToken cancellationToken = default)
        {
            if (variant == null)
            {
                return;
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE HashDb
                SET variant_id = @variant_id,
                    codec = @codec,
                    container = @container,
                    sample_rate_hz = @sample_rate_hz,
                    bit_depth = @bit_depth,
                    channels = @channels,
                    duration_ms = @duration_ms,
                    bitrate_kbps = @bitrate_kbps,
                    quality_score = @quality_score,
                    transcode_suspect = @transcode_suspect,
                    transcode_reason = @transcode_reason,
                    dynamic_range_dr = @dynamic_range_dr,
                    loudness_lufs = @loudness_lufs,
                    has_clipping = @has_clipping,
                    encoder_signature = @encoder_signature,
                    seen_count = COALESCE(seen_count, 1),
                    file_sha256 = @file_sha256,
                    musicbrainz_id = COALESCE(musicbrainz_id, @recording_id),
                    last_updated_at = @now
                WHERE flac_key = @flac_key";

            cmd.Parameters.AddWithValue("@variant_id", variant.VariantId ?? flacKey);
            cmd.Parameters.AddWithValue("@codec", (object?)variant.Codec ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@container", (object?)variant.Container ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sample_rate_hz", variant.SampleRateHz);
            cmd.Parameters.AddWithValue("@bit_depth", (object?)variant.BitDepth ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@channels", variant.Channels);
            cmd.Parameters.AddWithValue("@duration_ms", variant.DurationMs);
            cmd.Parameters.AddWithValue("@bitrate_kbps", variant.BitrateKbps);
            cmd.Parameters.AddWithValue("@quality_score", variant.QualityScore);
            cmd.Parameters.AddWithValue("@transcode_suspect", variant.TranscodeSuspect);
            cmd.Parameters.AddWithValue("@transcode_reason", (object?)variant.TranscodeReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dynamic_range_dr", (object?)variant.DynamicRangeDR ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@loudness_lufs", (object?)variant.LoudnessLUFS ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@has_clipping", (object?)variant.HasClipping ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@encoder_signature", (object?)variant.EncoderSignature ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@file_sha256", (object?)variant.FileSha256 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recording_id", (object?)variant.MusicBrainzRecordingId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@flac_key", flacKey);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task IncrementHashUseCountAsync(string flacKey, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE HashDb SET use_count = use_count + 1, last_updated_at = @now WHERE flac_key = @flac_key";
            cmd.Parameters.AddWithValue("@flac_key", flacKey);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ========== Mesh Sync ==========

        /// <inheritdoc/>
        public async Task<long> GetLatestSeqIdAsync(CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MAX(seq_id), 0) FROM HashDb";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<HashDbEntry>> GetEntriesSinceSeqAsync(long sinceSeq, int limit = 1000, CancellationToken cancellationToken = default)
        {
            var entries = new List<HashDbEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM HashDb WHERE seq_id > @since_seq ORDER BY seq_id LIMIT @limit";
            cmd.Parameters.AddWithValue("@since_seq", sinceSeq);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadHashEntry(reader));
            }

            return entries;
        }

        /// <inheritdoc/>
        public async Task<int> MergeEntriesFromMeshAsync(IEnumerable<HashDbEntry> entries, CancellationToken cancellationToken = default)
        {
            var merged = 0;
            foreach (var entry in entries)
            {
                // Check if we have this entry
                var existing = await LookupHashAsync(entry.FlacKey, cancellationToken);
                if (existing == null)
                {
                    // New entry - assign our own seq_id
                    await StoreHashAsync(entry, cancellationToken);
                    merged++;
                }
                else if (existing.ByteHash != entry.ByteHash)
                {
                    // Conflict! Keep the one with higher use_count
                    log.Warning("[HashDb] Hash conflict for {Key}: local={Local} vs remote={Remote}", entry.FlacKey, existing.ByteHash?.Substring(0, 16), entry.ByteHash?.Substring(0, 16));
                }
            }

            return merged;
        }

        /// <inheritdoc/>
        public async Task<long> GetPeerLastSeqSeenAsync(string peerId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(last_seq_seen, 0) FROM MeshPeerState WHERE peer_id = @peer_id";
            cmd.Parameters.AddWithValue("@peer_id", peerId);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        /// <inheritdoc/>
        public async Task UpdatePeerLastSeqSeenAsync(string peerId, long seqId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO MeshPeerState (peer_id, last_seq_seen, last_sync_time)
                VALUES (@peer_id, @seq_id, @now)
                ON CONFLICT(peer_id) DO UPDATE SET last_seq_seen = @seq_id, last_sync_time = @now";
            cmd.Parameters.AddWithValue("@peer_id", peerId);
            cmd.Parameters.AddWithValue("@seq_id", seqId);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ========== Backfill ==========

        /// <inheritdoc/>
        public async Task<IEnumerable<FlacInventoryEntry>> GetBackfillCandidatesAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            var entries = new List<FlacInventoryEntry>();
            var today = DateTimeOffset.UtcNow.Date;
            var todayUnix = new DateTimeOffset(today).ToUnixTimeSeconds();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            // Get unhashed files from peers who haven't hit their daily backfill limit
            cmd.CommandText = @"
                SELECT f.* FROM FlacInventory f
                JOIN Peers p ON f.peer_id = p.peer_id
                WHERE f.hash_status = 'none'
                  AND (p.backfill_reset_date IS NULL OR p.backfill_reset_date < @today OR p.backfills_today < 50)
                ORDER BY f.discovered_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@today", todayUnix);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadFlacEntry(reader));
            }

            return entries;
        }

        /// <inheritdoc/>
        public async Task IncrementPeerBackfillCountAsync(string peerId, CancellationToken cancellationToken = default)
        {
            var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date).ToUnixTimeSeconds();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Peers SET 
                    backfills_today = CASE WHEN backfill_reset_date = @today THEN backfills_today + 1 ELSE 1 END,
                    backfill_reset_date = @today
                WHERE peer_id = @peer_id";
            cmd.Parameters.AddWithValue("@peer_id", peerId);
            cmd.Parameters.AddWithValue("@today", today);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> GetPeerBackfillCountTodayAsync(string peerId, CancellationToken cancellationToken = default)
        {
            var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date).ToUnixTimeSeconds();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT CASE WHEN backfill_reset_date = @today THEN backfills_today ELSE 0 END
                FROM Peers WHERE peer_id = @peer_id";
            cmd.Parameters.AddWithValue("@peer_id", peerId);
            cmd.Parameters.AddWithValue("@today", today);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        /// <inheritdoc/>
        public async Task ResetDailyBackfillCountersAsync(CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Peers SET backfills_today = 0";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ========== History Backfill ==========

        /// <inheritdoc/>
        public async Task<int> BackfillFromSearchResponsesAsync(IEnumerable<Search.Response> responses, int maxFiles = int.MaxValue, CancellationToken cancellationToken = default)
        {
            var flacCount = 0;
            var skippedCount = 0;

            foreach (var response in responses)
            {
                if (flacCount >= maxFiles)
                {
                    break;
                }

                if (string.IsNullOrEmpty(response.Username))
                {
                    continue;
                }

                foreach (var file in response.Files)
                {
                    if (cancellationToken.IsCancellationRequested || flacCount >= maxFiles)
                    {
                        break;
                    }

                    // Only interested in FLAC files large enough to hash
                    if (!file.Filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (file.Size < HashChunkSize)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Add to inventory for backfill probing
                    var entry = new FlacInventoryEntry
                    {
                        PeerId = response.Username,
                        Path = file.Filename,
                        Size = file.Size,
                        HashStatusStr = "none",
                    };

                    await UpsertFlacEntryAsync(entry, cancellationToken);
                    flacCount++;
                }

                // Also track the peer
                await GetOrCreatePeerAsync(response.Username, cancellationToken);
            }

            if (flacCount > 0 || skippedCount > 0)
            {
                log.Information("[HashDb] Backfilled {Count} FLAC files from search history ({Skipped} too small)", flacCount, skippedCount);
            }

            return flacCount;
        }

        /// <inheritdoc/>
        public async Task<DateTimeOffset?> GetBackfillProgressAsync(CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM HashDbState WHERE key = 'backfill_progress'";

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            if (long.TryParse(result.ToString(), out var timestamp))
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task SetBackfillProgressAsync(DateTimeOffset oldestProcessed, CancellationToken cancellationToken = default)
        {
            var timestamp = oldestProcessed.ToUnixTimeSeconds();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO HashDbState (key, value) VALUES ('backfill_progress', @value)
                ON CONFLICT(key) DO UPDATE SET value = @value";
            cmd.Parameters.AddWithValue("@value", timestamp.ToString());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ========== Helpers ==========

        private static Peer ReadPeer(SqliteDataReader reader)
        {
            return new Peer
            {
                PeerId = reader.GetString(reader.GetOrdinal("peer_id")),
                Caps = reader.GetInt32(reader.GetOrdinal("caps")),
                ClientVersion = reader.IsDBNull(reader.GetOrdinal("client_version")) ? null : reader.GetString(reader.GetOrdinal("client_version")),
                LastSeen = reader.GetInt64(reader.GetOrdinal("last_seen")),
                LastCapCheck = reader.IsDBNull(reader.GetOrdinal("last_cap_check")) ? null : reader.GetInt64(reader.GetOrdinal("last_cap_check")),
                BackfillsToday = reader.GetInt32(reader.GetOrdinal("backfills_today")),
                BackfillResetDate = reader.IsDBNull(reader.GetOrdinal("backfill_reset_date")) ? null : reader.GetInt64(reader.GetOrdinal("backfill_reset_date")),
            };
        }

        private static FlacInventoryEntry ReadFlacEntry(SqliteDataReader reader)
        {
            var entry = new FlacInventoryEntry
            {
                FileId = reader.GetString(reader.GetOrdinal("file_id")),
                PeerId = reader.GetString(reader.GetOrdinal("peer_id")),
                Path = reader.GetString(reader.GetOrdinal("path")),
                Size = reader.GetInt64(reader.GetOrdinal("size")),
                DiscoveredAt = reader.GetInt64(reader.GetOrdinal("discovered_at")),
                HashStatusStr = reader.IsDBNull(reader.GetOrdinal("hash_status")) ? "none" : reader.GetString(reader.GetOrdinal("hash_status")),
                HashValue = reader.IsDBNull(reader.GetOrdinal("hash_value")) ? null : reader.GetString(reader.GetOrdinal("hash_value")),
                HashSourceStr = reader.IsDBNull(reader.GetOrdinal("hash_source")) ? null : reader.GetString(reader.GetOrdinal("hash_source")),
                FlacAudioMd5 = reader.IsDBNull(reader.GetOrdinal("flac_audio_md5")) ? null : reader.GetString(reader.GetOrdinal("flac_audio_md5")),
                SampleRate = reader.IsDBNull(reader.GetOrdinal("sample_rate")) ? null : reader.GetInt32(reader.GetOrdinal("sample_rate")),
                Channels = reader.IsDBNull(reader.GetOrdinal("channels")) ? null : reader.GetInt32(reader.GetOrdinal("channels")),
                BitDepth = reader.IsDBNull(reader.GetOrdinal("bit_depth")) ? null : reader.GetInt32(reader.GetOrdinal("bit_depth")),
                DurationSamples = reader.IsDBNull(reader.GetOrdinal("duration_samples")) ? null : reader.GetInt64(reader.GetOrdinal("duration_samples")),
            };

            // Read new optional columns (may not exist in older DBs)
            TryReadOptionalString(reader, "full_file_hash", v => entry.FullFileHash = v);
            TryReadOptionalInt(reader, "min_block_size", v => entry.MinBlockSize = v);
            TryReadOptionalInt(reader, "max_block_size", v => entry.MaxBlockSize = v);
            TryReadOptionalString(reader, "encoder_info", v => entry.EncoderInfo = v);
            TryReadOptionalString(reader, "album_hash", v => entry.AlbumHash = v);
            TryReadOptionalInt(reader, "probe_fail_count", v => entry.ProbeFailCount = v ?? 0);
            TryReadOptionalString(reader, "probe_fail_reason", v => entry.ProbeFailReason = v);
            TryReadOptionalLong(reader, "last_probe_at", v => entry.LastProbeAt = v);

            return entry;
        }

        private static void TryReadOptionalString(SqliteDataReader reader, string column, Action<string> setter)
        {
            try
            {
                var ordinal = reader.GetOrdinal(column);
                if (!reader.IsDBNull(ordinal))
                {
                    setter(reader.GetString(ordinal));
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Column doesn't exist in this DB version - ignore
            }
        }

        private static void TryReadOptionalInt(SqliteDataReader reader, string column, Action<int?> setter)
        {
            try
            {
                var ordinal = reader.GetOrdinal(column);
                setter(reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Column doesn't exist in this DB version - ignore
            }
        }

        private static void TryReadOptionalLong(SqliteDataReader reader, string column, Action<long?> setter)
        {
            try
            {
                var ordinal = reader.GetOrdinal(column);
                setter(reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Column doesn't exist in this DB version - ignore
            }
        }

        private static void TryReadOptionalDouble(SqliteDataReader reader, string column, Action<double?> setter)
        {
            try
            {
                var ordinal = reader.GetOrdinal(column);
                setter(reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Column doesn't exist in this DB version - ignore
            }
        }

        private static void TryReadOptionalBool(SqliteDataReader reader, string column, Action<bool?> setter)
        {
            try
            {
                var ordinal = reader.GetOrdinal(column);
                if (reader.IsDBNull(ordinal))
                {
                    setter(null);
                }
                else
                {
                    setter(reader.GetBoolean(ordinal));
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Column doesn't exist in this DB version - ignore
            }
        }

        private static HashDbEntry ReadHashEntry(SqliteDataReader reader)
        {
            var entry = new HashDbEntry
            {
                FlacKey = reader.GetString(reader.GetOrdinal("flac_key")),
                ByteHash = reader.GetString(reader.GetOrdinal("byte_hash")),
                Size = reader.GetInt64(reader.GetOrdinal("size")),
                MetaFlags = reader.IsDBNull(reader.GetOrdinal("meta_flags")) ? null : reader.GetInt32(reader.GetOrdinal("meta_flags")),
                FirstSeenAt = reader.GetInt64(reader.GetOrdinal("first_seen_at")),
                LastUpdatedAt = reader.GetInt64(reader.GetOrdinal("last_updated_at")),
                SeqId = reader.IsDBNull(reader.GetOrdinal("seq_id")) ? 0 : reader.GetInt64(reader.GetOrdinal("seq_id")),
                UseCount = reader.GetInt32(reader.GetOrdinal("use_count")),
            };

            TryReadOptionalString(reader, "variant_id", v => entry.VariantId = v);
            TryReadOptionalString(reader, "codec", v => entry.Codec = v);
            TryReadOptionalString(reader, "container", v => entry.Container = v);
            TryReadOptionalInt(reader, "sample_rate_hz", v => entry.SampleRateHz = v);
            TryReadOptionalInt(reader, "bit_depth", v => entry.BitDepth = v);
            TryReadOptionalInt(reader, "channels", v => entry.Channels = v);
            TryReadOptionalInt(reader, "duration_ms", v => entry.DurationMs = v);
            TryReadOptionalInt(reader, "bitrate_kbps", v => entry.BitrateKbps = v);
            TryReadOptionalDouble(reader, "quality_score", v => entry.QualityScore = v);
            TryReadOptionalBool(reader, "transcode_suspect", v => entry.TranscodeSuspect = v);
            TryReadOptionalString(reader, "transcode_reason", v => entry.TranscodeReason = v);
            TryReadOptionalDouble(reader, "dynamic_range_dr", v => entry.DynamicRangeDr = v);
            TryReadOptionalDouble(reader, "loudness_lufs", v => entry.LoudnessLufs = v);
            TryReadOptionalBool(reader, "has_clipping", v => entry.HasClipping = v);
            TryReadOptionalString(reader, "encoder_signature", v => entry.EncoderSignature = v);
            TryReadOptionalInt(reader, "seen_count", v => entry.SeenCount = v);
            TryReadOptionalString(reader, "file_sha256", v => entry.FileSha256 = v);
            TryReadOptionalString(reader, "musicbrainz_id", v => entry.MusicBrainzId = v);

            return entry;
        }
    }
}


