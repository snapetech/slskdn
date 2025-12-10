// <copyright file="HashDbMigrations.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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

namespace slskd.HashDb.Migrations;

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Serilog;

/// <summary>
///     Manages HashDb schema migrations.
/// </summary>
public static class HashDbMigrations
{
    /// <summary>
    ///     Current schema version. Increment when adding new migrations.
    /// </summary>
    public const int CurrentVersion = 9;

    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(HashDbMigrations));

    /// <summary>
    ///     Runs all pending migrations on the database.
    /// </summary>
    /// <param name="connection">Open SQLite connection.</param>
    /// <returns>The number of migrations applied.</returns>
    public static int RunMigrations(SqliteConnection connection)
    {
        EnsureMigrationTable(connection);
        var currentVersion = GetCurrentVersion(connection);

        Log.Information("[HashDb] Current schema version: {Version}, target: {Target}", currentVersion, CurrentVersion);

        if (currentVersion >= CurrentVersion)
        {
            Log.Information("[HashDb] Schema is up to date");
            return 0;
        }

        var migrationsApplied = 0;

        foreach (var migration in GetMigrations())
        {
            if (migration.Version <= currentVersion)
            {
                continue;
            }

            Log.Information("[HashDb] Applying migration {Version}: {Name}", migration.Version, migration.Name);

            using var transaction = connection.BeginTransaction();
            try
            {
                migration.Apply(connection);
                SetVersion(connection, migration.Version);
                transaction.Commit();
                migrationsApplied++;
                Log.Information("[HashDb] Migration {Version} applied successfully", migration.Version);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Log.Error(ex, "[HashDb] Migration {Version} failed, rolled back", migration.Version);
                throw new InvalidOperationException($"HashDb migration {migration.Version} ({migration.Name}) failed", ex);
            }
        }

        return migrationsApplied;
    }

    /// <summary>
    ///     Gets the current schema version from the database.
    /// </summary>
    public static int GetCurrentVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM __HashDbMigrations ORDER BY version DESC LIMIT 1";

        try
        {
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
        catch (SqliteException)
        {
            // Table doesn't exist yet
            return 0;
        }
    }

    private static void EnsureMigrationTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS __HashDbMigrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at INTEGER NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    private static void SetVersion(SqliteConnection connection, int version)
    {
        var migration = GetMigrations().Find(m => m.Version == version);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO __HashDbMigrations (version, name, applied_at)
            VALUES (@version, @name, @applied_at)";
        cmd.Parameters.AddWithValue("@version", version);
        cmd.Parameters.AddWithValue("@name", migration?.Name ?? $"Migration_{version}");
        cmd.Parameters.AddWithValue("@applied_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    private static List<Migration> GetMigrations()
    {
        return new List<Migration>
        {
            // Version 1: Initial schema (baseline)
            new Migration
            {
                Version = 1,
                Name = "Initial schema",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        -- Peer tracking with capabilities
                        CREATE TABLE IF NOT EXISTS Peers (
                            peer_id TEXT PRIMARY KEY,
                            caps INTEGER DEFAULT 0,
                            client_version TEXT,
                            last_seen INTEGER NOT NULL,
                            last_cap_check INTEGER,
                            backfills_today INTEGER DEFAULT 0,
                            backfill_reset_date INTEGER
                        );

                        -- FLAC file inventory with hash tracking
                        CREATE TABLE IF NOT EXISTS FlacInventory (
                            file_id TEXT PRIMARY KEY,
                            peer_id TEXT NOT NULL,
                            path TEXT NOT NULL,
                            size INTEGER NOT NULL,
                            discovered_at INTEGER NOT NULL,
                            hash_status TEXT DEFAULT 'none',
                            hash_value TEXT,
                            hash_source TEXT,
                            flac_audio_md5 TEXT,
                            sample_rate INTEGER,
                            channels INTEGER,
                            bit_depth INTEGER,
                            duration_samples INTEGER
                        );

                        -- DHT/Mesh hash database (content-addressed)
                        CREATE TABLE IF NOT EXISTS HashDb (
                            flac_key TEXT PRIMARY KEY,
                            byte_hash TEXT NOT NULL,
                            size INTEGER NOT NULL,
                            meta_flags INTEGER,
                            first_seen_at INTEGER NOT NULL,
                            last_updated_at INTEGER NOT NULL,
                            seq_id INTEGER,
                            use_count INTEGER DEFAULT 1
                        );

                        -- Mesh sync state per peer
                        CREATE TABLE IF NOT EXISTS MeshPeerState (
                            peer_id TEXT PRIMARY KEY,
                            last_sync_time INTEGER,
                            last_seq_seen INTEGER DEFAULT 0
                        );

                        -- Key-value store for misc state
                        CREATE TABLE IF NOT EXISTS HashDbState (
                            key TEXT PRIMARY KEY,
                            value TEXT
                        );

                        -- Indexes
                        CREATE INDEX IF NOT EXISTS idx_inventory_peer ON FlacInventory(peer_id);
                        CREATE INDEX IF NOT EXISTS idx_inventory_size ON FlacInventory(size);
                        CREATE INDEX IF NOT EXISTS idx_inventory_hash ON FlacInventory(hash_value);
                        CREATE INDEX IF NOT EXISTS idx_inventory_status ON FlacInventory(hash_status);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_size ON HashDb(size);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_seq ON HashDb(seq_id);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_hash ON HashDb(byte_hash);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            // Version 2: Extended schema for multi-source, probing, and future features
            new Migration
            {
                Version = 2,
                Name = "Extended schema for multi-source and probing",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();

                    // Add new columns to FlacInventory
                    cmd.CommandText = @"
                        ALTER TABLE FlacInventory ADD COLUMN full_file_hash TEXT;
                        ALTER TABLE FlacInventory ADD COLUMN min_block_size INTEGER;
                        ALTER TABLE FlacInventory ADD COLUMN max_block_size INTEGER;
                        ALTER TABLE FlacInventory ADD COLUMN encoder_info TEXT;
                        ALTER TABLE FlacInventory ADD COLUMN album_hash TEXT;
                        ALTER TABLE FlacInventory ADD COLUMN probe_fail_count INTEGER DEFAULT 0;
                        ALTER TABLE FlacInventory ADD COLUMN probe_fail_reason TEXT;
                        ALTER TABLE FlacInventory ADD COLUMN last_probe_at INTEGER;
                    ";

                    // SQLite doesn't support multiple ALTER statements in one command
                    foreach (var alterSql in cmd.CommandText.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.IsNullOrWhiteSpace(alterSql))
                        {
                            continue;
                        }

                        try
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = alterSql.Trim();
                            alterCmd.ExecuteNonQuery();
                        }
                        catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
                        {
                            // Column already exists - skip
                        }
                    }

                    // Add new columns to HashDb
                    var hashDbAlters = new[]
                    {
                        "ALTER TABLE HashDb ADD COLUMN full_file_hash TEXT",
                        "ALTER TABLE HashDb ADD COLUMN audio_fingerprint TEXT",
                        "ALTER TABLE HashDb ADD COLUMN musicbrainz_id TEXT",
                    };

                    foreach (var alterSql in hashDbAlters)
                    {
                        try
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = alterSql;
                            alterCmd.ExecuteNonQuery();
                        }
                        catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
                        {
                            // Column already exists - skip
                        }
                    }

                    // Create FileSources table
                    using var createCmd = conn.CreateCommand();
                    createCmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS FileSources (
                            content_hash TEXT NOT NULL,
                            peer_id TEXT NOT NULL,
                            path TEXT NOT NULL,
                            size INTEGER NOT NULL,
                            first_seen INTEGER NOT NULL,
                            last_seen INTEGER NOT NULL,
                            download_success_count INTEGER DEFAULT 0,
                            download_fail_count INTEGER DEFAULT 0,
                            avg_speed_bps INTEGER,
                            last_download_at INTEGER,
                            PRIMARY KEY (content_hash, peer_id, path)
                        );

                        CREATE INDEX IF NOT EXISTS idx_inventory_album ON FlacInventory(album_hash);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_full ON HashDb(full_file_hash);
                        CREATE INDEX IF NOT EXISTS idx_sources_hash ON FileSources(content_hash);
                        CREATE INDEX IF NOT EXISTS idx_sources_peer ON FileSources(peer_id);
                    ";
                    createCmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 3,
                Name = "MusicBrainz Album Targets",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS AlbumTargets (
                            release_id TEXT PRIMARY KEY,
                            discogs_release_id TEXT,
                            title TEXT NOT NULL,
                            artist TEXT NOT NULL,
                            metadata_release_date TEXT,
                            metadata_country TEXT,
                            metadata_label TEXT,
                            metadata_status TEXT,
                            created_at INTEGER NOT NULL DEFAULT (strftime('%s','now'))
                        );

                        CREATE TABLE IF NOT EXISTS AlbumTargetTracks (
                            release_id TEXT NOT NULL,
                            track_position INTEGER NOT NULL,
                            recording_id TEXT,
                            title TEXT NOT NULL,
                            artist TEXT NOT NULL,
                            duration_ms INTEGER,
                            isrc TEXT,
                            PRIMARY KEY (release_id, track_position)
                        );

                        CREATE INDEX IF NOT EXISTS idx_album_tracks_recording ON AlbumTargetTracks(recording_id);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 4,
                Name = "Canonical scoring variant metadata",
                Apply = conn =>
                {
                    var alterStatements = new[]
                    {
                        "ALTER TABLE HashDb ADD COLUMN variant_id TEXT",
                        "ALTER TABLE HashDb ADD COLUMN codec TEXT",
                        "ALTER TABLE HashDb ADD COLUMN container TEXT",
                        "ALTER TABLE HashDb ADD COLUMN sample_rate_hz INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN bit_depth INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN channels INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN duration_ms INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN bitrate_kbps INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN quality_score REAL DEFAULT 0.0",
                        "ALTER TABLE HashDb ADD COLUMN transcode_suspect BOOLEAN DEFAULT FALSE",
                        "ALTER TABLE HashDb ADD COLUMN transcode_reason TEXT",
                        "ALTER TABLE HashDb ADD COLUMN dynamic_range_dr REAL",
                        "ALTER TABLE HashDb ADD COLUMN loudness_lufs REAL",
                        "ALTER TABLE HashDb ADD COLUMN has_clipping BOOLEAN",
                        "ALTER TABLE HashDb ADD COLUMN encoder_signature TEXT",
                        "ALTER TABLE HashDb ADD COLUMN seen_count INTEGER DEFAULT 1",
                        "ALTER TABLE HashDb ADD COLUMN file_sha256 TEXT"
                    };

                    foreach (var alter in alterStatements)
                    {
                        try
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = alter;
                            alterCmd.ExecuteNonQuery();
                        }
                        catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
                        {
                            // Column already exists - skip
                        }
                    }

                    using var indexCmd = conn.CreateCommand();
                    indexCmd.CommandText = @"
                        CREATE INDEX IF NOT EXISTS idx_hashdb_variant ON HashDb(variant_id);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_recording_codec ON HashDb(musicbrainz_id, codec, sample_rate_hz);
                    ";
                    indexCmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 5,
                Name = "Canonical stats table",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS CanonicalStats (
                            id TEXT PRIMARY KEY,
                            musicbrainz_recording_id TEXT NOT NULL,
                            codec_profile_key TEXT NOT NULL,
                            variant_count INTEGER DEFAULT 0,
                            total_seen_count INTEGER DEFAULT 0,
                            avg_quality_score REAL DEFAULT 0.0,
                            max_quality_score REAL DEFAULT 0.0,
                            percent_transcode_suspect REAL DEFAULT 0.0,
                            codec_distribution TEXT,
                            bitrate_distribution TEXT,
                            sample_rate_distribution TEXT,
                            best_variant_id TEXT,
                            canonicality_score REAL DEFAULT 0.0,
                            last_updated INTEGER NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS idx_canonical_recording ON CanonicalStats(musicbrainz_recording_id);
                        CREATE INDEX IF NOT EXISTS idx_canonical_profile ON CanonicalStats(codec_profile_key);
                        CREATE INDEX IF NOT EXISTS idx_canonical_score ON CanonicalStats(canonicality_score DESC);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 6,
                Name = "Library Health tables",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS LibraryHealthIssues (
                            issue_id TEXT PRIMARY KEY,
                            type TEXT NOT NULL,
                            severity TEXT NOT NULL,
                            file_path TEXT,
                            mb_recording_id TEXT,
                            mb_release_id TEXT,
                            artist TEXT,
                            album TEXT,
                            title TEXT,
                            reason TEXT,
                            metadata TEXT,
                            can_auto_fix BOOLEAN DEFAULT FALSE,
                            suggested_action TEXT,
                            remediation_job_id TEXT,
                            status TEXT DEFAULT 'detected',
                            detected_at INTEGER NOT NULL,
                            resolved_at INTEGER,
                            resolved_by TEXT
                        );

                        CREATE INDEX IF NOT EXISTS idx_issues_status ON LibraryHealthIssues(status);
                        CREATE INDEX IF NOT EXISTS idx_issues_type ON LibraryHealthIssues(type);
                        CREATE INDEX IF NOT EXISTS idx_issues_severity ON LibraryHealthIssues(severity);
                        CREATE INDEX IF NOT EXISTS idx_issues_release ON LibraryHealthIssues(mb_release_id);
                        CREATE INDEX IF NOT EXISTS idx_issues_file ON LibraryHealthIssues(file_path);

                        CREATE TABLE IF NOT EXISTS LibraryHealthScans (
                            scan_id TEXT PRIMARY KEY,
                            library_path TEXT NOT NULL,
                            started_at INTEGER NOT NULL,
                            completed_at INTEGER,
                            status TEXT DEFAULT 'running',
                            files_scanned INTEGER DEFAULT 0,
                            issues_detected INTEGER DEFAULT 0,
                            error_message TEXT
                        );

                        CREATE INDEX IF NOT EXISTS idx_scans_path ON LibraryHealthScans(library_path);
                        CREATE INDEX IF NOT EXISTS idx_scans_completed ON LibraryHealthScans(completed_at DESC);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 7,
                Name = "Codec-specific fingerprints and analyzer version",
                Apply = conn =>
                {
                    var alterStatements = new[]
                    {
                        "ALTER TABLE HashDb ADD COLUMN flac_streaminfo_hash42 TEXT",
                        "ALTER TABLE HashDb ADD COLUMN flac_pcm_md5 TEXT",
                        "ALTER TABLE HashDb ADD COLUMN flac_min_block_size INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN flac_max_block_size INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN flac_min_frame_size INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN flac_max_frame_size INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN flac_total_samples INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN mp3_stream_hash TEXT",
                        "ALTER TABLE HashDb ADD COLUMN mp3_encoder TEXT",
                        "ALTER TABLE HashDb ADD COLUMN mp3_encoder_preset TEXT",
                        "ALTER TABLE HashDb ADD COLUMN mp3_frames_analyzed INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN effective_bandwidth_hz REAL",
                        "ALTER TABLE HashDb ADD COLUMN nominal_lowpass_hz REAL",
                        "ALTER TABLE HashDb ADD COLUMN spectral_flatness_score REAL",
                        "ALTER TABLE HashDb ADD COLUMN hf_energy_ratio REAL",
                        "ALTER TABLE HashDb ADD COLUMN opus_stream_hash TEXT",
                        "ALTER TABLE HashDb ADD COLUMN opus_nominal_bitrate_kbps INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN opus_application TEXT",
                        "ALTER TABLE HashDb ADD COLUMN opus_bandwidth_mode TEXT",
                        "ALTER TABLE HashDb ADD COLUMN aac_stream_hash TEXT",
                        "ALTER TABLE HashDb ADD COLUMN aac_profile TEXT",
                        "ALTER TABLE HashDb ADD COLUMN aac_sbr_present BOOLEAN",
                        "ALTER TABLE HashDb ADD COLUMN aac_ps_present BOOLEAN",
                        "ALTER TABLE HashDb ADD COLUMN aac_nominal_bitrate_kbps INTEGER",
                        "ALTER TABLE HashDb ADD COLUMN audio_sketch_hash TEXT",
                        "ALTER TABLE HashDb ADD COLUMN analyzer_version TEXT",
                    };

                    foreach (var alter in alterStatements)
                    {
                        try
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = alter;
                            alterCmd.ExecuteNonQuery();
                        }
                        catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
                        {
                            // Column already exists - skip
                        }
                    }

                    using var indexCmd = conn.CreateCommand();
                    indexCmd.CommandText = @"
                        CREATE INDEX IF NOT EXISTS idx_hashdb_flac_streaminfo ON HashDb(flac_streaminfo_hash42);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_flac_pcm ON HashDb(flac_pcm_md5);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_mp3_stream ON HashDb(mp3_stream_hash);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_encoder ON HashDb(mp3_encoder);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_opus_stream ON HashDb(opus_stream_hash);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_aac_stream ON HashDb(aac_stream_hash);
                        CREATE INDEX IF NOT EXISTS idx_hashdb_aac_profile ON HashDb(aac_profile);
                    ";
                    indexCmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 8,
                Name = "Artist release graph cache",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ArtistReleaseGraphs (
                            artist_id TEXT PRIMARY KEY,
                            name TEXT NOT NULL,
                            cached_at INTEGER NOT NULL,
                            expires_at INTEGER,
                            json_data TEXT NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS idx_artist_release_graph_expiry ON ArtistReleaseGraphs(expires_at);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 9,
                Name = "Discography job cache",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS DiscographyJobs (
                            job_id TEXT PRIMARY KEY,
                            artist_id TEXT NOT NULL,
                            artist_name TEXT NOT NULL,
                            profile TEXT NOT NULL,
                            target_directory TEXT NOT NULL,
                            total_releases INTEGER DEFAULT 0,
                            completed_releases INTEGER DEFAULT 0,
                            failed_releases INTEGER DEFAULT 0,
                            status TEXT DEFAULT 'pending',
                            created_at INTEGER NOT NULL,
                            json_data TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS DiscographyReleaseJobs (
                            discography_job_id TEXT NOT NULL,
                            release_id TEXT NOT NULL,
                            status TEXT DEFAULT 'pending',
                            PRIMARY KEY (discography_job_id, release_id),
                            FOREIGN KEY (discography_job_id) REFERENCES DiscographyJobs(job_id)
                        );
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 10,
                Name = "Label crate job cache",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS LabelCrateJobs (
                            job_id TEXT PRIMARY KEY,
                            label_id TEXT,
                            label_name TEXT NOT NULL,
                            limit_count INTEGER DEFAULT 0,
                            total_releases INTEGER DEFAULT 0,
                            completed_releases INTEGER DEFAULT 0,
                            failed_releases INTEGER DEFAULT 0,
                            status TEXT DEFAULT 'pending',
                            created_at INTEGER NOT NULL,
                            json_data TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS LabelCrateReleaseJobs (
                            label_crate_job_id TEXT NOT NULL,
                            release_id TEXT NOT NULL,
                            status TEXT DEFAULT 'pending',
                            PRIMARY KEY (label_crate_job_id, release_id),
                            FOREIGN KEY (label_crate_job_id) REFERENCES LabelCrateJobs(job_id)
                        );
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 11,
                Name = "Peer metrics storage",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS PeerMetrics (
                            peer_id TEXT PRIMARY KEY,
                            source TEXT NOT NULL,
                            rtt_avg_ms REAL,
                            rtt_stddev_ms REAL,
                            last_rtt_sample INTEGER,
                            throughput_avg_bps REAL,
                            throughput_stddev_bps REAL,
                            total_bytes INTEGER,
                            last_throughput_sample INTEGER,
                            chunks_requested INTEGER,
                            chunks_completed INTEGER,
                            chunks_failed INTEGER,
                            chunks_timedout INTEGER,
                            chunks_corrupted INTEGER,
                            sample_count INTEGER,
                            first_seen INTEGER,
                            last_updated INTEGER,
                            reputation_score REAL,
                            reputation_updated_at INTEGER
                        );
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 12,
                Name = "Traffic accounting",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS TrafficStats (
                            key TEXT PRIMARY KEY,
                            overlay_upload_bytes INTEGER DEFAULT 0,
                            overlay_download_bytes INTEGER DEFAULT 0,
                            soulseek_upload_bytes INTEGER DEFAULT 0,
                            soulseek_download_bytes INTEGER DEFAULT 0,
                            updated_at INTEGER NOT NULL
                        );
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 13,
                Name = "Warm cache popularity",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS WarmCachePopularity (
                            content_id TEXT PRIMARY KEY,
                            hits INTEGER DEFAULT 0,
                            last_updated INTEGER NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS idx_warmcache_hits ON WarmCachePopularity(hits DESC);
                        CREATE INDEX IF NOT EXISTS idx_warmcache_updated ON WarmCachePopularity(last_updated DESC);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 14,
                Name = "Warm cache entries",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS WarmCacheEntries (
                            content_id TEXT PRIMARY KEY,
                            path TEXT NOT NULL,
                            size_bytes INTEGER NOT NULL,
                            pinned INTEGER DEFAULT 0,
                            last_accessed INTEGER NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS idx_warmcache_last_accessed ON WarmCacheEntries(last_accessed);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },

            new Migration
            {
                Version = 15,
                Name = "Virtual Soulfind pseudonyms",
                Apply = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Pseudonyms (
                            SoulseekUsername TEXT PRIMARY KEY NOT NULL,
                            PeerId TEXT NOT NULL,
                            UpdatedAt INTEGER NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS idx_pseudonyms_peer_id ON Pseudonyms(PeerId);
                    ";
                    cmd.ExecuteNonQuery();
                },
            },
        };
    }

    /// <summary>
    ///     Represents a database migration.
    /// </summary>
    private class Migration
    {
        /// <summary>Gets or sets the migration version number.</summary>
        public int Version { get; set; }

        /// <summary>Gets or sets the migration name/description.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the migration action.</summary>
        public Action<SqliteConnection> Apply { get; set; }
    }
}

