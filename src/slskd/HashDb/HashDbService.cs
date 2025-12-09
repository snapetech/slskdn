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
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Serilog;
    using slskd.Capabilities;
    using slskd.Events;
    using slskd.HashDb.Models;
    using slskd.Mesh;

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
        private long currentSeqId;

        /// <summary>
        ///     Initializes a new instance of the <see cref="HashDbService"/> class.
        /// </summary>
        /// <param name="appDirectory">The application data directory.</param>
        /// <param name="eventBus">The event bus for subscribing to download events (optional).</param>
        /// <param name="serviceProvider">Service provider for lazy resolution of mesh sync (optional).</param>
        public HashDbService(string appDirectory, EventBus eventBus = null, IServiceProvider serviceProvider = null)
        {
            this.serviceProvider = serviceProvider;
            dbPath = Path.Combine(appDirectory, "hashdb.db");
            InitializeDatabase();
            currentSeqId = GetLatestSeqIdSync();
            log.Information("[HashDb] Initialized at {Path}, current seq_id: {SeqId}", dbPath, currentSeqId);

            // Subscribe to download completion events to automatically hash downloaded files
            if (eventBus != null)
            {
                eventBus.Subscribe<DownloadFileCompleteEvent>("HashDbService.DownloadComplete", OnDownloadCompleteAsync);
                log.Information("[HashDb] Subscribed to download completion events for automatic hashing");
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
                if (!File.Exists(filename))
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

        private SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            return conn;
        }

        private void InitializeDatabase()
        {
            using var conn = GetConnection();

            var schema = @"
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

            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema;
            cmd.ExecuteNonQuery();
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

            if (File.Exists(dbPath))
            {
                stats.DatabaseSizeBytes = new FileInfo(dbPath).Length;
            }

            return stats;
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
            return new FlacInventoryEntry
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
        }

        private static HashDbEntry ReadHashEntry(SqliteDataReader reader)
        {
            return new HashDbEntry
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
        }
    }
}


