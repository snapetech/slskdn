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
    using slskd.Audio.Analyzers;
    using slskd.Capabilities;
    using slskd.Events;
    using slskd.HashDb.Models;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.AutoTagging;
    using slskd.Integrations.Chromaprint;
    using slskd.Integrations.MusicBrainz;
    using slskd.Integrations.MusicBrainz.Models;
    using slskd.Jobs;
    using slskdOptions = slskd.Options;
    using slskd.Mesh;
    using TagLib;
    using System.Text.Json;

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
        private readonly AudioSketchService audioSketchService;
        private readonly QualityScorer qualityScorer = new();
        private readonly TranscodeDetector transcodeDetector = new();
        private readonly FlacAnalyzer flacAnalyzer = new();
        private readonly Mp3Analyzer mp3Analyzer = new();
        private readonly OpusAnalyzer opusAnalyzer = new();
        private readonly AacAnalyzer aacAnalyzer = new();
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
            if (optionsMonitor != null)
            {
                audioSketchService = new AudioSketchService(optionsMonitor);
            }
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

            if (string.Equals(variant.Codec, "FLAC", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var flacResult = flacAnalyzer.Analyze(filePath, variant);

                    // Update core fields from STREAMINFO (more authoritative than tags)
                    if (flacResult.SampleRateHz.HasValue)
                    {
                        variant.SampleRateHz = flacResult.SampleRateHz.Value;
                    }

                    if (flacResult.BitDepth.HasValue)
                    {
                        variant.BitDepth = flacResult.BitDepth;
                    }

                    if (flacResult.Channels.HasValue)
                    {
                        variant.Channels = flacResult.Channels.Value;
                    }

                    variant.FlacStreamInfoHash42 = flacResult.FlacStreamInfoHash42;
                    variant.FlacPcmMd5 = flacResult.FlacPcmMd5;
                    variant.FlacMinBlockSize = flacResult.FlacMinBlockSize;
                    variant.FlacMaxBlockSize = flacResult.FlacMaxBlockSize;
                    variant.FlacMinFrameSize = flacResult.FlacMinFrameSize;
                    variant.FlacMaxFrameSize = flacResult.FlacMaxFrameSize;
                    variant.FlacTotalSamples = flacResult.FlacTotalSamples;
                    variant.AnalyzerVersion = flacResult.AnalyzerVersion;

                    variant.QualityScore = flacResult.QualityScore;
                    variant.TranscodeSuspect = flacResult.TranscodeSuspect;
                    variant.TranscodeReason = flacResult.TranscodeReason;
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] FLAC analysis failed for {File}", filePath);
                    variant.QualityScore = qualityScorer.ComputeQualityScore(variant);
                    var (suspect, reason) = transcodeDetector.DetectTranscode(variant);
                    variant.TranscodeSuspect = suspect;
                    variant.TranscodeReason = reason;
                }
            }
            else if (string.Equals(variant.Codec, "MP3", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var mp3Result = mp3Analyzer.Analyze(filePath, variant);

                    variant.Mp3StreamHash = mp3Result.Mp3StreamHash;
                    variant.Mp3Encoder = mp3Result.Mp3Encoder;
                    variant.Mp3EncoderPreset = mp3Result.Mp3EncoderPreset;
                    variant.Mp3FramesAnalyzed = mp3Result.Mp3FramesAnalyzed;
                    variant.EffectiveBandwidthHz = mp3Result.EffectiveBandwidthHz;
                    variant.NominalLowpassHz = mp3Result.NominalLowpassHz;
                    variant.SpectralFlatnessScore = mp3Result.SpectralFlatnessScore;
                    variant.HfEnergyRatio = mp3Result.HfEnergyRatio;
                    variant.AnalyzerVersion = mp3Result.AnalyzerVersion;

                    variant.QualityScore = mp3Result.QualityScore;
                    variant.TranscodeSuspect = mp3Result.TranscodeSuspect;
                    variant.TranscodeReason = mp3Result.TranscodeReason;
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] MP3 analysis failed for {File}", filePath);
                    variant.QualityScore = qualityScorer.ComputeQualityScore(variant);
                    var (suspect, reason) = transcodeDetector.DetectTranscode(variant);
                    variant.TranscodeSuspect = suspect;
                    variant.TranscodeReason = reason;
                }
            }
            else if (string.Equals(variant.Codec, "Opus", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var opusResult = opusAnalyzer.Analyze(filePath, variant);

                    variant.OpusStreamHash = opusResult.OpusStreamHash;
                    variant.OpusNominalBitrateKbps = opusResult.OpusNominalBitrateKbps;
                    variant.OpusApplication = opusResult.OpusApplication;
                    variant.OpusBandwidthMode = opusResult.OpusBandwidthMode;
                    variant.EffectiveBandwidthHz = opusResult.EffectiveBandwidthHz;
                    variant.HfEnergyRatio = opusResult.HfEnergyRatio;
                    variant.AnalyzerVersion = opusResult.AnalyzerVersion;

                    variant.QualityScore = opusResult.QualityScore;
                    variant.TranscodeSuspect = opusResult.TranscodeSuspect;
                    variant.TranscodeReason = opusResult.TranscodeReason;
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] Opus analysis failed for {File}", filePath);
                    variant.QualityScore = qualityScorer.ComputeQualityScore(variant);
                    var (suspect, reason) = transcodeDetector.DetectTranscode(variant);
                    variant.TranscodeSuspect = suspect;
                    variant.TranscodeReason = reason;
                }
            }
            else if (string.Equals(variant.Codec, "AAC", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var aacResult = aacAnalyzer.Analyze(filePath, variant);

                    variant.AacStreamHash = aacResult.AacStreamHash;
                    variant.AacProfile = aacResult.AacProfile;
                    variant.AacSbrPresent = aacResult.AacSbrPresent;
                    variant.AacPsPresent = aacResult.AacPsPresent;
                    variant.AacNominalBitrateKbps = aacResult.AacNominalBitrateKbps;
                    variant.EffectiveBandwidthHz = aacResult.EffectiveBandwidthHz;
                    variant.HfEnergyRatio = aacResult.HfEnergyRatio;
                    variant.AnalyzerVersion = aacResult.AnalyzerVersion;

                    variant.QualityScore = aacResult.QualityScore;
                    variant.TranscodeSuspect = aacResult.TranscodeSuspect;
                    variant.TranscodeReason = aacResult.TranscodeReason;
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] AAC analysis failed for {File}", filePath);
                    variant.QualityScore = qualityScorer.ComputeQualityScore(variant);
                    var (suspect, reason) = transcodeDetector.DetectTranscode(variant);
                    variant.TranscodeSuspect = suspect;
                    variant.TranscodeReason = reason;
                }
            }
            else
            {
                variant.QualityScore = qualityScorer.ComputeQualityScore(variant);
                var (suspect, reason) = transcodeDetector.DetectTranscode(variant);
                variant.TranscodeSuspect = suspect;
                variant.TranscodeReason = reason;
            }

            if (audioSketchService != null && string.IsNullOrWhiteSpace(variant.AudioSketchHash))
            {
                try
                {
                    variant.AudioSketchHash = audioSketchService.ComputeSketchHash(filePath);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[HashDb] Audio sketch hash failed for {File}", filePath);
                }
            }

            return variant;
        }

        private static bool CodecProfileFromCodec(string codec)
        {
            return codec switch
            {
                "FLAC" => true,
                "ALAC" => true,
                "WAV" => true,
                "AIFF" => true,
                _ => false,
            };
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

        // ========== Library Health ==========

        /// <inheritdoc/>
        public async Task UpsertLibraryHealthScanAsync(LibraryHealth.LibraryHealthScan scan, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO LibraryHealthScans (
                    scan_id,
                    library_path,
                    started_at,
                    completed_at,
                    status,
                    files_scanned,
                    issues_detected,
                    error_message)
                VALUES (
                    @scan_id,
                    @library_path,
                    @started_at,
                    @completed_at,
                    @status,
                    @files_scanned,
                    @issues_detected,
                    @error_message)
                ON CONFLICT(scan_id) DO UPDATE SET
                    completed_at = excluded.completed_at,
                    status = excluded.status,
                    files_scanned = excluded.files_scanned,
                    issues_detected = excluded.issues_detected,
                    error_message = excluded.error_message";

            cmd.Parameters.AddWithValue("@scan_id", scan.ScanId);
            cmd.Parameters.AddWithValue("@library_path", scan.LibraryPath);
            cmd.Parameters.AddWithValue("@started_at", scan.StartedAt.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@completed_at", scan.CompletedAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", scan.Status.ToString().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@files_scanned", scan.FilesScanned);
            cmd.Parameters.AddWithValue("@issues_detected", scan.IssuesDetected);
            cmd.Parameters.AddWithValue("@error_message", (object?)scan.ErrorMessage ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<LibraryHealth.LibraryHealthScan?> GetLibraryHealthScanAsync(string scanId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LibraryHealthScans WHERE scan_id = @id";
            cmd.Parameters.AddWithValue("@id", scanId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadScan(reader);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<List<LibraryHealth.LibraryIssue>> GetLibraryIssuesAsync(LibraryHealth.LibraryHealthIssueFilter filter, CancellationToken cancellationToken = default)
        {
            var issues = new List<LibraryHealth.LibraryIssue>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            var sql = @"SELECT * FROM LibraryHealthIssues WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(filter.LibraryPath))
            {
                sql += " AND file_path LIKE @path";
                cmd.Parameters.AddWithValue("@path", $"{filter.LibraryPath}%");
            }

            if (!string.IsNullOrWhiteSpace(filter.MusicBrainzReleaseId))
            {
                sql += " AND mb_release_id = @rid";
                cmd.Parameters.AddWithValue("@rid", filter.MusicBrainzReleaseId);
            }

            if (filter.Types != null && filter.Types.Count > 0)
            {
                sql += $" AND type IN ({string.Join(",", filter.Types.Select((_, i) => $"@t{i}"))})";
                for (int i = 0; i < filter.Types.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@t{i}", filter.Types[i].ToString());
                }
            }

            if (filter.Severities != null && filter.Severities.Count > 0)
            {
                sql += $" AND severity IN ({string.Join(",", filter.Severities.Select((_, i) => $"@s{i}"))})";
                for (int i = 0; i < filter.Severities.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@s{i}", filter.Severities[i].ToString());
                }
            }

            if (filter.Statuses != null && filter.Statuses.Count > 0)
            {
                sql += $" AND status IN ({string.Join(",", filter.Statuses.Select((_, i) => $"@st{i}"))})";
                for (int i = 0; i < filter.Statuses.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@st{i}", filter.Statuses[i].ToString().ToLowerInvariant());
                }
            }

            sql += " ORDER BY detected_at DESC LIMIT @limit OFFSET @offset";
            cmd.Parameters.AddWithValue("@limit", filter.Limit);
            cmd.Parameters.AddWithValue("@offset", filter.Offset);
            cmd.CommandText = sql;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                issues.Add(ReadIssue(reader));
            }

            return issues;
        }

        /// <inheritdoc/>
        public async Task UpdateLibraryIssueStatusAsync(string issueId, LibraryHealth.LibraryIssueStatus status, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE LibraryHealthIssues
                SET status = @status,
                    resolved_at = CASE WHEN @status IN ('resolved','ignored') THEN strftime('%s','now') ELSE resolved_at END
                WHERE issue_id = @id";
            cmd.Parameters.AddWithValue("@status", status.ToString().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@id", issueId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task InsertLibraryIssueAsync(LibraryHealth.LibraryIssue issue, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO LibraryHealthIssues (
                    issue_id,
                    type,
                    severity,
                    file_path,
                    mb_recording_id,
                    mb_release_id,
                    artist,
                    album,
                    title,
                    reason,
                    metadata,
                    can_auto_fix,
                    suggested_action,
                    remediation_job_id,
                    status,
                    detected_at,
                    resolved_at,
                    resolved_by)
                VALUES (
                    @issue_id,
                    @type,
                    @severity,
                    @file_path,
                    @mb_recording_id,
                    @mb_release_id,
                    @artist,
                    @album,
                    @title,
                    @reason,
                    @metadata,
                    @can_auto_fix,
                    @suggested_action,
                    @remediation_job_id,
                    @status,
                    @detected_at,
                    @resolved_at,
                    @resolved_by)";

            cmd.Parameters.AddWithValue("@issue_id", issue.IssueId);
            cmd.Parameters.AddWithValue("@type", issue.Type.ToString());
            cmd.Parameters.AddWithValue("@severity", issue.Severity.ToString());
            cmd.Parameters.AddWithValue("@file_path", (object?)issue.FilePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mb_recording_id", (object?)issue.MusicBrainzRecordingId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mb_release_id", (object?)issue.MusicBrainzReleaseId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@artist", (object?)issue.Artist ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@album", (object?)issue.Album ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title", (object?)issue.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@reason", (object?)issue.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(issue.Metadata ?? new Dictionary<string, object>()));
            cmd.Parameters.AddWithValue("@can_auto_fix", issue.CanAutoFix);
            cmd.Parameters.AddWithValue("@suggested_action", (object?)issue.SuggestedAction ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@remediation_job_id", (object?)issue.RemediationJobId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", issue.Status.ToString().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@detected_at", issue.DetectedAt.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@resolved_at", issue.ResolvedAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@resolved_by", (object?)issue.ResolvedBy ?? DBNull.Value);

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
                    flac_streaminfo_hash42 = @flac_streaminfo_hash42,
                    flac_pcm_md5 = @flac_pcm_md5,
                    flac_min_block_size = @flac_min_block_size,
                    flac_max_block_size = @flac_max_block_size,
                    flac_min_frame_size = @flac_min_frame_size,
                    flac_max_frame_size = @flac_max_frame_size,
                    flac_total_samples = @flac_total_samples,
                    mp3_stream_hash = @mp3_stream_hash,
                    mp3_encoder = @mp3_encoder,
                    mp3_encoder_preset = @mp3_encoder_preset,
                    mp3_frames_analyzed = @mp3_frames_analyzed,
                    effective_bandwidth_hz = @effective_bandwidth_hz,
                    nominal_lowpass_hz = @nominal_lowpass_hz,
                    spectral_flatness_score = @spectral_flatness_score,
                    hf_energy_ratio = @hf_energy_ratio,
                    opus_stream_hash = @opus_stream_hash,
                    opus_nominal_bitrate_kbps = @opus_nominal_bitrate_kbps,
                    opus_application = @opus_application,
                    opus_bandwidth_mode = @opus_bandwidth_mode,
                    aac_stream_hash = @aac_stream_hash,
                    aac_profile = @aac_profile,
                    aac_sbr_present = @aac_sbr_present,
                    aac_ps_present = @aac_ps_present,
                    aac_nominal_bitrate_kbps = @aac_nominal_bitrate_kbps,
                    audio_sketch_hash = @audio_sketch_hash,
                    quality_score = @quality_score,
                    transcode_suspect = @transcode_suspect,
                    transcode_reason = @transcode_reason,
                    dynamic_range_dr = @dynamic_range_dr,
                    loudness_lufs = @loudness_lufs,
                    has_clipping = @has_clipping,
                    encoder_signature = @encoder_signature,
                    analyzer_version = @analyzer_version,
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
            cmd.Parameters.AddWithValue("@flac_streaminfo_hash42", (object?)variant.FlacStreamInfoHash42 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_pcm_md5", (object?)variant.FlacPcmMd5 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_min_block_size", (object?)variant.FlacMinBlockSize ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_max_block_size", (object?)variant.FlacMaxBlockSize ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_min_frame_size", (object?)variant.FlacMinFrameSize ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_max_frame_size", (object?)variant.FlacMaxFrameSize ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@flac_total_samples", (object?)variant.FlacTotalSamples ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mp3_stream_hash", (object?)variant.Mp3StreamHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mp3_encoder", (object?)variant.Mp3Encoder ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mp3_encoder_preset", (object?)variant.Mp3EncoderPreset ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mp3_frames_analyzed", (object?)variant.Mp3FramesAnalyzed ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@effective_bandwidth_hz", (object?)variant.EffectiveBandwidthHz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nominal_lowpass_hz", (object?)variant.NominalLowpassHz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@spectral_flatness_score", (object?)variant.SpectralFlatnessScore ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hf_energy_ratio", (object?)variant.HfEnergyRatio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@opus_stream_hash", (object?)variant.OpusStreamHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@opus_nominal_bitrate_kbps", (object?)variant.OpusNominalBitrateKbps ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@opus_application", (object?)variant.OpusApplication ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@opus_bandwidth_mode", (object?)variant.OpusBandwidthMode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aac_stream_hash", (object?)variant.AacStreamHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aac_profile", (object?)variant.AacProfile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aac_sbr_present", (object?)variant.AacSbrPresent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aac_ps_present", (object?)variant.AacPsPresent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aac_nominal_bitrate_kbps", (object?)variant.AacNominalBitrateKbps ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@audio_sketch_hash", (object?)variant.AudioSketchHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@quality_score", variant.QualityScore);
            cmd.Parameters.AddWithValue("@transcode_suspect", variant.TranscodeSuspect);
            cmd.Parameters.AddWithValue("@transcode_reason", (object?)variant.TranscodeReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dynamic_range_dr", (object?)variant.DynamicRangeDR ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@loudness_lufs", (object?)variant.LoudnessLUFS ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@has_clipping", (object?)variant.HasClipping ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@encoder_signature", (object?)variant.EncoderSignature ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@analyzer_version", (object?)variant.AnalyzerVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@file_sha256", (object?)variant.FileSha256 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recording_id", (object?)variant.MusicBrainzRecordingId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@flac_key", flacKey);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<AudioVariant>> GetVariantsByRecordingAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            var list = new List<AudioVariant>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM HashDb WHERE musicbrainz_id = @recordingId";
            cmd.Parameters.AddWithValue("@recordingId", recordingId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var entry = ReadHashEntry(reader);
                var variant = MapEntryToVariant(entry);
                if (variant != null)
                {
                    list.Add(variant);
                }
            }

            return list;
        }

        /// <inheritdoc/>
        public async Task<List<AudioVariant>> GetVariantsByRecordingAndProfileAsync(string recordingId, string codecProfileKey, CancellationToken cancellationToken = default)
        {
            var list = new List<AudioVariant>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM HashDb WHERE musicbrainz_id = @recordingId";
            cmd.Parameters.AddWithValue("@recordingId", recordingId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var entry = ReadHashEntry(reader);
                var variant = MapEntryToVariant(entry);
                if (variant != null && CodecProfile.FromVariant(variant).ToKey() == codecProfileKey)
                {
                    list.Add(variant);
                }
            }

            return list;
        }

        /// <inheritdoc/>
        public async Task UpsertCanonicalStatsAsync(CanonicalStats stats, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO CanonicalStats (
                    id,
                    musicbrainz_recording_id,
                    codec_profile_key,
                    variant_count,
                    total_seen_count,
                    avg_quality_score,
                    max_quality_score,
                    percent_transcode_suspect,
                    codec_distribution,
                    bitrate_distribution,
                    sample_rate_distribution,
                    best_variant_id,
                    canonicality_score,
                    last_updated)
                VALUES (
                    @id,
                    @recording_id,
                    @codec_profile_key,
                    @variant_count,
                    @total_seen_count,
                    @avg_quality_score,
                    @max_quality_score,
                    @percent_transcode_suspect,
                    @codec_distribution,
                    @bitrate_distribution,
                    @sample_rate_distribution,
                    @best_variant_id,
                    @canonicality_score,
                    @last_updated)
                ON CONFLICT(id) DO UPDATE SET
                    variant_count = excluded.variant_count,
                    total_seen_count = excluded.total_seen_count,
                    avg_quality_score = excluded.avg_quality_score,
                    max_quality_score = excluded.max_quality_score,
                    percent_transcode_suspect = excluded.percent_transcode_suspect,
                    codec_distribution = excluded.codec_distribution,
                    bitrate_distribution = excluded.bitrate_distribution,
                    sample_rate_distribution = excluded.sample_rate_distribution,
                    best_variant_id = excluded.best_variant_id,
                    canonicality_score = excluded.canonicality_score,
                    last_updated = excluded.last_updated";

            cmd.Parameters.AddWithValue("@id", stats.Id);
            cmd.Parameters.AddWithValue("@recording_id", stats.MusicBrainzRecordingId);
            cmd.Parameters.AddWithValue("@codec_profile_key", stats.CodecProfileKey);
            cmd.Parameters.AddWithValue("@variant_count", stats.VariantCount);
            cmd.Parameters.AddWithValue("@total_seen_count", stats.TotalSeenCount);
            cmd.Parameters.AddWithValue("@avg_quality_score", stats.AvgQualityScore);
            cmd.Parameters.AddWithValue("@max_quality_score", stats.MaxQualityScore);
            cmd.Parameters.AddWithValue("@percent_transcode_suspect", stats.PercentTranscodeSuspect);
            cmd.Parameters.AddWithValue("@codec_distribution", JsonSerializer.Serialize(stats.CodecDistribution));
            cmd.Parameters.AddWithValue("@bitrate_distribution", JsonSerializer.Serialize(stats.BitrateDistribution));
            cmd.Parameters.AddWithValue("@sample_rate_distribution", JsonSerializer.Serialize(stats.SampleRateDistribution));
            cmd.Parameters.AddWithValue("@best_variant_id", (object?)stats.BestVariantId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@canonicality_score", stats.CanonicalityScore);
            cmd.Parameters.AddWithValue("@last_updated", stats.LastUpdated.ToUnixTimeSeconds());

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CanonicalStats?> GetCanonicalStatsAsync(string recordingId, string codecProfileKey, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM CanonicalStats WHERE musicbrainz_recording_id = @rec AND codec_profile_key = @key";
            cmd.Parameters.AddWithValue("@rec", recordingId);
            cmd.Parameters.AddWithValue("@key", codecProfileKey);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadCanonicalStats(reader);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetRecordingIdsWithVariantsAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<string>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT musicbrainz_id FROM HashDb WHERE musicbrainz_id IS NOT NULL";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(reader.GetString(0));
            }

            return list;
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetCodecProfilesForRecordingAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            var list = new List<string>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT codec, sample_rate_hz, bit_depth, channels FROM HashDb WHERE musicbrainz_id = @rec";
            cmd.Parameters.AddWithValue("@rec", recordingId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var codec = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                var sampleRate = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                var bitDepth = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                var channels = reader.IsDBNull(3) ? 2 : reader.GetInt32(3);

                var profile = new CodecProfile
                {
                    Codec = codec,
                    IsLossless = CodecProfileFromCodec(codec),
                    SampleRateHz = sampleRate,
                    BitDepth = bitDepth,
                    Channels = channels,
                }.ToKey();

                if (!list.Contains(profile))
                {
                    list.Add(profile);
                }
            }

            return list;
        }

        /// <inheritdoc/>
        public async Task<Jobs.DiscographyJob?> GetDiscographyJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT json_data FROM DiscographyJobs WHERE job_id = @job_id";
            cmd.Parameters.AddWithValue("@job_id", jobId);

            var json = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Jobs.DiscographyJob>(json);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Failed to deserialize discography job {JobId}", jobId);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task UpsertDiscographyJobAsync(Jobs.DiscographyJob job, CancellationToken cancellationToken = default)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.JobId))
            {
                return;
            }

            var json = JsonSerializer.Serialize(job);

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO DiscographyJobs (job_id, artist_id, artist_name, profile, target_directory, total_releases, completed_releases, failed_releases, status, created_at, json_data)
                VALUES (@job_id, @artist_id, @artist_name, @profile, @target_directory, @total_releases, @completed_releases, @failed_releases, @status, @created_at, @json_data)
                ON CONFLICT(job_id) DO UPDATE SET
                    artist_id = excluded.artist_id,
                    artist_name = excluded.artist_name,
                    profile = excluded.profile,
                    target_directory = excluded.target_directory,
                    total_releases = excluded.total_releases,
                    completed_releases = excluded.completed_releases,
                    failed_releases = excluded.failed_releases,
                    status = excluded.status,
                    json_data = excluded.json_data";

            cmd.Parameters.AddWithValue("@job_id", job.JobId);
            cmd.Parameters.AddWithValue("@artist_id", job.ArtistId ?? string.Empty);
            cmd.Parameters.AddWithValue("@artist_name", job.ArtistName ?? string.Empty);
            cmd.Parameters.AddWithValue("@profile", job.Profile.ToString());
            cmd.Parameters.AddWithValue("@target_directory", job.TargetDirectory ?? string.Empty);
            cmd.Parameters.AddWithValue("@total_releases", job.TotalReleases);
            cmd.Parameters.AddWithValue("@completed_releases", job.CompletedReleases);
            cmd.Parameters.AddWithValue("@failed_releases", job.FailedReleases);
            cmd.Parameters.AddWithValue("@status", job.Status.ToString());
            cmd.Parameters.AddWithValue("@created_at", job.CreatedAt.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@json_data", json);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DiscographyReleaseJobStatus>> GetDiscographyReleaseJobsAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var list = new List<DiscographyReleaseJobStatus>();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT release_id, status
                FROM DiscographyReleaseJobs
                WHERE discography_job_id = @job_id";
            cmd.Parameters.AddWithValue("@job_id", jobId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var releaseId = reader.GetString(0);
                var statusText = reader.GetString(1);
                var status = Enum.TryParse<JobStatus>(statusText, ignoreCase: true, out var s) ? s : JobStatus.Pending;
                list.Add(new DiscographyReleaseJobStatus
                {
                    ReleaseId = releaseId,
                    Status = status,
                });
            }

            return list;
        }

        /// <inheritdoc/>
        public async Task UpsertDiscographyReleaseJobsAsync(string jobId, IEnumerable<DiscographyReleaseJobStatus> releases, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId) || releases == null)
            {
                return;
            }

            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();

            foreach (var release in releases)
            {
                if (release == null || string.IsNullOrWhiteSpace(release.ReleaseId))
                {
                    continue;
                }

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO DiscographyReleaseJobs (discography_job_id, release_id, status)
                    VALUES (@job_id, @release_id, @status)
                    ON CONFLICT(discography_job_id, release_id) DO UPDATE SET
                        status = excluded.status";

                cmd.Parameters.AddWithValue("@job_id", jobId);
                cmd.Parameters.AddWithValue("@release_id", release.ReleaseId);
                cmd.Parameters.AddWithValue("@status", release.Status.ToString().ToLowerInvariant());

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            tx.Commit();
        }

        /// <inheritdoc/>
        public Task SetDiscographyReleaseJobStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken cancellationToken = default)
        {
            return UpsertDiscographyReleaseJobsAsync(jobId, new[]
            {
                new DiscographyReleaseJobStatus
                {
                    ReleaseId = releaseId,
                    Status = status,
                },
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<LabelPresence>> GetLabelPresenceAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<LabelPresence>();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT metadata_label, COUNT(*) AS cnt
                FROM AlbumTargets
                WHERE metadata_label IS NOT NULL AND metadata_label <> ''
                GROUP BY metadata_label
                ORDER BY cnt DESC, metadata_label";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new LabelPresence
                {
                    Label = reader.GetString(0),
                    ReleaseCount = reader.GetInt32(1),
                });
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetReleaseIdsByLabelAsync(string labelNameOrId, int limit, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(labelNameOrId) || limit <= 0)
            {
                return results;
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT release_id
                FROM AlbumTargets
                WHERE (metadata_label = @label OR discogs_release_id = @label)
                ORDER BY created_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@label", labelNameOrId);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }

        // ========== Label Crate Jobs ==========

        /// <inheritdoc/>
        public async Task<LabelCrateJob?> GetLabelCrateJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT json_data FROM LabelCrateJobs WHERE job_id = @job_id";
            cmd.Parameters.AddWithValue("@job_id", jobId);

            var json = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<LabelCrateJob>(json);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Failed to deserialize label crate job {JobId}", jobId);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task UpsertLabelCrateJobAsync(LabelCrateJob job, CancellationToken cancellationToken = default)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.JobId))
            {
                return;
            }

            var json = JsonSerializer.Serialize(job);

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO LabelCrateJobs (job_id, label_id, label_name, limit_count, total_releases, completed_releases, failed_releases, status, created_at, json_data)
                VALUES (@job_id, @label_id, @label_name, @limit_count, @total_releases, @completed_releases, @failed_releases, @status, @created_at, @json_data)
                ON CONFLICT(job_id) DO UPDATE SET
                    label_id = excluded.label_id,
                    label_name = excluded.label_name,
                    limit_count = excluded.limit_count,
                    total_releases = excluded.total_releases,
                    completed_releases = excluded.completed_releases,
                    failed_releases = excluded.failed_releases,
                    status = excluded.status,
                    json_data = excluded.json_data";

            cmd.Parameters.AddWithValue("@job_id", job.JobId);
            cmd.Parameters.AddWithValue("@label_id", (object?)job.LabelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@label_name", job.LabelName ?? string.Empty);
            cmd.Parameters.AddWithValue("@limit_count", job.Limit);
            cmd.Parameters.AddWithValue("@total_releases", job.TotalReleases);
            cmd.Parameters.AddWithValue("@completed_releases", job.CompletedReleases);
            cmd.Parameters.AddWithValue("@failed_releases", job.FailedReleases);
            cmd.Parameters.AddWithValue("@status", job.Status.ToString());
            cmd.Parameters.AddWithValue("@created_at", job.CreatedAt.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@json_data", json);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<DiscographyReleaseJobStatus>> GetLabelCrateReleaseJobsAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var list = new List<DiscographyReleaseJobStatus>();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT release_id, status
                FROM LabelCrateReleaseJobs
                WHERE label_crate_job_id = @job_id";
            cmd.Parameters.AddWithValue("@job_id", jobId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var releaseId = reader.GetString(0);
                var statusText = reader.GetString(1);
                var status = Enum.TryParse<JobStatus>(statusText, ignoreCase: true, out var s) ? s : JobStatus.Pending;
                list.Add(new DiscographyReleaseJobStatus
                {
                    ReleaseId = releaseId,
                    Status = status,
                });
            }

            return list;
        }

        /// <inheritdoc/>
        public async Task UpsertLabelCrateReleaseJobsAsync(string jobId, IEnumerable<DiscographyReleaseJobStatus> releases, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId) || releases == null)
            {
                return;
            }

            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();

            foreach (var release in releases)
            {
                if (release == null || string.IsNullOrWhiteSpace(release.ReleaseId))
                {
                    continue;
                }

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO LabelCrateReleaseJobs (label_crate_job_id, release_id, status)
                    VALUES (@job_id, @release_id, @status)
                    ON CONFLICT(label_crate_job_id, release_id) DO UPDATE SET
                        status = excluded.status";

                cmd.Parameters.AddWithValue("@job_id", jobId);
                cmd.Parameters.AddWithValue("@release_id", release.ReleaseId);
                cmd.Parameters.AddWithValue("@status", release.Status.ToString().ToLowerInvariant());

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            tx.Commit();
        }

        /// <inheritdoc/>
        public Task SetLabelCrateReleaseJobStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken cancellationToken = default)
        {
            return UpsertLabelCrateReleaseJobsAsync(jobId, new[]
            {
                new DiscographyReleaseJobStatus
                {
                    ReleaseId = releaseId,
                    Status = status,
                },
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<ArtistReleaseGraph?> GetArtistReleaseGraphAsync(string artistId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT json_data
                FROM ArtistReleaseGraphs
                WHERE artist_id = @artist_id";
            cmd.Parameters.AddWithValue("@artist_id", artistId);

            var json = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ArtistReleaseGraph>(json);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[HashDb] Failed to deserialize release graph for artist {ArtistId}", artistId);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task UpsertArtistReleaseGraphAsync(ArtistReleaseGraph graph, CancellationToken cancellationToken = default)
        {
            if (graph == null || string.IsNullOrWhiteSpace(graph.ArtistId))
            {
                return;
            }

            var json = JsonSerializer.Serialize(graph);

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ArtistReleaseGraphs (artist_id, name, cached_at, expires_at, json_data)
                VALUES (@artist_id, @name, @cached_at, @expires_at, @json_data)
                ON CONFLICT(artist_id) DO UPDATE SET
                    name = excluded.name,
                    cached_at = excluded.cached_at,
                    expires_at = excluded.expires_at,
                    json_data = excluded.json_data";

            cmd.Parameters.AddWithValue("@artist_id", graph.ArtistId);
            cmd.Parameters.AddWithValue("@name", graph.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@cached_at", graph.CachedAt.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@expires_at", graph.ExpiresAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@json_data", json);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            TryReadOptionalString(reader, "audio_fingerprint", v => entry.AudioFingerprint = v);
            TryReadOptionalString(reader, "audio_sketch_hash", v => entry.AudioSketchHash = v);
            TryReadOptionalString(reader, "analyzer_version", v => entry.AnalyzerVersion = v);

            // FLAC-specific
            TryReadOptionalString(reader, "flac_streaminfo_hash42", v => entry.FlacStreamInfoHash42 = v);
            TryReadOptionalString(reader, "flac_pcm_md5", v => entry.FlacPcmMd5 = v);
            TryReadOptionalInt(reader, "flac_min_block_size", v => entry.FlacMinBlockSize = v);
            TryReadOptionalInt(reader, "flac_max_block_size", v => entry.FlacMaxBlockSize = v);
            TryReadOptionalInt(reader, "flac_min_frame_size", v => entry.FlacMinFrameSize = v);
            TryReadOptionalInt(reader, "flac_max_frame_size", v => entry.FlacMaxFrameSize = v);
            TryReadOptionalLong(reader, "flac_total_samples", v => entry.FlacTotalSamples = v);

            // MP3-specific
            TryReadOptionalString(reader, "mp3_stream_hash", v => entry.Mp3StreamHash = v);
            TryReadOptionalString(reader, "mp3_encoder", v => entry.Mp3Encoder = v);
            TryReadOptionalString(reader, "mp3_encoder_preset", v => entry.Mp3EncoderPreset = v);
            TryReadOptionalInt(reader, "mp3_frames_analyzed", v => entry.Mp3FramesAnalyzed = v);

            // Shared lossy spectral metrics
            TryReadOptionalDouble(reader, "effective_bandwidth_hz", v => entry.EffectiveBandwidthHz = v);
            TryReadOptionalDouble(reader, "nominal_lowpass_hz", v => entry.NominalLowpassHz = v);
            TryReadOptionalDouble(reader, "spectral_flatness_score", v => entry.SpectralFlatnessScore = v);
            TryReadOptionalDouble(reader, "hf_energy_ratio", v => entry.HfEnergyRatio = v);

            // Opus-specific
            TryReadOptionalString(reader, "opus_stream_hash", v => entry.OpusStreamHash = v);
            TryReadOptionalInt(reader, "opus_nominal_bitrate_kbps", v => entry.OpusNominalBitrateKbps = v);
            TryReadOptionalString(reader, "opus_application", v => entry.OpusApplication = v);
            TryReadOptionalString(reader, "opus_bandwidth_mode", v => entry.OpusBandwidthMode = v);

            // AAC-specific
            TryReadOptionalString(reader, "aac_stream_hash", v => entry.AacStreamHash = v);
            TryReadOptionalString(reader, "aac_profile", v => entry.AacProfile = v);
            TryReadOptionalBool(reader, "aac_sbr_present", v => entry.AacSbrPresent = v);
            TryReadOptionalBool(reader, "aac_ps_present", v => entry.AacPsPresent = v);
            TryReadOptionalInt(reader, "aac_nominal_bitrate_kbps", v => entry.AacNominalBitrateKbps = v);

            return entry;
        }

        private static AudioVariant MapEntryToVariant(HashDbEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return new AudioVariant
            {
                VariantId = entry.VariantId ?? entry.FlacKey,
                MusicBrainzRecordingId = entry.MusicBrainzId,
                FlacKey = entry.FlacKey,
                Codec = entry.Codec,
                Container = entry.Container,
                SampleRateHz = entry.SampleRateHz ?? 0,
                BitDepth = entry.BitDepth,
                Channels = entry.Channels ?? 2,
                DurationMs = entry.DurationMs ?? 0,
                BitrateKbps = entry.BitrateKbps ?? 0,
                FileSizeBytes = entry.Size,
                FileSha256 = entry.FileSha256,
                AudioFingerprint = entry.AudioFingerprint,
                AudioSketchHash = entry.AudioSketchHash,
                AnalyzerVersion = entry.AnalyzerVersion,
                QualityScore = entry.QualityScore ?? 0.0,
                TranscodeSuspect = entry.TranscodeSuspect ?? false,
                TranscodeReason = entry.TranscodeReason,
                DynamicRangeDR = entry.DynamicRangeDr,
                LoudnessLUFS = entry.LoudnessLufs,
                HasClipping = entry.HasClipping,
                EncoderSignature = entry.EncoderSignature,
                FirstSeenAt = DateTimeOffset.FromUnixTimeSeconds(entry.FirstSeenAt),
                LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(entry.LastUpdatedAt),
                SeenCount = entry.SeenCount ?? 1,
                FlacStreamInfoHash42 = entry.FlacStreamInfoHash42,
                FlacPcmMd5 = entry.FlacPcmMd5,
                FlacMinBlockSize = entry.FlacMinBlockSize,
                FlacMaxBlockSize = entry.FlacMaxBlockSize,
                FlacMinFrameSize = entry.FlacMinFrameSize,
                FlacMaxFrameSize = entry.FlacMaxFrameSize,
                FlacTotalSamples = entry.FlacTotalSamples,
                Mp3StreamHash = entry.Mp3StreamHash,
                Mp3Encoder = entry.Mp3Encoder,
                Mp3EncoderPreset = entry.Mp3EncoderPreset,
                Mp3FramesAnalyzed = entry.Mp3FramesAnalyzed,
                EffectiveBandwidthHz = entry.EffectiveBandwidthHz,
                NominalLowpassHz = entry.NominalLowpassHz,
                SpectralFlatnessScore = entry.SpectralFlatnessScore,
                HfEnergyRatio = entry.HfEnergyRatio,
                OpusStreamHash = entry.OpusStreamHash,
                OpusNominalBitrateKbps = entry.OpusNominalBitrateKbps,
                OpusApplication = entry.OpusApplication,
                OpusBandwidthMode = entry.OpusBandwidthMode,
                AacStreamHash = entry.AacStreamHash,
                AacProfile = entry.AacProfile,
                AacSbrPresent = entry.AacSbrPresent,
                AacPsPresent = entry.AacPsPresent,
                AacNominalBitrateKbps = entry.AacNominalBitrateKbps,
            };
        }

        private static CanonicalStats ReadCanonicalStats(SqliteDataReader reader)
        {
            var stats = new CanonicalStats
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                MusicBrainzRecordingId = reader.GetString(reader.GetOrdinal("musicbrainz_recording_id")),
                CodecProfileKey = reader.GetString(reader.GetOrdinal("codec_profile_key")),
                VariantCount = reader.GetInt32(reader.GetOrdinal("variant_count")),
                TotalSeenCount = reader.GetInt32(reader.GetOrdinal("total_seen_count")),
                AvgQualityScore = reader.GetDouble(reader.GetOrdinal("avg_quality_score")),
                MaxQualityScore = reader.GetDouble(reader.GetOrdinal("max_quality_score")),
                PercentTranscodeSuspect = reader.GetDouble(reader.GetOrdinal("percent_transcode_suspect")),
                BestVariantId = reader.IsDBNull(reader.GetOrdinal("best_variant_id")) ? null : reader.GetString(reader.GetOrdinal("best_variant_id")),
                CanonicalityScore = reader.GetDouble(reader.GetOrdinal("canonicality_score")),
                LastUpdated = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("last_updated"))),
            };

            var codecJson = reader.IsDBNull(reader.GetOrdinal("codec_distribution")) ? "{}" : reader.GetString(reader.GetOrdinal("codec_distribution"));
            var bitrateJson = reader.IsDBNull(reader.GetOrdinal("bitrate_distribution")) ? "{}" : reader.GetString(reader.GetOrdinal("bitrate_distribution"));
            var srJson = reader.IsDBNull(reader.GetOrdinal("sample_rate_distribution")) ? "{}" : reader.GetString(reader.GetOrdinal("sample_rate_distribution"));

            stats.CodecDistribution = JsonSerializer.Deserialize<Dictionary<string, int>>(codecJson) ?? new Dictionary<string, int>();
            stats.BitrateDistribution = JsonSerializer.Deserialize<Dictionary<int, int>>(bitrateJson) ?? new Dictionary<int, int>();
            stats.SampleRateDistribution = JsonSerializer.Deserialize<Dictionary<int, int>>(srJson) ?? new Dictionary<int, int>();

            return stats;
        }

        private static LibraryHealth.LibraryHealthScan ReadScan(SqliteDataReader reader)
        {
            return new LibraryHealth.LibraryHealthScan
            {
                ScanId = reader.GetString(reader.GetOrdinal("scan_id")),
                LibraryPath = reader.GetString(reader.GetOrdinal("library_path")),
                StartedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("started_at"))),
                CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("completed_at"))),
                Status = Enum.TryParse<LibraryHealth.ScanStatus>(reader.GetString(reader.GetOrdinal("status")), true, out var st) ? st : LibraryHealth.ScanStatus.Running,
                FilesScanned = reader.IsDBNull(reader.GetOrdinal("files_scanned")) ? 0 : reader.GetInt32(reader.GetOrdinal("files_scanned")),
                IssuesDetected = reader.IsDBNull(reader.GetOrdinal("issues_detected")) ? 0 : reader.GetInt32(reader.GetOrdinal("issues_detected")),
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString(reader.GetOrdinal("error_message")),
            };
        }

        private static LibraryHealth.LibraryIssue ReadIssue(SqliteDataReader reader)
        {
            var issue = new LibraryHealth.LibraryIssue
            {
                IssueId = reader.GetString(reader.GetOrdinal("issue_id")),
                Type = Enum.TryParse<LibraryHealth.LibraryIssueType>(reader.GetString(reader.GetOrdinal("type")), true, out var t) ? t : LibraryHealth.LibraryIssueType.MissingMetadata,
                Severity = Enum.TryParse<LibraryHealth.LibraryIssueSeverity>(reader.GetString(reader.GetOrdinal("severity")), true, out var sev) ? sev : LibraryHealth.LibraryIssueSeverity.Low,
                FilePath = reader.IsDBNull(reader.GetOrdinal("file_path")) ? null : reader.GetString(reader.GetOrdinal("file_path")),
                MusicBrainzRecordingId = reader.IsDBNull(reader.GetOrdinal("mb_recording_id")) ? null : reader.GetString(reader.GetOrdinal("mb_recording_id")),
                MusicBrainzReleaseId = reader.IsDBNull(reader.GetOrdinal("mb_release_id")) ? null : reader.GetString(reader.GetOrdinal("mb_release_id")),
                Artist = reader.IsDBNull(reader.GetOrdinal("artist")) ? null : reader.GetString(reader.GetOrdinal("artist")),
                Album = reader.IsDBNull(reader.GetOrdinal("album")) ? null : reader.GetString(reader.GetOrdinal("album")),
                Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
                Reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? null : reader.GetString(reader.GetOrdinal("reason")),
                CanAutoFix = !reader.IsDBNull(reader.GetOrdinal("can_auto_fix")) && reader.GetBoolean(reader.GetOrdinal("can_auto_fix")),
                SuggestedAction = reader.IsDBNull(reader.GetOrdinal("suggested_action")) ? null : reader.GetString(reader.GetOrdinal("suggested_action")),
                RemediationJobId = reader.IsDBNull(reader.GetOrdinal("remediation_job_id")) ? null : reader.GetString(reader.GetOrdinal("remediation_job_id")),
                Status = Enum.TryParse<LibraryHealth.LibraryIssueStatus>(reader.GetString(reader.GetOrdinal("status")), true, out var st) ? st : LibraryHealth.LibraryIssueStatus.Detected,
                DetectedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("detected_at"))),
                ResolvedAt = reader.IsDBNull(reader.GetOrdinal("resolved_at")) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("resolved_at"))),
                ResolvedBy = reader.IsDBNull(reader.GetOrdinal("resolved_by")) ? null : reader.GetString(reader.GetOrdinal("resolved_by")),
            };

            var metaJson = reader.IsDBNull(reader.GetOrdinal("metadata")) ? "{}" : reader.GetString(reader.GetOrdinal("metadata"));
            issue.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metaJson) ?? new Dictionary<string, object>();
            return issue;
        }

        // Peer metrics methods (placeholder implementations for T-406)
        public Task<Transfers.MultiSource.Metrics.PeerPerformanceMetrics> GetPeerMetricsAsync(string peerId, CancellationToken cancellationToken = default)
        {
            // TODO (T-406): Implement database query for peer metrics
            return Task.FromResult<Transfers.MultiSource.Metrics.PeerPerformanceMetrics>(null);
        }

        public Task UpsertPeerMetricsAsync(Transfers.MultiSource.Metrics.PeerPerformanceMetrics metrics, CancellationToken cancellationToken = default)
        {
            // TODO (T-406): Implement database upsert for peer metrics
            return Task.CompletedTask;
        }

        public Task<List<Transfers.MultiSource.Metrics.PeerPerformanceMetrics>> GetAllPeerMetricsAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<Transfers.MultiSource.Metrics.PeerPerformanceMetrics>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PeerMetrics";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadPeerMetrics(reader));
            }

            return Task.FromResult(list);
        }

        /// <inheritdoc/>
        public Task<Transfers.MultiSource.Metrics.PeerPerformanceMetrics> GetPeerMetricsAsync(string peerId, CancellationToken cancellationToken = default)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PeerMetrics WHERE peer_id = @peer_id";
            cmd.Parameters.AddWithValue("@peer_id", peerId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Task.FromResult(ReadPeerMetrics(reader));
            }

            return Task.FromResult<Transfers.MultiSource.Metrics.PeerPerformanceMetrics>(null);
        }

        /// <inheritdoc/>
        public Task UpsertPeerMetricsAsync(Transfers.MultiSource.Metrics.PeerPerformanceMetrics metrics, CancellationToken cancellationToken = default)
        {
            if (metrics == null || string.IsNullOrWhiteSpace(metrics.PeerId))
            {
                return Task.CompletedTask;
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO PeerMetrics (
                    peer_id,
                    source,
                    rtt_avg_ms,
                    rtt_stddev_ms,
                    last_rtt_sample,
                    throughput_avg_bps,
                    throughput_stddev_bps,
                    total_bytes,
                    last_throughput_sample,
                    chunks_requested,
                    chunks_completed,
                    chunks_failed,
                    chunks_timedout,
                    chunks_corrupted,
                    sample_count,
                    first_seen,
                    last_updated,
                    reputation_score,
                    reputation_updated_at
                ) VALUES (
                    @peer_id,
                    @source,
                    @rtt_avg_ms,
                    @rtt_stddev_ms,
                    @last_rtt_sample,
                    @throughput_avg_bps,
                    @throughput_stddev_bps,
                    @total_bytes,
                    @last_throughput_sample,
                    @chunks_requested,
                    @chunks_completed,
                    @chunks_failed,
                    @chunks_timedout,
                    @chunks_corrupted,
                    @sample_count,
                    @first_seen,
                    @last_updated,
                    @reputation_score,
                    @reputation_updated_at
                )
                ON CONFLICT(peer_id) DO UPDATE SET
                    source = excluded.source,
                    rtt_avg_ms = excluded.rtt_avg_ms,
                    rtt_stddev_ms = excluded.rtt_stddev_ms,
                    last_rtt_sample = excluded.last_rtt_sample,
                    throughput_avg_bps = excluded.throughput_avg_bps,
                    throughput_stddev_bps = excluded.throughput_stddev_bps,
                    total_bytes = excluded.total_bytes,
                    last_throughput_sample = excluded.last_throughput_sample,
                    chunks_requested = excluded.chunks_requested,
                    chunks_completed = excluded.chunks_completed,
                    chunks_failed = excluded.chunks_failed,
                    chunks_timedout = excluded.chunks_timedout,
                    chunks_corrupted = excluded.chunks_corrupted,
                    sample_count = excluded.sample_count,
                    first_seen = excluded.first_seen,
                    last_updated = excluded.last_updated,
                    reputation_score = excluded.reputation_score,
                    reputation_updated_at = excluded.reputation_updated_at;
            ";

            cmd.Parameters.AddWithValue("@peer_id", metrics.PeerId);
            cmd.Parameters.AddWithValue("@source", metrics.Source.ToString());
            cmd.Parameters.AddWithValue("@rtt_avg_ms", metrics.RttAvgMs);
            cmd.Parameters.AddWithValue("@rtt_stddev_ms", metrics.RttStdDevMs);
            cmd.Parameters.AddWithValue("@last_rtt_sample", (object?)metrics.LastRttSample?.ToUnixTimeSeconds() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@throughput_avg_bps", metrics.ThroughputAvgBytesPerSec);
            cmd.Parameters.AddWithValue("@throughput_stddev_bps", metrics.ThroughputStdDevBytesPerSec);
            cmd.Parameters.AddWithValue("@total_bytes", metrics.TotalBytesTransferred);
            cmd.Parameters.AddWithValue("@last_throughput_sample", (object?)metrics.LastThroughputSample?.ToUnixTimeSeconds() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@chunks_requested", metrics.ChunksRequested);
            cmd.Parameters.AddWithValue("@chunks_completed", metrics.ChunksCompleted);
            cmd.Parameters.AddWithValue("@chunks_failed", metrics.ChunksFailed);
            cmd.Parameters.AddWithValue("@chunks_timedout", metrics.ChunksTimedOut);
            cmd.Parameters.AddWithValue("@chunks_corrupted", metrics.ChunksCorrupted);
            cmd.Parameters.AddWithValue("@sample_count", metrics.SampleCount);
            cmd.Parameters.AddWithValue("@first_seen", metrics.FirstSeen.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@last_updated", metrics.LastUpdated.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@reputation_score", metrics.ReputationScore);
            cmd.Parameters.AddWithValue("@reputation_updated_at", (object?)metrics.ReputationUpdatedAt?.ToUnixTimeSeconds() ?? DBNull.Value);

            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }

        private Transfers.MultiSource.Metrics.PeerPerformanceMetrics ReadPeerMetrics(SqliteDataReader reader)
        {
            var metrics = new Transfers.MultiSource.Metrics.PeerPerformanceMetrics
            {
                PeerId = reader.GetString(reader.GetOrdinal("peer_id")),
                Source = Enum.TryParse<Transfers.MultiSource.Metrics.PeerSource>(reader.GetString(reader.GetOrdinal("source")), true, out var src) ? src : Transfers.MultiSource.Metrics.PeerSource.Soulseek,
                RttAvgMs = reader.IsDBNull(reader.GetOrdinal("rtt_avg_ms")) ? 0 : reader.GetDouble(reader.GetOrdinal("rtt_avg_ms")),
                RttStdDevMs = reader.IsDBNull(reader.GetOrdinal("rtt_stddev_ms")) ? 0 : reader.GetDouble(reader.GetOrdinal("rtt_stddev_ms")),
                LastRttSample = reader.IsDBNull(reader.GetOrdinal("last_rtt_sample")) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("last_rtt_sample"))),
                ThroughputAvgBytesPerSec = reader.IsDBNull(reader.GetOrdinal("throughput_avg_bps")) ? 0 : reader.GetDouble(reader.GetOrdinal("throughput_avg_bps")),
                ThroughputStdDevBytesPerSec = reader.IsDBNull(reader.GetOrdinal("throughput_stddev_bps")) ? 0 : reader.GetDouble(reader.GetOrdinal("throughput_stddev_bps")),
                TotalBytesTransferred = reader.IsDBNull(reader.GetOrdinal("total_bytes")) ? 0 : reader.GetInt64(reader.GetOrdinal("total_bytes")),
                LastThroughputSample = reader.IsDBNull(reader.GetOrdinal("last_throughput_sample")) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("last_throughput_sample"))),
                ChunksRequested = reader.IsDBNull(reader.GetOrdinal("chunks_requested")) ? 0 : reader.GetInt32(reader.GetOrdinal("chunks_requested")),
                ChunksCompleted = reader.IsDBNull(reader.GetOrdinal("chunks_completed")) ? 0 : reader.GetInt32(reader.GetOrdinal("chunks_completed")),
                ChunksFailed = reader.IsDBNull(reader.GetOrdinal("chunks_failed")) ? 0 : reader.GetInt32(reader.GetOrdinal("chunks_failed")),
                ChunksTimedOut = reader.IsDBNull(reader.GetOrdinal("chunks_timedout")) ? 0 : reader.GetInt32(reader.GetOrdinal("chunks_timedout")),
                ChunksCorrupted = reader.IsDBNull(reader.GetOrdinal("chunks_corrupted")) ? 0 : reader.GetInt32(reader.GetOrdinal("chunks_corrupted")),
                SampleCount = reader.IsDBNull(reader.GetOrdinal("sample_count")) ? 0 : reader.GetInt32(reader.GetOrdinal("sample_count")),
                FirstSeen = reader.IsDBNull(reader.GetOrdinal("first_seen")) ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("first_seen"))),
                LastUpdated = reader.IsDBNull(reader.GetOrdinal("last_updated")) ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("last_updated"))),
                ReputationScore = reader.IsDBNull(reader.GetOrdinal("reputation_score")) ? 0.5 : reader.GetDouble(reader.GetOrdinal("reputation_score")),
                ReputationUpdatedAt = reader.IsDBNull(reader.GetOrdinal("reputation_updated_at")) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("reputation_updated_at"))),
                RecentRttSamples = new Queue<Transfers.MultiSource.Metrics.RttSample>(),
                RecentThroughputSamples = new Queue<Transfers.MultiSource.Metrics.ThroughputSample>(),
            };

            return metrics;
        }
    }
}


