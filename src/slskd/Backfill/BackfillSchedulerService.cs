// <copyright file="BackfillSchedulerService.cs" company="slskdn Team">
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

namespace slskd.Backfill
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Soulseek;
    using slskd.HashDb;
    using slskd.HashDb.Models;
    using slskd.Mesh;
    using slskd.Transfers.MultiSource;

    /// <summary>
    ///     Service for scheduling conservative header probing to discover FLAC hashes.
    /// </summary>
    public class BackfillSchedulerService : BackgroundService, IBackfillSchedulerService
    {
        private readonly IHashDbService hashDb;
        private readonly IMeshSyncService meshSync;
        private readonly ISoulseekClient soulseekClient;
        private readonly ILogger log = Log.ForContext<BackfillSchedulerService>();

        private readonly BackfillConfig config = new();
        private readonly BackfillStats stats = new();
        private readonly SemaphoreSlim backfillLock = new(2, 2); // Max 2 concurrent
        private int activeBackfills;
        private bool isIdle;
        private DateTime? idleStartTime;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BackfillSchedulerService"/> class.
        /// </summary>
        public BackfillSchedulerService(
            IHashDbService hashDb,
            IMeshSyncService meshSync,
            ISoulseekClient soulseekClient)
        {
            this.hashDb = hashDb;
            this.meshSync = meshSync;
            this.soulseekClient = soulseekClient;
        }

        /// <inheritdoc/>
        public BackfillStats Stats
        {
            get
            {
                stats.Active = activeBackfills;
                stats.IsIdle = isIdle;
                stats.IdleDuration = idleStartTime.HasValue ? DateTime.UtcNow - idleStartTime.Value : null;
                return stats;
            }
        }

        /// <inheritdoc/>
        public BackfillConfig Config => config;

        /// <inheritdoc/>
        public bool IsEnabled => config.Enabled;

        /// <inheritdoc/>
        public bool IsIdle => isIdle;

        /// <inheritdoc/>
        public int ActiveBackfillCount => activeBackfills;

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            config.Enabled = enabled;
            log.Information("[BACKFILL] Scheduler {State}", enabled ? "enabled" : "disabled");
        }

        /// <inheritdoc/>
        public void ReportIdle()
        {
            if (!isIdle)
            {
                isIdle = true;
                idleStartTime = DateTime.UtcNow;
                log.Debug("[BACKFILL] System now idle");
            }
        }

        /// <inheritdoc/>
        public void ReportBusy()
        {
            if (isIdle)
            {
                isIdle = false;
                idleStartTime = null;
                log.Debug("[BACKFILL] System now busy");
            }
        }

        /// <inheritdoc/>
        public async Task<BackfillCycleResult> TriggerCycleAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new BackfillCycleResult();

            try
            {
                // Get candidates
                var candidates = await GetCandidatesAsync(10, cancellationToken);
                result.CandidatesEvaluated = candidates.Count();

                foreach (var candidate in candidates)
                {
                    if (activeBackfills >= config.MaxGlobalConnections)
                    {
                        log.Debug("[BACKFILL] Max concurrent limit reached, stopping cycle");
                        break;
                    }

                    // Check rate limit
                    var todayCount = await hashDb.GetPeerBackfillCountTodayAsync(candidate.PeerId, cancellationToken);
                    if (todayCount >= config.MaxPerPeerPerDay)
                    {
                        result.RateLimited++;
                        stats.RateLimited++;
                        continue;
                    }

                    // Skip slskdn peers (they can mesh sync instead)
                    if (candidate.IsPeerSlskdn)
                    {
                        continue;
                    }

                    // Attempt backfill
                    result.BackfillsAttempted++;
                    var backfillResult = await BackfillFileAsync(candidate.PeerId, candidate.Path, candidate.Size, cancellationToken);
                    result.Results.Add(backfillResult);

                    if (backfillResult.Success)
                    {
                        result.Successful++;
                        stats.Successful++;
                        stats.HashesDiscovered++;
                    }
                    else
                    {
                        result.Failed++;
                        stats.Failed++;
                    }

                    stats.TotalAttempts++;
                }

                stats.LastCycleTime = DateTime.UtcNow;
                stats.NextCycleTime = DateTime.UtcNow.AddSeconds(config.RunIntervalSeconds);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[BACKFILL] Cycle failed");
            }

            result.DurationMs = sw.ElapsedMilliseconds;
            log.Information("[BACKFILL] Cycle complete: {Attempted} attempted, {Success} success, {Failed} failed, {RateLimited} rate-limited in {Duration}ms",
                result.BackfillsAttempted, result.Successful, result.Failed, result.RateLimited, result.DurationMs);

            return result;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<BackfillCandidate>> GetCandidatesAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            var entries = await hashDb.GetBackfillCandidatesAsync(limit, cancellationToken);

            var candidates = new List<BackfillCandidate>();
            foreach (var entry in entries)
            {
                var backfillsToday = await hashDb.GetPeerBackfillCountTodayAsync(entry.PeerId, cancellationToken);

                candidates.Add(new BackfillCandidate
                {
                    FileId = entry.FileId,
                    PeerId = entry.PeerId,
                    Path = entry.Path,
                    Size = entry.Size,
                    DiscoveredAt = entry.DiscoveredAtUtc,
                    PeerBackfillsToday = backfillsToday,
                    IsPeerOnline = true, // TODO: Check actual online status
                    IsPeerSlskdn = false, // TODO: Check capability service
                });
            }

            return candidates;
        }

        /// <inheritdoc/>
        public async Task<BackfillResult> BackfillFileAsync(string peerId, string path, long size, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new BackfillResult
            {
                PeerId = peerId,
                Path = path,
            };

            // Generate file ID for tracking
            var fileId = FlacInventoryEntry.GenerateFileId(peerId, path, size);

            // Mark as pending
            await hashDb.UpsertFlacEntryAsync(new FlacInventoryEntry
            {
                FileId = fileId,
                PeerId = peerId,
                Path = path,
                Size = size,
                HashStatusStr = "pending",
            }, cancellationToken);

            try
            {
                await backfillLock.WaitAsync(cancellationToken);
                Interlocked.Increment(ref activeBackfills);

                log.Debug("[BACKFILL] Probing {Peer}/{Path} ({Size} bytes)", peerId, path, size);

                // Download header bytes
                var buffer = new byte[config.MaxHeaderBytes];
                int bytesRead = 0;

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(config.TransferTimeoutSeconds));

                    using var memoryStream = new System.IO.MemoryStream(config.MaxHeaderBytes);
                    var limitedStream = new LimitedWriteStream(memoryStream, config.MaxHeaderBytes, cts);

                    try
                    {
                        await soulseekClient.DownloadAsync(
                            username: peerId,
                            remoteFilename: path,
                            outputStreamFactory: () => Task.FromResult<System.IO.Stream>(limitedStream),
                            size: size,
                            startOffset: 0,
                            cancellationToken: cts.Token,
                            options: new TransferOptions(
                                maximumLingerTime: 1000,
                                disposeOutputStreamOnCompletion: false));
                    }
                    catch (OperationCanceledException) when (limitedStream.LimitReached)
                    {
                        // Expected: we got enough bytes
                    }

                    if (limitedStream.BytesWritten > 0)
                    {
                        var data = memoryStream.ToArray();
                        bytesRead = data.Length;
                        Array.Copy(data, buffer, Math.Min(data.Length, buffer.Length));
                        
                        // Treat as success if we got enough bytes to parse
                        if (bytesRead >= 42)
                        {
                            // Proceed to parse below
                        }
                        else
                        {
                            throw new Exception($"Only read {bytesRead} bytes, need at least 42 for FLAC header");
                        }
                    }
                    else
                    {
                        throw new Exception("No data received");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || bytesRead < 42)
                {
                    // If we didn't get enough bytes, fail
                    result.Success = false;
                    result.Error = ex.Message;
                    throw; // Re-throw to catch block below
                }

                // Parse FLAC header to get hash
                var hash = ParseFlacHeader(buffer, bytesRead);
                if (hash != null)
                {
                    result.Success = true;
                    result.Hash = hash;
                    result.FlacKey = HashDbEntry.GenerateFlacKey(path, size);

                    // Update inventory
                    await hashDb.UpdateFlacHashAsync(fileId, hash, HashSource.BackfillSniff, cancellationToken);

                    // Store in hash DB
                    await hashDb.StoreHashFromVerificationAsync(path, size, hash, cancellationToken: cancellationToken);

                    // Publish to mesh
                    await meshSync.PublishHashAsync(result.FlacKey, hash, size, cancellationToken: cancellationToken);

                    // Increment backfill count
                    await hashDb.IncrementPeerBackfillCountAsync(peerId, cancellationToken);

                    log.Information("[BACKFILL] ✓ Discovered hash for {Peer}/{Path}: {Hash}", peerId, System.IO.Path.GetFileName(path), hash.Substring(0, 16));
                }
                else
                {
                    result.Error = "Failed to parse FLAC header";
                    await hashDb.MarkFlacHashFailedAsync(fileId, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                await hashDb.MarkFlacHashFailedAsync(fileId, cancellationToken);
                log.Debug("[BACKFILL] ✗ Failed {Peer}/{Path}: {Error}", peerId, System.IO.Path.GetFileName(path), ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref activeBackfills);
                backfillLock.Release();
            }

            result.DurationMs = sw.ElapsedMilliseconds;
            return result;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            log.Information("[BACKFILL] Background service started (interval: {Interval}s, max concurrent: {Max})",
                config.RunIntervalSeconds, config.MaxGlobalConnections);

            // Initial delay
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    stats.NextCycleTime = DateTime.UtcNow.AddSeconds(config.RunIntervalSeconds);

                    if (config.Enabled && ShouldRunCycle())
                    {
                        await TriggerCycleAsync(stoppingToken);
                    }
                    else
                    {
                        log.Debug("[BACKFILL] Skipping cycle (enabled={Enabled}, idle={Idle}, idleTime={IdleTime})",
                            config.Enabled, isIdle, stats.IdleDuration);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.Warning(ex, "[BACKFILL] Background cycle error");
                }

                await Task.Delay(TimeSpan.FromSeconds(config.RunIntervalSeconds), stoppingToken);
            }

            log.Information("[BACKFILL] Background service stopped");
        }

        private bool ShouldRunCycle()
        {
            // Must be idle for at least MinIdleTimeSeconds
            if (!isIdle || !idleStartTime.HasValue)
            {
                return false;
            }

            var idleDuration = DateTime.UtcNow - idleStartTime.Value;
            return idleDuration.TotalSeconds >= config.MinIdleTimeSeconds;
        }

        private static string ParseFlacHeader(byte[] buffer, int length)
        {
            if (length < 42)
            {
                return null;
            }

            // Check FLAC magic
            if (buffer[0] != 'f' || buffer[1] != 'L' || buffer[2] != 'a' || buffer[3] != 'C')
            {
                return null;
            }

            // STREAMINFO block should be at offset 4
            // Block type in bits 1-6 of first byte (0 = STREAMINFO)
            var blockType = buffer[4] & 0x7F;
            if (blockType != 0)
            {
                return null;
            }

            // Block length in next 3 bytes
            var blockLength = (buffer[5] << 16) | (buffer[6] << 8) | buffer[7];
            if (blockLength < 34 || 8 + blockLength > length)
            {
                return null;
            }

            // STREAMINFO is 34 bytes, MD5 is last 16 bytes
            // SHA256 the first 32KB instead for byte-identical verification
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(buffer, 0, Math.Min(length, 32768));
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}


