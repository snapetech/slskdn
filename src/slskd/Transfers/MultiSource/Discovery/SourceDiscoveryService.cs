// <copyright file="SourceDiscoveryService.cs" company="slskd Team">
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

namespace slskd.Transfers.MultiSource.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Service for continuous background discovery of file sources.
    /// </summary>
    public class SourceDiscoveryService : IHostedService, ISourceDiscoveryService
    {
        /// <summary>
        ///     Search window duration in milliseconds (4 minutes 30 seconds).
        /// </summary>
        private const int SearchWindowMs = 270000;

        /// <summary>
        ///     Pause between search cycles in milliseconds (1 second).
        /// </summary>
        private const int CyclePauseMs = 1000;

        private readonly ISoulseekClient client;
        private readonly IContentVerificationService verificationService;
        private readonly string dbPath;
        private readonly ILogger log = Log.ForContext<SourceDiscoveryService>();

        private CancellationTokenSource cts;
        private Task discoveryTask;
        private int searchCycles;
        private int lastCycleNewFiles;
        private bool enableHashVerification;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SourceDiscoveryService"/> class.
        /// </summary>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="verificationService">The content verification service.</param>
        public SourceDiscoveryService(
            ISoulseekClient soulseekClient,
            IContentVerificationService verificationService)
        {
            client = soulseekClient;
            this.verificationService = verificationService;

            // Store DB in the app data directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var slskdPath = Path.Combine(appDataPath, "slskd");
            System.IO.Directory.CreateDirectory(slskdPath);
            dbPath = Path.Combine(slskdPath, "discovery.db");

            InitializeDatabase();
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            log.Information("[Discovery] Service started.");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            log.Information("[Discovery] Service stopping.");
            await StopDiscoveryAsync();
        }

        /// <inheritdoc/>
        public bool IsRunning => discoveryTask != null && !discoveryTask.IsCompleted;

        /// <inheritdoc/>
        public string CurrentSearchTerm { get; private set; }

        /// <inheritdoc/>
        public async Task StartDiscoveryAsync(string searchTerm, bool enableHashVerification = true, CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                log.Warning("[Discovery] Already running. Stop first before starting a new discovery.");
                return;
            }

            CurrentSearchTerm = searchTerm;
            this.enableHashVerification = enableHashVerification;
            searchCycles = 0;
            lastCycleNewFiles = 0;

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            log.Information("[Discovery] Starting continuous discovery for: {SearchTerm} (hash verification: {HashEnabled})",
                searchTerm, enableHashVerification);

            discoveryTask = Task.Run(() => DiscoveryLoopAsync(cts.Token), cts.Token);

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopDiscoveryAsync()
        {
            if (!IsRunning)
            {
                return;
            }

            log.Information("[Discovery] Stopping discovery...");
            cts?.Cancel();

            if (discoveryTask != null)
            {
                try
                {
                    await discoveryTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            log.Information("[Discovery] Stopped. Total cycles: {Cycles}", searchCycles);
        }

        /// <inheritdoc/>
        public DiscoveryStats GetStats()
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var stats = new DiscoveryStats
            {
                SearchCycles = searchCycles,
                LastCycleNewFiles = lastCycleNewFiles,
                HashVerificationEnabled = enableHashVerification,
            };

            // Total files
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM DiscoveredFiles";
                stats.TotalFiles = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // Total users
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(DISTINCT Username) FROM DiscoveredFiles";
                stats.TotalUsers = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // Files with hash
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM DiscoveredFiles WHERE Hash IS NOT NULL";
                stats.FilesWithHash = Convert.ToInt32(cmd.ExecuteScalar());
            }

            return stats;
        }

        /// <inheritdoc/>
        public List<DiscoveredSource> GetSourcesBySize(long size, int limit = 100)
        {
            var results = new List<DiscoveredSource>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Username, Filename, Size, Hash, UploadSpeed, FirstSeenUnix, LastSeenUnix
                FROM DiscoveredFiles
                WHERE Size = @size
                ORDER BY UploadSpeed DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@size", size);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new DiscoveredSource
                {
                    Username = reader.GetString(0),
                    Filename = reader.GetString(1),
                    Size = reader.GetInt64(2),
                    Hash = reader.IsDBNull(3) ? null : reader.GetString(3),
                    UploadSpeed = reader.GetInt32(4),
                    FirstSeenUnix = reader.GetInt64(5),
                    LastSeenUnix = reader.GetInt64(6),
                });
            }

            return results;
        }

        /// <inheritdoc/>
        public List<DiscoveredSource> GetSourcesByFilename(string filenamePattern, int limit = 100)
        {
            var results = new List<DiscoveredSource>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Username, Filename, Size, Hash, UploadSpeed, FirstSeenUnix, LastSeenUnix
                FROM DiscoveredFiles
                WHERE Filename LIKE @pattern
                ORDER BY UploadSpeed DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@pattern", $"%{filenamePattern}%");
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new DiscoveredSource
                {
                    Username = reader.GetString(0),
                    Filename = reader.GetString(1),
                    Size = reader.GetInt64(2),
                    Hash = reader.IsDBNull(3) ? null : reader.GetString(3),
                    UploadSpeed = reader.GetInt32(4),
                    FirstSeenUnix = reader.GetInt64(5),
                    LastSeenUnix = reader.GetInt64(6),
                });
            }

            return results;
        }

        /// <inheritdoc/>
        public List<FileSizeSummary> GetFileSizeSummaries(int minSources = 2)
        {
            var results = new List<FileSizeSummary>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Size, COUNT(DISTINCT Username) as SourceCount, MIN(Filename) as SampleFilename
                FROM DiscoveredFiles
                GROUP BY Size
                HAVING SourceCount >= @minSources
                ORDER BY SourceCount DESC
                LIMIT 100";
            cmd.Parameters.AddWithValue("@minSources", minSources);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new FileSizeSummary
                {
                    Size = reader.GetInt64(0),
                    SourceCount = reader.GetInt32(1),
                    SampleFilename = reader.GetString(2),
                });
            }

            return results;
        }

        /// <inheritdoc/>
        public int GetNoPartialSupportCount()
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM DiscoveredFiles WHERE SupportsPartial = 0";

            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result ?? 0);
        }

        /// <inheritdoc/>
        public void ResetPartialSupportFlags()
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE DiscoveredFiles SET SupportsPartial = 1 WHERE SupportsPartial = 0";
            var affected = cmd.ExecuteNonQuery();

            log.Information("[Discovery] Reset partial support flags for {Count} entries", affected);
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS DiscoveredFiles (
                    Username TEXT NOT NULL,
                    Filename TEXT NOT NULL,
                    Size INTEGER NOT NULL,
                    Hash TEXT,
                    UploadSpeed INTEGER DEFAULT 0,
                    FirstSeenUnix INTEGER NOT NULL,
                    LastSeenUnix INTEGER NOT NULL,
                    PRIMARY KEY (Username, Filename, Size)
                );
                CREATE INDEX IF NOT EXISTS idx_size ON DiscoveredFiles(Size);
                CREATE INDEX IF NOT EXISTS idx_filename ON DiscoveredFiles(Filename);
                CREATE INDEX IF NOT EXISTS idx_hash ON DiscoveredFiles(Hash);
            ";
            cmd.ExecuteNonQuery();

            log.Information("[Discovery] Database initialized at {Path}", dbPath);
        }

        private async Task DiscoveryLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    searchCycles++;
                    log.Information("[Discovery] Starting search cycle #{Cycle} for: {Term}", searchCycles, CurrentSearchTerm);

                    // Collect responses during the search window
                    var responses = new List<SearchResponse>();
                    var searchStarted = DateTime.UtcNow;

                    try
                    {
                        await client.SearchAsync(
                            SearchQuery.FromText(CurrentSearchTerm),
                            responseHandler: (response) => responses.Add(response),
                            options: new SearchOptions(
                                searchTimeout: SearchWindowMs / 1000, // Convert to seconds
                                responseLimit: 10000,
                                fileLimit: 1000000,
                                filterResponses: true,
                                minimumResponseFileCount: 1),
                            cancellationToken: cancellationToken);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Search timeout is expected
                    }

                    var searchDuration = DateTime.UtcNow - searchStarted;
                    log.Information("[Discovery] Search cycle #{Cycle} collected {Count} responses in {Duration:F1}s",
                        searchCycles, responses.Count, searchDuration.TotalSeconds);

                    // Process responses and store in DB
                    lastCycleNewFiles = await ProcessResponsesAsync(responses, cancellationToken);

                    log.Information("[Discovery] Cycle #{Cycle} complete. New files: {New}. Total in DB: {Total}",
                        searchCycles, lastCycleNewFiles, GetStats().TotalFiles);

                    // Small pause before next cycle
                    await Task.Delay(CyclePauseMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.Error(ex, "[Discovery] Error in discovery loop: {Message}", ex.Message);
                    await Task.Delay(5000, cancellationToken); // Wait before retry
                }
            }
        }

        private async Task<int> ProcessResponsesAsync(List<SearchResponse> responses, CancellationToken cancellationToken)
        {
            var newFiles = 0;
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var response in responses)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    foreach (var file in response.Files)
                    {
                        // Insert or update (UPDATE LastSeenUnix if exists)
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            INSERT INTO DiscoveredFiles (Username, Filename, Size, UploadSpeed, FirstSeenUnix, LastSeenUnix)
                            VALUES (@username, @filename, @size, @speed, @now, @now)
                            ON CONFLICT(Username, Filename, Size) DO UPDATE SET
                                LastSeenUnix = @now,
                                UploadSpeed = @speed";
                        cmd.Parameters.AddWithValue("@username", response.Username);
                        cmd.Parameters.AddWithValue("@filename", file.Filename);
                        cmd.Parameters.AddWithValue("@size", file.Size);
                        cmd.Parameters.AddWithValue("@speed", response.UploadSpeed);
                        cmd.Parameters.AddWithValue("@now", nowUnix);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected > 0)
                        {
                            // Check if this was an INSERT (new) vs UPDATE
                            // SQLite doesn't distinguish easily, so we count all inserts
                            // A more accurate count would require checking rowid changes
                            newFiles++;
                        }
                    }
                }

                transaction.Commit();

                // Future: Hash verification for FLAC files
                if (enableHashVerification)
                {
                    await VerifyFlacHashesAsync(connection, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                log.Error(ex, "[Discovery] Failed to process responses: {Message}", ex.Message);
                throw;
            }

            return newFiles;
        }

        private async Task VerifyFlacHashesAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            // Get FLAC files without hash
            var filesToVerify = new List<(string Username, string Filename, long Size)>();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Username, Filename, Size
                    FROM DiscoveredFiles
                    WHERE Hash IS NULL
                      AND LOWER(Filename) LIKE '%.flac'
                    LIMIT 10"; // Process in batches

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    filesToVerify.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
                }
            }

            if (filesToVerify.Count == 0)
            {
                return;
            }

            log.Debug("[Discovery] Verifying hashes for {Count} FLAC files", filesToVerify.Count);

            foreach (var (username, filename, size) in filesToVerify)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var hash = await verificationService.GetContentHashAsync(username, filename, size, cancellationToken);

                    if (!string.IsNullOrEmpty(hash))
                    {
                        using var updateCmd = connection.CreateCommand();
                        updateCmd.CommandText = @"
                            UPDATE DiscoveredFiles
                            SET Hash = @hash
                            WHERE Username = @username AND Filename = @filename AND Size = @size";
                        updateCmd.Parameters.AddWithValue("@hash", hash);
                        updateCmd.Parameters.AddWithValue("@username", username);
                        updateCmd.Parameters.AddWithValue("@filename", filename);
                        updateCmd.Parameters.AddWithValue("@size", size);
                        updateCmd.ExecuteNonQuery();

                        log.Debug("[Discovery] Verified hash for {Filename} from {Username}: {Hash}",
                            Path.GetFileName(filename), username, hash.Substring(0, 16) + "...");
                    }
                }
                catch (Exception ex)
                {
                    log.Warning("[Discovery] Failed to verify hash for {Filename} from {Username}: {Message}",
                        filename, username, ex.Message);
                }
            }
        }
    }
}
