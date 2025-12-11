// <copyright file="MultiSourceDownloadService.cs" company="slskdn Team">
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

namespace slskd.Transfers.MultiSource;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soulseek;
using slskd;
using slskdOptions = slskd.Options;
    using slskd.HashDb;
    using slskd.HashDb.Models;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.AutoTagging;
    using slskd.Integrations.Chromaprint;
    using slskd.Audio;
    using slskd.Mesh;
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
    public const int DefaultChunkSize = 512 * 1024;  // 512KB - balance between overhead amortization and failure recovery

        private const double DefaultMinQualityImprovement = 0.1;
        private const double DefaultLocalQualityThreshold = 0.85;

    private readonly ILogger<MultiSourceDownloadService> _logger;
    private readonly ISoulseekClient _client;
    private readonly IContentVerificationService _contentVerification;
    private readonly IHashDbService? _hashDb;
    private readonly IMeshSyncService? _meshSync;
    private readonly ICanonicalStatsService? canonicalStatsService;
    private readonly IFingerprintExtractionService fingerprintExtractionService;
    private readonly IAcoustIdClient acoustIdClient;
    private readonly IAutoTaggingService autoTaggingService;
    private readonly IOptionsMonitor<slskdOptions> optionsMonitor;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MultiSourceDownloadService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="soulseekClient">The Soulseek client.</param>
    /// <param name="contentVerificationService">The content verification service.</param>
    /// <param name="hashDb">The hash database service (optional).</param>
    /// <param name="meshSync">The mesh sync service (optional).</param>
        public MultiSourceDownloadService(
            ILogger<MultiSourceDownloadService> logger,
            ISoulseekClient soulseekClient,
            IContentVerificationService contentVerificationService,
            IHashDbService? hashDb = null,
            IMeshSyncService? meshSync = null,
            IFingerprintExtractionService fingerprintExtractionService = null,
            IAcoustIdClient acoustIdClient = null,
            IAutoTaggingService autoTaggingService = null,
            IOptionsMonitor<slskdOptions> optionsMonitor = null,
            ICanonicalStatsService canonicalStatsService = null)
    {
        _logger = logger;
        _client = soulseekClient;
        _contentVerification = contentVerificationService;
        _hashDb = hashDb;
        _meshSync = meshSync;
            this.fingerprintExtractionService = fingerprintExtractionService;
            this.acoustIdClient = acoustIdClient;
            this.autoTaggingService = autoTaggingService;
            this.optionsMonitor = optionsMonitor;
            this.canonicalStatsService = canonicalStatsService;
    }

    /// <inheritdoc/>
    public ConcurrentDictionary<Guid, MultiSourceDownloadStatus> ActiveDownloads { get; } = new();

    /// <inheritdoc/>
    public async Task<List<VerifiedSource>> SelectCanonicalSourcesAsync(
        ContentVerificationResult verificationResult,
        CancellationToken cancellationToken = default)
    {
        var semanticSources = verificationResult.BestSemanticSources.ToList();
        var bestSources = verificationResult.BestSources.ToList();

        var recordingId = verificationResult.BestSemanticRecordingId;

        if (canonicalStatsService == null || string.IsNullOrWhiteSpace(recordingId))
        {
            return semanticSources.Count >= 2 ? semanticSources : bestSources;
        }

        var candidates = await canonicalStatsService.GetCanonicalVariantCandidatesAsync(recordingId, cancellationToken).ConfigureAwait(false);
        if (candidates == null || candidates.Count == 0)
        {
            return semanticSources.Count >= 2 ? semanticSources : bestSources;
        }

        // Prefer sources that match the canonical recording
        var canonicalSources = semanticSources
            .Where(s => string.Equals(s.MusicBrainzRecordingId, recordingId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (canonicalSources.Count >= 2)
        {
            return canonicalSources;
        }

        return semanticSources.Count >= 2 ? semanticSources : bestSources;
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldSkipDownloadAsync(
        string recordingId,
        AudioVariant proposedVariant,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordingId) || _hashDb == null)
        {
            return false;
        }

        var locals = await _hashDb.GetVariantsByRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);
        if (locals == null || locals.Count == 0)
        {
            return false;
        }

        var bestLocal = locals.OrderByDescending(v => v.QualityScore).First();
        var proposedScore = proposedVariant?.QualityScore ?? bestLocal.QualityScore;

        if (bestLocal.QualityScore >= DefaultLocalQualityThreshold &&
            (proposedScore - bestLocal.QualityScore) <= DefaultMinQualityImprovement)
        {
            _logger.LogInformation("[CANONICAL] Skipping download; local quality {Local:F2} vs proposed {Proposed:F2}", bestLocal.QualityScore, proposedScore);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<ContentVerificationResult> FindVerifiedSourcesAsync(
        string filename,
        long fileSize,
        string excludeUsername = null,
        CancellationToken cancellationToken = default)
    {
        // Extract just the filename for searching
        var searchTerm = IOPath.GetFileNameWithoutExtension(filename);

        _logger.LogInformation("Searching for alternative sources: {SearchTerm}", searchTerm);

        // Search for the file
        var searchResults = new List<SearchResponse>();
        var searchOptions = new SearchOptions(
            filterResponses: true,
            minimumResponseFileCount: 1,
            responseLimit: 50);

        try
        {
            await _client.SearchAsync(
                SearchQuery.FromText(searchTerm),
                responseHandler: (response) => searchResults.Add(response),
                options: searchOptions,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search failed: {Message}", ex.Message);
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

        _logger.LogInformation("Found {Count} candidate sources with exact match", candidates.Count);

        if (candidates.Count == 0)
        {
            return new ContentVerificationResult
            {
                Filename = filename,
                FileSize = fileSize,
            };
        }

        // Verify sources
        return await _contentVerification.VerifySourcesAsync(
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
            status.TargetMusicBrainzRecordingId = request.TargetMusicBrainzRecordingId;
            status.TargetFingerprint = request.TargetFingerprint;
            status.TargetSemanticKey = request.TargetSemanticKey;
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

                _logger.LogInformation(
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
                            chunks,
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
                _logger.LogInformation("[SWARM] First pass: {Completed}/{Total} chunks", completedChunks.Count, chunks.Count);

                // If chunks remain, keep retrying until complete or truly stuck
                var retryAttempt = 0;
                var stuckCount = 0;  // Track consecutive retries with no progress
                const int maxStuckRetries = 3;  // Give up after 3 retries with ZERO progress
                
                // Limit concurrency for retries to avoid flooding resources
                const int MaxConcurrentRetryWorkers = 10;
                using var workerSemaphore = new SemaphoreSlim(MaxConcurrentRetryWorkers);

                while (failedCount > 0 && stuckCount < maxStuckRetries)
                {
                    retryAttempt++;
                    var successfulSources = sourceStats
                        .Where(s => s.Value > 0 && !failedUsers.ContainsKey(s.Key) && !status.IsPeerInTimeout(s.Key))
                        .Select(s => s.Key).ToList();

                    // If we don't have enough proven sources, try other candidates (excluding failed ones)
                    // This prevents stalling on a single peer
                    if (successfulSources.Count < 3)
                    {
                        var candidates = request.Sources
                            .Where(s => !failedUsers.ContainsKey(s.Username) && !status.IsPeerInTimeout(s.Username))
                            .Select(s => s.Username)
                            .ToList();

                        if (candidates.Count > 0)
                        {
                            _logger.LogWarning("[SWARM] Only {Count} proven sources. Retrying with {Candidates} candidates (excluding failed/timed-out).",
                                successfulSources.Count, candidates.Count);
                            successfulSources = candidates;
                        }
                        else
                        {
                            // Desperation: Clear all timeouts and blacklist, retry everyone
                            _logger.LogWarning("[SWARM] All sources failed/timed-out. Clearing timeouts and retrying everyone.");
                            failedUsers.Clear();
                            status.PeerTimeouts.Clear();
                            successfulSources = request.Sources.Select(s => s.Username).ToList();
                        }
                    }

                    if (successfulSources.Count == 0)
                    {
                        _logger.LogWarning("[SWARM] No sources available to retry with");
                        break;
                    }

                    _logger.LogInformation("[SWARM] Retry {Attempt}: {Missing} chunks remaining, using {Sources} sources (stuck={Stuck}/{MaxStuck})",
                        retryAttempt, failedCount, successfulSources.Count, stuckCount, maxStuckRetries);

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
                                await workerSemaphore.WaitAsync(cancellationToken);
                                try
                                {
                                    await RunSourceWorkerAsync(
                                        source,
                                        request.Filename,
                                        request.FileSize,
                                        chunkQueue,
                                        chunks,
                                        completedChunks,
                                        sourceStats,
                                        failedUsers,
                                        tempDir,
                                        status,
                                        cancellationToken);
                                }
                                finally
                                {
                                    workerSemaphore.Release();
                                }
                            }, cancellationToken));
                        }
                    }

                    await Task.WhenAll(retryTasks);
                    var newFailedCount = chunks.Count - completedChunks.Count;
                    
                    // Track progress - if no chunks completed this round, we're stuck
                    if (newFailedCount >= failedCount)
                    {
                        stuckCount++;
                        _logger.LogWarning("[SWARM] Retry {Attempt} made NO progress ({Stuck}/{MaxStuck} stuck rounds)",
                            retryAttempt, stuckCount, maxStuckRetries);
                    }
                    else
                    {
                        stuckCount = 0;  // Reset - we made progress!
                        _logger.LogInformation("[SWARM] After retry {Attempt}: {Completed}/{Total} chunks (+{Progress})",
                            retryAttempt, completedChunks.Count, chunks.Count, failedCount - newFailedCount);
                    }
                    
                    failedCount = newFailedCount;
                }

                result.Chunks = completedChunks.Values.ToList();
                result.SourcesUsed = sourceStats.Count(s => s.Value > 0);

                if (failedCount > 0)
                {
                    result.Error = $"{failedCount} chunks failed after {retryAttempt} retries ({stuckCount} stuck rounds)";
                    result.Success = false;
                    status.State = MultiSourceDownloadState.Failed;

                    _logger.LogError("[SWARM] FAILED: {Failed}/{Total} chunks missing after all retries", failedCount, chunks.Count);
                    return result;
                }

                // Log source distribution
                _logger.LogInformation("[SWARM] SUCCESS! Chunk distribution:");
                foreach (var stat in sourceStats.OrderByDescending(s => s.Value))
                {
                    _logger.LogInformation("  {Username}: {Count} chunks", stat.Key, stat.Value);
                }

                // Assemble chunks
                status.State = MultiSourceDownloadState.Assembling;
                _logger.LogInformation("Assembling {Count} chunks into final file", chunks.Count);

                await AssembleChunksAsync(tempDir, chunks.Count, request.OutputPath, cancellationToken);

                // Verify final file
                status.State = MultiSourceDownloadState.VerifyingFinal;
                var finalHash = await ComputeFileHashAsync(request.OutputPath, cancellationToken);
                result.FinalHash = finalHash;

                if (request.ExpectedHash != null && !finalHash.Equals(request.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Final hash mismatch! Expected: {Expected}, Got: {Actual}",
                        request.ExpectedHash,
                        finalHash);
                    result.Error = "Final hash verification failed";
                    result.Success = false;
                    status.State = MultiSourceDownloadState.Failed;
                    return result;
                }

                var verification = await VerifyFinalFileAsync(
                    request.OutputPath,
                    request.TargetFingerprint,
                    request.TargetSemanticKey,
                    request.TargetMusicBrainzRecordingId,
                    request.FileSize,
                    status,
                    cancellationToken);
                result.Fingerprint = verification.Fingerprint;
                result.FingerprintVerified = verification.Verified;
                result.ResolvedRecordingId = verification.ResolvedRecordingId;
                status.Fingerprint = verification.Fingerprint;
                status.FingerprintVerified = verification.Verified;
                status.ResolvedRecordingId = verification.ResolvedRecordingId;

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

                _logger.LogInformation(
                    "SWARM SUCCESS: {Filename} in {Time}ms ({Speed:F2} MB/s) from {Sources} sources",
                    request.Filename,
                    result.TotalTimeMs,
                    (request.FileSize / 1024.0 / 1024.0) / (result.TotalTimeMs / 1000.0),
                    result.SourcesUsed);

                // Phase 5 Integration: Store hash after successful download
                await PublishDownloadedHashAsync(request.Filename, request.FileSize, finalHash, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SWARM DOWNLOAD FAILED: {Message}", ex.Message);
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
            List<(int Index, long StartOffset, long EndOffset)> allChunks,
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
            _logger.LogInformation("[SWARM] Worker started: {Username}", username);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check if all chunks done
                    if (completedChunks.Count >= status.TotalChunks)
                    {
                        break;
                    }

                    // Check if this peer is in timeout (was too slow recently)
                    if (status.IsPeerInTimeout(username))
                    {
                        _logger.LogDebug("[SWARM] {Username} is in timeout, waiting...", username);
                        await Task.Delay(5000, cancellationToken); // Check again in 5s
                        continue;
                    }

                    // Try to grab a chunk from the queue
                    if (!chunkQueue.TryDequeue(out var chunk))
                    {
                        // Queue empty - check if we're done or should wait
                        if (completedChunks.Count >= status.TotalChunks)
                        {
                            break; // All done!
                        }

                        // Find any incomplete chunk (speculative execution / work stealing)
                        // This allows fast workers to re-attempt chunks that slow workers are struggling with
                        var incompleteChunkData = allChunks
                            .Where(c => !completedChunks.ContainsKey(c.Index))
                            .FirstOrDefault();

                        if (incompleteChunkData.Index >= 0 || incompleteChunkData.EndOffset > 0)
                        {
                            chunk = new ChunkInfo
                            {
                                Index = incompleteChunkData.Index,
                                StartOffset = incompleteChunkData.StartOffset,
                                EndOffset = incompleteChunkData.EndOffset,
                            };
                            _logger.LogDebug("[SWARM] {Username} stealing chunk {Index} (speculative)", username, chunk.Index);
                        }
                        else
                        {
                            // Wait a bit and check again
                            await Task.Delay(100, cancellationToken);
                            continue;
                        }
                    }

                    // Skip if already completed by another worker
                    if (completedChunks.ContainsKey(chunk.Index))
                    {
                        continue;
                    }

                    // Use unique temp file per worker to avoid race conditions
                    // Then atomically move to final path only if we're first to complete
                    var workerTempPath = IOPath.Combine(tempDir, $"chunk_{chunk.Index:D4}_{username.GetHashCode():X8}.tmp");
                    var chunkPath = IOPath.Combine(tempDir, $"chunk_{chunk.Index:D4}.bin");

                    try
                    {
                        // AGGRESSIVE timeout - 10s max per chunk to prevent stragglers
                        // Fast peers do 256KB chunks in 1-5 seconds; 10s is plenty
                        using var chunkCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        chunkCts.CancelAfter(10000); // 10s max per chunk

                        var result = await DownloadChunkAsync(
                            username,
                            sourcePath,
                            fileSize,
                            chunk.StartOffset,
                            chunk.EndOffset,
                            workerTempPath,  // Write to worker-specific temp file
                            status,
                            chunkCts.Token);

                        result.MusicBrainzRecordingId = source.MusicBrainzRecordingId ?? status.TargetMusicBrainzRecordingId;
                        result.Fingerprint = source.AudioFingerprint ?? status.TargetFingerprint;

                        if (result.Success)
                        {
                            // Atomic: Only move to final path if we're first to complete this chunk
                            // TryAdd returns false if another worker already completed it
                            if (completedChunks.TryAdd(chunk.Index, result))
                            {
                                // We won the race - move our file to final path
                                if (System.IO.File.Exists(workerTempPath))
                                {
                                    System.IO.File.Move(workerTempPath, chunkPath, overwrite: true);
                                }
                            }
                            else
                            {
                                // Another worker won - delete our temp file
                                if (System.IO.File.Exists(workerTempPath))
                                {
                                    System.IO.File.Delete(workerTempPath);
                                }
                                _logger.LogDebug("[SWARM] {Username} completed chunk {Index} but another worker won the race", username, chunk.Index);
                                continue;
                            }
                            sourceStats.AddOrUpdate(username, 1, (_, count) => count + 1);
                            consecutiveFailures = 0; // Reset on success

                            _logger.LogInformation(
                                "[SWARM] ✓ {Username} chunk {Index} @ {Speed:F0} KB/s [{Completed}/{Total}]",
                                username,
                                chunk.Index,
                                result.SpeedBps / 1024.0,
                                completedChunks.Count,
                                status.TotalChunks);
                        }
                        else
                        {
                            // Check for rejection (peer doesn't support partial downloads)
                            var isRejection = result.Error?.Contains("reported as failed") == true ||
                                              result.Error?.Contains("rejected") == true;

                            // Immediately blacklist peers who reject - they won't support ANY chunks
                            if (isRejection)
                            {
                                _logger.LogWarning("[SWARM] {Username} rejected partial download - blacklisting", username);
                                failedUsers.TryAdd(username, true);
                                chunkQueue.Enqueue(chunk);
                                break; // Exit this worker immediately
                            }

                            // Don't count "Too slow" as a hard failure that kills the worker
                            var isSpeedFailure = result.Error?.Contains("Too slow") == true;
                            if (!isSpeedFailure)
                            {
                                consecutiveFailures++;
                            }

                            _logger.LogWarning("[SWARM] ✗ {Username} chunk {Index}: {Error} (fail {Fails}/{Max})",
                                username, chunk.Index, result.Error, consecutiveFailures, maxConsecutiveFailures);

                            // Put chunk back for another worker
                            chunkQueue.Enqueue(chunk);

                            // Only exit if too many consecutive HARD failures
                            if (consecutiveFailures >= maxConsecutiveFailures)
                            {
                                _logger.LogWarning("[SWARM] {Username} giving up after {Fails} consecutive failures", username, consecutiveFailures);
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
                        _logger.LogWarning("[SWARM] {Username} dropped chunk {Index} (cancellation) - backing off", username, chunk.Index);
                        await Task.Delay(2000, cancellationToken);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;
                        _logger.LogWarning("[SWARM] ✗ {Username} chunk {Index} exception: {Message} (fail {Fails}/{Max})",
                            username, chunk.Index, ex.Message, consecutiveFailures, maxConsecutiveFailures);

                        chunkQueue.Enqueue(chunk);

                        if (consecutiveFailures >= maxConsecutiveFailures)
                        {
                            _logger.LogWarning("[SWARM] {Username} giving up after {Fails} consecutive failures", username, consecutiveFailures);
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
                _logger.LogInformation("[SWARM] Worker finished: {Username} (Completed: {Count})", username, sourceStats.GetValueOrDefault(username, 0));
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
            const int absoluteMinSpeedBps = 5 * 1024;  // 5 KB/s absolute floor
            const double minSpeedPercent = 0.15;       // 15% of best speed
            const int slowDurationMs = 8000;           // 8 seconds of slow = too slow (512KB chunks)
            const int peerTimeoutSeconds = 20;         // Timeout duration for slow peers

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

                _logger.LogDebug(
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

                // Timing metrics
                var firstByteTime = (long?)null;  // Time to first byte (connection overhead)
                var transferStartTime = (long?)null;  // When actual transfer started
                
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

                        // Track time to first byte
                        if (currentBytes > 0 && firstByteTime == null)
                        {
                            firstByteTime = stopwatch.ElapsedMilliseconds;
                            transferStartTime = stopwatch.ElapsedMilliseconds;
                        }

                        // Update best speed if this is faster
                        if (speedBps > 0)
                        {
                            status.UpdateBestSpeed(speedBps);
                        }

                        // Dynamic threshold: 15% of best speed, minimum 5 KB/s
                        var dynamicMinSpeed = Math.Max(absoluteMinSpeedBps, (long)(status.BestSpeedBps * minSpeedPercent));

                        // Log live rate periodically (every 2s)
                        if (currentBytes > 0)
                        {
                            _logger.LogDebug("[SWARM] {Username} rate: {Speed:F1} KB/s (threshold: {Threshold:F1} KB/s)",
                                username, speedBps / 1024.0, dynamicMinSpeed / 1024.0);
                        }

                        if (speedBps < dynamicMinSpeed && currentBytes > 0)
                        {
                            slowSince ??= DateTime.UtcNow;
                            var slowDuration = (DateTime.UtcNow - slowSince.Value).TotalMilliseconds;

                            if (slowDuration >= slowDurationMs)
                            {
                                // Only cycle out if we have other workers available
                                if (status.ActiveWorkers > 1)
                                {
                                    _logger.LogWarning("[SWARM] {Username} too slow ({Speed:F1} KB/s < {Threshold:F1} KB/s for {Duration:F0}s) - timeout {Timeout}s",
                                        username, speedBps / 1024.0, dynamicMinSpeed / 1024.0, slowDuration / 1000.0, peerTimeoutSeconds);
                                    result.Error = $"Too slow: {speedBps / 1024.0:F1} KB/s for {slowDuration / 1000.0:F0}s";
                                    // Set timeout instead of blacklist - peer can retry later
                                    status.SetPeerTimeout(username, TimeSpan.FromSeconds(peerTimeoutSeconds));
                                    cts.Cancel();
                                    return;
                                }
                                else
                                {
                                    _logger.LogWarning("[SWARM] {Username} is slow ({Speed:F1} KB/s) but is the LAST WORKER - keeping alive",
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
                    await _client.DownloadAsync(
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
                    _logger.LogDebug("Chunk complete (cancelled remaining) from {Username}", username);
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
                var totalMs = stopwatch.ElapsedMilliseconds;
                var ttfb = firstByteTime ?? totalMs;  // If no bytes, ttfb = total time
                var transferMs = transferStartTime.HasValue ? (totalMs - transferStartTime.Value) : 0;
                
                result.BytesDownloaded = limitedStream.BytesWritten;
                result.TimeMs = totalMs;
                result.TimeToFirstByteMs = ttfb;
                result.TransferTimeMs = transferMs;
                result.Success = limitedStream.BytesWritten >= chunkSize;

                if (result.Success)
                {
                    status.AddBytesDownloaded(chunkSize);
                    status.IncrementCompletedChunks();

                    // Detailed timing log
                    _logger.LogInformation(
                        "[CHUNK] {Username}: {Size}KB in {Total}ms | TTFB:{TTFB}ms Transfer:{Transfer}ms | Overhead:{Overhead:F0}% | Speed:{Speed:F0}KB/s (raw:{RawSpeed:F0}KB/s)",
                        username,
                        chunkSize / 1024,
                        totalMs,
                        ttfb,
                        transferMs,
                        result.OverheadPercent,
                        result.SpeedBps / 1024.0,
                        result.TransferSpeedBps / 1024.0);
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

                _logger.LogWarning(ex, "Chunk download failed from {Username}: {Message}", username, ex.Message);
                return result;
            }
            finally
            {
                status.DecrementActiveChunks();
            }
        }

        /// <summary>
        ///     Publishes the hash of a successfully downloaded file to the hash database and mesh.
        /// </summary>
        private async Task PublishDownloadedHashAsync(string filename, long fileSize, string hash, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return;
            }

            try
            {
                // Store in local hash database
                if (_hashDb != null)
                {
                    await _hashDb.StoreHashFromVerificationAsync(filename, fileSize, hash, cancellationToken: cancellationToken);
                    _logger.LogDebug("[HASHDB] Stored downloaded file hash: {Filename} -> {Hash}", IOPath.GetFileName(filename), hash.Substring(0, 16) + "...");
                }

                // Publish to mesh for other slskdn clients
                if (_meshSync != null)
                {
                    var flacKey = HashDbEntry.GenerateFlacKey(filename, fileSize);
                    await _meshSync.PublishHashAsync(flacKey, hash, fileSize, cancellationToken: cancellationToken);
                    _logger.LogDebug("[MESH] Published hash to mesh: {Key} -> {Hash}", flacKey, hash.Substring(0, 16) + "...");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HASHDB] Error publishing hash for {Filename}", filename);
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

        private async Task<FingerprintVerificationResult> VerifyFinalFileAsync(
            string filePath,
            string targetFingerprint,
            string targetSemanticKey,
            string targetRecordingId,
            long fileSize,
            MultiSourceDownloadStatus status,
            CancellationToken cancellationToken)
        {
            if (fingerprintExtractionService == null)
            {
                return new FingerprintVerificationResult(null, false, null);
            }

            try
            {
                var fingerprint = await fingerprintExtractionService.ExtractFingerprintAsync(filePath, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(fingerprint) && _hashDb != null)
                {
                    await _hashDb.UpdateHashFingerprintAsync(HashDbEntry.GenerateFlacKey(status.Filename ?? filePath, fileSize), fingerprint, cancellationToken).ConfigureAwait(false);
                }

                var resolvedRecordingId = (string)null;
                var verified = false;

                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    if (!string.IsNullOrWhiteSpace(targetFingerprint) && fingerprint.Equals(targetFingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        verified = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(targetSemanticKey) && !string.IsNullOrWhiteSpace(status.TargetSemanticKey) && targetSemanticKey.Equals(status.TargetSemanticKey, StringComparison.OrdinalIgnoreCase))
                    {
                        verified = true;
                    }
                }

                if (acoustIdClient != null && optionsMonitor != null && !string.IsNullOrWhiteSpace(fingerprint))
                {
                    var chromaOptions = optionsMonitor.CurrentValue.Integration.Chromaprint;
                    var result = await acoustIdClient.LookupAsync(fingerprint, chromaOptions.SampleRate, chromaOptions.DurationSeconds, cancellationToken).ConfigureAwait(false);
                    resolvedRecordingId = result?.Recordings?.FirstOrDefault()?.Id;

                    if (!verified && !string.IsNullOrWhiteSpace(resolvedRecordingId))
                    {
                        if (!string.IsNullOrWhiteSpace(targetRecordingId) && targetRecordingId.Equals(resolvedRecordingId, StringComparison.OrdinalIgnoreCase))
                        {
                            verified = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(targetSemanticKey) && !string.IsNullOrWhiteSpace(status.TargetSemanticKey) && targetSemanticKey.Equals(status.TargetSemanticKey, StringComparison.OrdinalIgnoreCase))
                        {
                            verified = true;
                        }
                    }
                }

                return new FingerprintVerificationResult(fingerprint, verified, resolvedRecordingId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AUTO-TAGGING] Final fingerprint verification failed for {File}", filePath);
                return new FingerprintVerificationResult(null, false, null);
            }
        }

        private sealed record FingerprintVerificationResult(string Fingerprint, bool Verified, string ResolvedRecordingId);
    }

