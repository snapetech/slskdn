// <copyright file="MultiSourceDownloadService.cs" company="slskd Team">
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

namespace slskd.Transfers.MultiSource
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;
    using IODirectory = System.IO.Directory;
    using IOPath = System.IO.Path;
    using FileStream = System.IO.FileStream;
    using FileMode = System.IO.FileMode;
    using FileAccess = System.IO.FileAccess;
    using Stream = System.IO.Stream;

    /// <summary>
    ///     Experimental multi-source download service.
    /// </summary>
    public class MultiSourceDownloadService : IMultiSourceDownloadService
    {
        /// <summary>
        ///     Default chunk size for parallel downloads (1MB).
        /// </summary>
        public const int DefaultChunkSize = 1024 * 1024;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MultiSourceDownloadService"/> class.
        /// </summary>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="contentVerificationService">The content verification service.</param>
        public MultiSourceDownloadService(
            ISoulseekClient soulseekClient,
            IContentVerificationService contentVerificationService)
        {
            Client = soulseekClient;
            ContentVerification = contentVerificationService;
        }

        private ISoulseekClient Client { get; }
        private IContentVerificationService ContentVerification { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<MultiSourceDownloadService>();
        private ConcurrentDictionary<Guid, MultiSourceDownloadStatus> ActiveDownloads { get; } = new();

        /// <inheritdoc/>
        public async Task<ContentVerificationResult> FindVerifiedSourcesAsync(
            string filename,
            long fileSize,
            string excludeUsername = null,
            CancellationToken cancellationToken = default)
        {
            // Extract just the filename for searching
            var searchTerm = IOPath.GetFileNameWithoutExtension(filename);

            Log.Information("Searching for alternative sources: {SearchTerm}", searchTerm);

            // Search for the file
            var searchResults = new List<SearchResponse>();
            var searchOptions = new SearchOptions(
                filterResponses: true,
                minimumResponseFileCount: 1,
                responseLimit: 50);

            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(searchTerm),
                    responseHandler: (response) => searchResults.Add(response),
                    options: searchOptions,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Search failed: {Message}", ex.Message);
            }

            // Find exact matches (same filename, same size)
            var originalFilename = IOPath.GetFileName(filename);
            var candidates = new List<string>();

            foreach (var response in searchResults)
            {
                if (excludeUsername != null && response.Username.Equals(excludeUsername, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var file in response.Files)
                {
                    var responseFilename = IOPath.GetFileName(file.Filename);
                    if (responseFilename.Equals(originalFilename, StringComparison.OrdinalIgnoreCase) &&
                        file.Size == fileSize)
                    {
                        if (!candidates.Contains(response.Username))
                        {
                            candidates.Add(response.Username);
                        }
                    }
                }
            }

            Log.Information("Found {Count} candidate sources with exact match", candidates.Count);

            if (candidates.Count == 0)
            {
                return new ContentVerificationResult
                {
                    Filename = filename,
                    FileSize = fileSize,
                };
            }

            // Verify sources
            return await ContentVerification.VerifySourcesAsync(
                new ContentVerificationRequest
                {
                    Filename = filename,
                    FileSize = fileSize,
                    CandidateUsernames = candidates,
                },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<MultiSourceDownloadResult> DownloadAsync(
            MultiSourceDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = new MultiSourceDownloadResult
            {
                Id = request.Id,
                Filename = request.Filename,
                OutputPath = request.OutputPath,
            };

            var stopwatch = Stopwatch.StartNew();

            var status = new MultiSourceDownloadStatus
            {
                Id = request.Id,
                Filename = request.Filename,
                FileSize = request.FileSize,
                State = MultiSourceDownloadState.Downloading,
            };
            ActiveDownloads[request.Id] = status;

            try
            {
                if (request.Sources.Count == 0)
                {
                    result.Error = "No verified sources provided";
                    result.Success = false;
                    return result;
                }

                // Calculate chunks - use smaller chunks for more parallelism
                var chunkSize = request.ChunkSize > 0 ? request.ChunkSize : DefaultChunkSize;
                var chunks = CalculateChunksFixed(request.FileSize, chunkSize);
                status.TotalChunks = chunks.Count;

                Log.Information(
                    "SWARM DOWNLOAD: {Filename} ({Size} bytes) = {Chunks} chunks from {Sources} sources",
                    request.Filename,
                    request.FileSize,
                    chunks.Count,
                    request.Sources.Count);

                // Create temp directory for chunks
                var tempDir = IOPath.Combine(IOPath.GetTempPath(), "slskdn-multidownload", request.Id.ToString());
                IODirectory.CreateDirectory(tempDir);

                // SWARM MODE: Shared work queue
                var chunkQueue = new ConcurrentQueue<ChunkInfo>();
                foreach (var chunk in chunks)
                {
                    chunkQueue.Enqueue(new ChunkInfo
                    {
                        Index = chunk.Index,
                        StartOffset = chunk.StartOffset,
                        EndOffset = chunk.EndOffset,
                    });
                }

                var completedChunks = new ConcurrentDictionary<int, ChunkResult>();
                var sourceStats = new ConcurrentDictionary<string, int>(); // username -> chunks completed
                var failedUsers = new ConcurrentDictionary<string, bool>(); // username -> failed hard

                // Spawn worker for EACH source - they all grab from the shared queue
                var workerTasks = new List<Task>();

                foreach (var source in request.Sources)
                {
                    workerTasks.Add(Task.Run(async () =>
                    {
                        await RunSourceWorkerAsync(
                            source,
                            request.Filename,
                            request.FileSize,
                            chunkQueue,
                            completedChunks,
                            sourceStats,
                            failedUsers,
                            tempDir,
                            status,
                            cancellationToken);
                    }, cancellationToken));
                }

                // Wait for all workers (they exit when queue is empty or all chunks complete)
                await Task.WhenAll(workerTasks);

                // Check results after first pass
                var failedCount = chunks.Count - completedChunks.Count;
                Log.Information("[SWARM] First pass: {Completed}/{Total} chunks", completedChunks.Count, chunks.Count);

                // If chunks remain, retry with workers that SUCCEEDED
                var retryAttempt = 0;
                const int maxRetries = 5;

                while (failedCount > 0 && retryAttempt < maxRetries)
                {
                    retryAttempt++;
                    var successfulSources = sourceStats.Where(s => s.Value > 0 && !failedUsers.ContainsKey(s.Key))
                        .Select(s => s.Key).ToList();

                    // If we don't have enough proven sources, try other candidates (excluding failed ones)
                    // This prevents stalling on a single peer
                    if (successfulSources.Count < 3)
                    {
                        var candidates = request.Sources
                            .Where(s => !failedUsers.ContainsKey(s.Username))
                            .Select(s => s.Username)
                            .ToList();

                        if (candidates.Count > 0)
                        {
                            Log.Warning("[SWARM] Only {Count} proven sources. Retrying with {Candidates} candidates (excluding failed).",
                                successfulSources.Count, candidates.Count);
                            successfulSources = candidates;
                        }
                        else
                        {
                            // Desperation: Purge blacklist and retry everyone
                            Log.Warning("[SWARM] All sources failed/blacklisted. Purging blacklist and retrying everyone.");
                            failedUsers.Clear();
                            successfulSources = request.Sources.Select(s => s.Username).ToList();
                        }
                    }

                    if (successfulSources.Count == 0)
                    {
                        Log.Warning("[SWARM] No sources available to retry with");
                        break;
                    }

                    Log.Information("[SWARM] Retry {Attempt}/{Max}: {Missing} chunks remaining, using {Sources} sources",
                        retryAttempt, maxRetries, failedCount, successfulSources.Count);

                    // Re-enqueue missing chunks
                    foreach (var chunk in chunks)
                    {
                        if (!completedChunks.ContainsKey(chunk.Index))
                        {
                            chunkQueue.Enqueue(new ChunkInfo
                            {
                                Index = chunk.Index,
                                StartOffset = chunk.StartOffset,
                                EndOffset = chunk.EndOffset,
                            });
                        }
                    }

                    // Spawn workers only for selected sources
                    var retryTasks = new List<Task>();
                    foreach (var username in successfulSources)
                    {
                        var source = request.Sources.FirstOrDefault(s => s.Username == username);
                        if (source != null)
                        {
                            retryTasks.Add(Task.Run(async () =>
                            {
                                await RunSourceWorkerAsync(
                                    source,
                                    request.Filename,
                                    request.FileSize,
                                    chunkQueue,
                                    completedChunks,
                                    sourceStats,
                                    failedUsers,
                                    tempDir,
                                    status,
                                    cancellationToken);
                            }, cancellationToken));
                        }
                    }

                    await Task.WhenAll(retryTasks);
                    failedCount = chunks.Count - completedChunks.Count;
                    Log.Information("[SWARM] After retry {Attempt}: {Completed}/{Total} chunks",
                        retryAttempt, completedChunks.Count, chunks.Count);
                }

                result.Chunks = completedChunks.Values.ToList();
                result.SourcesUsed = sourceStats.Count(s => s.Value > 0);

                if (failedCount > 0)
                {
                    result.Error = $"{failedCount} chunks failed after {retryAttempt} retries";
                    result.Success = false;
                    status.State = MultiSourceDownloadState.Failed;

                    Log.Error("[SWARM] FAILED: {Failed}/{Total} chunks missing after all retries", failedCount, chunks.Count);
                    return result;
                }

                // Log source distribution
                Log.Information("[SWARM] SUCCESS! Chunk distribution:");
                foreach (var stat in sourceStats.OrderByDescending(s => s.Value))
                {
                    Log.Information("  {Username}: {Count} chunks", stat.Key, stat.Value);
                }

                // Assemble chunks
                status.State = MultiSourceDownloadState.Assembling;
                Log.Information("Assembling {Count} chunks into final file", chunks.Count);

                await AssembleChunksAsync(tempDir, chunks.Count, request.OutputPath, cancellationToken);

                // Verify final file
                status.State = MultiSourceDownloadState.VerifyingFinal;
                var finalHash = await ComputeFileHashAsync(request.OutputPath, cancellationToken);
                result.FinalHash = finalHash;

                if (request.ExpectedHash != null && !finalHash.Equals(request.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning(
                        "Final hash mismatch! Expected: {Expected}, Got: {Actual}",
                        request.ExpectedHash,
                        finalHash);
                    result.Error = "Final hash verification failed";
                    result.Success = false;
                    status.State = MultiSourceDownloadState.Failed;
                    return result;
                }

                // Cleanup temp files
                try
                {
                    IODirectory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                stopwatch.Stop();
                result.TotalTimeMs = stopwatch.ElapsedMilliseconds;
                result.BytesDownloaded = request.FileSize;
                result.Success = true;
                status.State = MultiSourceDownloadState.Completed;

                Log.Information(
                    "SWARM SUCCESS: {Filename} in {Time}ms ({Speed:F2} MB/s) from {Sources} sources",
                    request.Filename,
                    result.TotalTimeMs,
                    (request.FileSize / 1024.0 / 1024.0) / (result.TotalTimeMs / 1000.0),
                    result.SourcesUsed);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SWARM DOWNLOAD FAILED: {Message}", ex.Message);
                result.Error = ex.Message;
                result.Success = false;
                status.State = MultiSourceDownloadState.Failed;
                return result;
            }
            finally
            {
                ActiveDownloads.TryRemove(request.Id, out _);
            }
        }

        private async Task RunSourceWorkerAsync(
            VerifiedSource source,
            string filename,
            long fileSize,
            ConcurrentQueue<ChunkInfo> chunkQueue,
            ConcurrentDictionary<int, ChunkResult> completedChunks,
            ConcurrentDictionary<string, int> sourceStats,
            ConcurrentDictionary<string, bool> failedUsers,
            string tempDir,
            MultiSourceDownloadStatus status,
            CancellationToken cancellationToken)
        {
            var username = source.Username;
            var sourcePath = source.FullPath;
            sourceStats[username] = 0;
            var consecutiveFailures = 0;
            const int maxConsecutiveFailures = 3;

            status.IncrementActiveWorkers();
            Log.Information("[SWARM] Worker started: {Username}", username);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check if all chunks done
                    if (completedChunks.Count >= status.TotalChunks)
                    {
                        break;
                    }

                    // Try to grab a chunk from the queue
                    if (!chunkQueue.TryDequeue(out var chunk))
                    {
                        // Queue empty - wait a bit and check again (other workers might requeue)
                        await Task.Delay(100, cancellationToken);

                        // If still empty and all done, exit
                        if (chunkQueue.IsEmpty && completedChunks.Count >= status.TotalChunks)
                        {
                            break;
                        }

                        // Try again
                        continue;
                    }

                    // Skip if already completed by another worker
                    if (completedChunks.ContainsKey(chunk.Index))
                    {
                        continue;
                    }

                    var chunkPath = IOPath.Combine(tempDir, $"chunk_{chunk.Index:D4}.bin");

                    try
                    {
                        // Enforce hard timeout on chunk download to prevent hanging
                        using var chunkCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        chunkCts.CancelAfter(45000); // 45s max per chunk

                        var result = await DownloadChunkAsync(
                            username,
                            sourcePath,
                            fileSize,
                            chunk.StartOffset,
                            chunk.EndOffset,
                            chunkPath,
                            status,
                            chunkCts.Token);

                        if (result.Success)
                        {
                            completedChunks[chunk.Index] = result;
                            sourceStats.AddOrUpdate(username, 1, (_, count) => count + 1);
                            consecutiveFailures = 0; // Reset on success

                            Log.Information(
                                "[SWARM] ✓ {Username} chunk {Index} @ {Speed:F0} KB/s [{Completed}/{Total}]",
                                username,
                                chunk.Index,
                                result.SpeedBps / 1024.0,
                                completedChunks.Count,
                                status.TotalChunks);
                        }
                        else
                        {
                            // Don't count "Too slow" as a hard failure that kills the worker
                            // This keeps the worker alive to try other chunks (or retry later)
                            var isSpeedFailure = result.Error?.Contains("Too slow") == true;
                            if (!isSpeedFailure)
                            {
                                consecutiveFailures++;
                            }

                            Log.Warning("[SWARM] ✗ {Username} chunk {Index}: {Error} (fail {Fails}/{Max})",
                                username, chunk.Index, result.Error, consecutiveFailures, maxConsecutiveFailures);

                            // Put chunk back for another worker
                            chunkQueue.Enqueue(chunk);

                            // Only exit if too many consecutive HARD failures
                            if (consecutiveFailures >= maxConsecutiveFailures)
                            {
                                Log.Warning("[SWARM] {Username} giving up after {Fails} consecutive failures", username, consecutiveFailures);
                                failedUsers.TryAdd(username, true);
                                break;
                            }

                            // Delay before retry - longer for speed failures to let others grab it
                            await Task.Delay(isSpeedFailure ? 2000 : 500, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        chunkQueue.Enqueue(chunk);
                        
                        // If main token cancelled, exit
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        // Otherwise it's likely a speed cancellation from within DownloadChunkAsync
                        // Back off and continue
                        Log.Warning("[SWARM] {Username} dropped chunk {Index} (cancellation) - backing off", username, chunk.Index);
                        await Task.Delay(2000, cancellationToken);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;
                        Log.Warning("[SWARM] ✗ {Username} chunk {Index} exception: {Message} (fail {Fails}/{Max})",
                            username, chunk.Index, ex.Message, consecutiveFailures, maxConsecutiveFailures);

                        chunkQueue.Enqueue(chunk);

                        if (consecutiveFailures >= maxConsecutiveFailures)
                        {
                            Log.Warning("[SWARM] {Username} giving up after {Fails} consecutive failures", username, consecutiveFailures);
                            failedUsers.TryAdd(username, true);
                            break;
                        }

                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            finally
            {
                status.DecrementActiveWorkers();
                Log.Information("[SWARM] Worker finished: {Username} (Completed: {Count})", username, sourceStats.GetValueOrDefault(username, 0));
            }
        }

        private class ChunkInfo
        {
            public int Index { get; set; }
            public long StartOffset { get; set; }
            public long EndOffset { get; set; }
        }

        /// <inheritdoc/>
        public MultiSourceDownloadStatus GetStatus(Guid downloadId)
        {
            return ActiveDownloads.TryGetValue(downloadId, out var status) ? status : null;
        }

        private List<(int Index, long StartOffset, long EndOffset)> CalculateChunks(long fileSize, int sourceCount)
        {
            var chunks = new List<(int Index, long StartOffset, long EndOffset)>();

            // Use smaller chunks if we have more sources
            var chunkSize = Math.Max(DefaultChunkSize, fileSize / Math.Max(sourceCount * 2, 4));
            var offset = 0L;
            var index = 0;

            while (offset < fileSize)
            {
                var endOffset = Math.Min(offset + chunkSize, fileSize);
                chunks.Add((index, offset, endOffset));
                offset = endOffset;
                index++;
            }

            return chunks;
        }

        private List<(int Index, long StartOffset, long EndOffset)> CalculateChunksFixed(long fileSize, long chunkSize)
        {
            var chunks = new List<(int Index, long StartOffset, long EndOffset)>();
            var offset = 0L;
            var index = 0;

            while (offset < fileSize)
            {
                var endOffset = Math.Min(offset + chunkSize, fileSize);
                chunks.Add((index, offset, endOffset));
                offset = endOffset;
                index++;
            }

            return chunks;
        }

        private async Task<ChunkResult> DownloadChunkAsync(
            string username,
            string filename,
            long fileSize,
            long startOffset,
            long endOffset,
            string outputPath,
            MultiSourceDownloadStatus status,
            CancellationToken cancellationToken)
        {
            const int minSpeedBps = 5 * 1024;  // 5 KB/s minimum
            const int slowDurationMs = 15000;   // 15 seconds of slow = too slow

            var result = new ChunkResult
            {
                Username = username,
                StartOffset = startOffset,
                EndOffset = endOffset,
            };

            var stopwatch = Stopwatch.StartNew();
            status.IncrementActiveChunks();

            try
            {
                var chunkSize = endOffset - startOffset;

                Log.Debug(
                    "Downloading chunk from {Username}: {Start}-{End} ({Size} bytes of {FileSize})",
                    username,
                    startOffset,
                    endOffset,
                    chunkSize,
                    fileSize);

                // Soulseek requires size when startOffset > 0
                // We use a limited stream that cancels after receiving our chunk
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                var limitedStream = new LimitedWriteStream(fileStream, chunkSize, cts);

                // Speed monitor - cancel if too slow for too long
                var slowSince = (DateTime?)null;
                var lastBytes = 0L;
                var random = new Random();

                var speedMonitorTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested && !limitedStream.LimitReached)
                    {
                        // Randomize check interval to prevent simultaneous cancellations
                        await Task.Delay(2000 + random.Next(1000), cts.Token).ConfigureAwait(false);

                        var currentBytes = limitedStream.BytesWritten;
                        var bytesInInterval = currentBytes - lastBytes;
                        var speedBps = bytesInInterval / 2; // 2 second interval (approx)
                        lastBytes = currentBytes;

                        // Log live rate periodically (every 2s)
                        if (currentBytes > 0)
                        {
                            Log.Debug("[SWARM] {Username} rate: {Speed:F1} KB/s", username, speedBps / 1024.0);
                        }

                        if (speedBps < minSpeedBps && currentBytes > 0)
                        {
                            slowSince ??= DateTime.UtcNow;
                            var slowDuration = (DateTime.UtcNow - slowSince.Value).TotalMilliseconds;

                            if (slowDuration >= slowDurationMs)
                            {
                                // Only cycle out if we have other workers available
                                if (status.ActiveWorkers > 1)
                                {
                                    Log.Warning("[SWARM] {Username} too slow ({Speed:F1} KB/s for {Duration:F0}s) - cycling out",
                                        username, speedBps / 1024.0, slowDuration / 1000.0);
                                    result.Error = $"Too slow: {speedBps / 1024.0:F1} KB/s for {slowDuration / 1000.0:F0}s";
                                    cts.Cancel();
                                    return;
                                }
                                else
                                {
                                    Log.Warning("[SWARM] {Username} is slow ({Speed:F1} KB/s) but is the LAST WORKER - keeping alive",
                                        username, speedBps / 1024.0);
                                    slowSince = DateTime.UtcNow; // Reset timer to avoid log spam
                                }
                            }
                        }
                        else
                        {
                            slowSince = null; // Reset if speed recovered
                        }
                    }
                }, cts.Token);

                try
                {
                    // Pass the file size (required when startOffset > 0)
                    // The limited stream will cancel after we get our chunk
                    await Client.DownloadAsync(
                        username: username,
                        remoteFilename: filename,
                        outputStreamFactory: () => Task.FromResult<Stream>(limitedStream),
                        size: fileSize,
                        startOffset: startOffset,
                        cancellationToken: cts.Token,
                        options: new TransferOptions(
                            maximumLingerTime: 3000,
                            disposeOutputStreamOnCompletion: false));
                }
                catch (OperationCanceledException) when (limitedStream.LimitReached)
                {
                    // Expected - we cancelled after getting our chunk
                    Log.Debug("Chunk complete (cancelled remaining) from {Username}", username);
                }
                catch (OperationCanceledException) when (result.Error?.Contains("Too slow") == true)
                {
                    // Speed monitor cancelled us
                    return result;
                }

                // Stop speed monitor
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // Ignore
                }

                try
                {
                    await speedMonitorTask.ConfigureAwait(false);
                }
                catch
                {
                    // Ignore
                }

                stopwatch.Stop();
                result.BytesDownloaded = limitedStream.BytesWritten;
                result.TimeMs = stopwatch.ElapsedMilliseconds;
                result.Success = limitedStream.BytesWritten >= chunkSize;

                if (result.Success)
                {
                    status.AddBytesDownloaded(chunkSize);
                    status.IncrementCompletedChunks();

                    Log.Debug(
                        "Chunk complete from {Username}: {Size} bytes in {Time}ms ({Speed:F2} KB/s)",
                        username,
                        chunkSize,
                        result.TimeMs,
                        result.SpeedBps / 1024.0);
                }
                else
                {
                    result.Error = $"Only got {limitedStream.BytesWritten}/{chunkSize} bytes";
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.TimeMs = stopwatch.ElapsedMilliseconds;
                result.Error = ex.Message;
                result.Success = false;

                Log.Warning(ex, "Chunk download failed from {Username}: {Message}", username, ex.Message);
                return result;
            }
            finally
            {
                status.DecrementActiveChunks();
            }
        }

        private async Task AssembleChunksAsync(string tempDir, int chunkCount, string outputPath, CancellationToken cancellationToken)
        {
            var outputDir = IOPath.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                IODirectory.CreateDirectory(outputDir);
            }

            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

            for (int i = 0; i < chunkCount; i++)
            {
                var chunkPath = IOPath.Combine(tempDir, $"chunk_{i:D4}.bin");
                using var chunkStream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read);
                await chunkStream.CopyToAsync(outputStream, cancellationToken);
            }
        }

        private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
