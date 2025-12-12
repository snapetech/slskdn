using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soulseek;
using slskd.Transfers.MultiSource;
using slskd.Transfers.MultiSource.Scheduling;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using LimitedWriteStream = slskd.Transfers.MultiSource.LimitedWriteStream;

namespace slskd.Swarm;

/// <summary>
/// Background orchestrator for swarm downloads.
/// </summary>
public class SwarmDownloadOrchestrator : BackgroundService
{
    private readonly ILogger<SwarmDownloadOrchestrator> logger;
    private readonly IVerificationEngine verifier;
    private readonly IChunkScheduler chunkScheduler;
    private readonly ISoulseekClient soulseekClient;
    private readonly slskd.Mesh.IMeshDataPlane meshDataPlane;
    private readonly Channel<SwarmJob> jobs = Channel.CreateUnbounded<SwarmJob>();
    private readonly ConcurrentDictionary<string, SwarmJobStatus> activeJobs = new();

    public SwarmDownloadOrchestrator(
        ILogger<SwarmDownloadOrchestrator> logger,
        IVerificationEngine verifier,
        IChunkScheduler chunkScheduler,
        ISoulseekClient soulseekClient,
        slskd.Mesh.IMeshDataPlane meshDataPlane = null)
    {
        this.logger = logger;
        this.verifier = verifier;
        this.chunkScheduler = chunkScheduler;
        this.soulseekClient = soulseekClient;
        this.meshDataPlane = meshDataPlane;
    }

    public bool Enqueue(SwarmJob job)
    {
        logger.LogDebug("[SwarmOrchestrator] Enqueue {JobId} ({ContentId})", job.JobId, job.File.ContentId);
        return jobs.Writer.TryWrite(job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                logger.LogInformation("[SwarmOrchestrator] Start {JobId} ({ContentId})", job.JobId, job.File.ContentId);
                await ProcessJob(job, stoppingToken);
                logger.LogInformation("[SwarmOrchestrator] Completed {JobId}", job.JobId);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SwarmOrchestrator] Failed {JobId}: {Message}", job.JobId, ex.Message);
            }
        }
    }

    private async Task ProcessJob(SwarmJob job, CancellationToken ct)
    {
        logger.LogInformation("[SwarmOrchestrator] Processing job {JobId}: {ContentId} ({Size} bytes) from {SourceCount} sources",
            job.JobId, job.File.ContentId, job.File.SizeBytes, job.Sources.Count);

        if (job.Sources.Count == 0)
        {
            logger.LogWarning("[SwarmOrchestrator] Job {JobId} has no sources", job.JobId);
            return;
        }

        var status = new SwarmJobStatus
        {
            JobId = job.JobId,
            State = SwarmJobState.Downloading,
            TotalChunks = 0,
            CompletedChunks = 0,
        };
        activeJobs[job.JobId] = status;

        try
        {
            // Calculate chunks
            const int chunkSize = 512 * 1024; // 512KB chunks
            var chunks = CalculateChunks(job.File.SizeBytes, chunkSize);
            status.TotalChunks = chunks.Count;

            logger.LogInformation("[SwarmOrchestrator] Job {JobId}: {ChunkCount} chunks of {ChunkSize} bytes each",
                job.JobId, chunks.Count, chunkSize);

            // Create temp directory for chunks
            var tempDir = IOPath.Combine(IOPath.GetTempPath(), "slskdn-swarm", job.JobId);
            IODirectory.CreateDirectory(tempDir);

            // Convert SwarmSource to peer identifiers for chunk scheduler
            // Mesh-first approach: prefer overlay transport, fallback to Soulseek
            var availablePeers = job.Sources
                .OrderBy(s => s.Transport == "overlay" ? 0 : s.Transport == "soulseek" ? 1 : 2) // Mesh first
                .Select(s => new { s.MeshPeerId, s.SoulseekUsername, s.Transport })
                .ToList();

            if (availablePeers.Count == 0)
            {
                logger.LogWarning("[SwarmOrchestrator] Job {JobId}: No peers available", job.JobId);
                status.State = SwarmJobState.Failed;
                status.Error = "No peers available";
                return;
            }
            
            logger.LogInformation(
                "[SwarmOrchestrator] Job {JobId}: Using {OverlayCount} overlay peers, {SoulseekCount} Soulseek peers",
                job.JobId,
                availablePeers.Count(p => p.Transport == "overlay"),
                availablePeers.Count(p => p.Transport == "soulseek"));

            // Build peer ID list for scheduler (using mesh IDs)
            var peerIds = availablePeers.Select(p => p.MeshPeerId).ToList();

            // Shared work queue for chunks
            var chunkQueue = new ConcurrentQueue<ChunkInfo>();
            foreach (var chunk in chunks)
            {
                chunkQueue.Enqueue(chunk);
            }

            var completedChunks = new ConcurrentDictionary<int, ChunkResult>();
            var chunkAssignments = new ConcurrentDictionary<int, ChunkAssignment>();

            // Process chunks using chunk scheduler
            var downloadTasks = new List<Task>();

            while (!chunkQueue.IsEmpty || completedChunks.Count < chunks.Count)
            {
                if (chunkQueue.TryDequeue(out var chunk))
                {
                    // Get chunk assignment from scheduler
                    var assignment = await chunkScheduler.AssignChunkAsync(
                        new ChunkRequest
                        {
                            ChunkIndex = chunk.Index,
                            Size = chunk.EndOffset - chunk.StartOffset,
                        },
                        peerIds,
                        ct);

                    if (assignment.Success && !string.IsNullOrEmpty(assignment.AssignedPeer))
                    {
                        chunkAssignments[chunk.Index] = assignment;

                        // Download chunk from assigned peer
                        downloadTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var chunkResult = await DownloadChunkAsync(
                                    job,
                                    chunk,
                                    assignment.AssignedPeer,
                                    tempDir,
                                    ct);

                                if (chunkResult.Success)
                                {
                                    // Verify chunk
                                    var verified = await verifier.VerifyChunkAsync(
                                        job.File.ContentId,
                                        chunk.Index,
                                        chunkResult.Data,
                                        ct);

                                    if (verified)
                                    {
                                        completedChunks[chunk.Index] = chunkResult;
                                        var completed = Interlocked.Increment(ref status.CompletedChunks);
                                        logger.LogDebug("[SwarmOrchestrator] Job {JobId}: Chunk {ChunkIndex} completed and verified ({Completed}/{Total})",
                                            job.JobId, chunk.Index, completed, status.TotalChunks);
                                    }
                                    else
                                    {
                                        logger.LogWarning("[SwarmOrchestrator] Job {JobId}: Chunk {ChunkIndex} verification failed",
                                            job.JobId, chunk.Index);
                                        // Re-enqueue for retry
                                        chunkQueue.Enqueue(chunk);
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("[SwarmOrchestrator] Job {JobId}: Chunk {ChunkIndex} download failed: {Error}",
                                        job.JobId, chunk.Index, chunkResult.Error);
                                    // Re-enqueue for retry
                                    chunkQueue.Enqueue(chunk);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "[SwarmOrchestrator] Job {JobId}: Error processing chunk {ChunkIndex}",
                                    job.JobId, chunk.Index);
                                // Re-enqueue for retry
                                chunkQueue.Enqueue(chunk);
                            }
                        }, ct));
                    }
                    else
                    {
                        logger.LogWarning("[SwarmOrchestrator] Job {JobId}: Failed to assign chunk {ChunkIndex}: {Reason}",
                            job.JobId, chunk.Index, assignment.Reason);
                        // Re-enqueue for retry
                        chunkQueue.Enqueue(chunk);
                    }
                }

                // Limit concurrent downloads
                if (downloadTasks.Count >= availablePeers.Count * 2)
                {
                    await Task.WhenAny(downloadTasks);
                    downloadTasks.RemoveAll(t => t.IsCompleted);
                }

                // Check if we're done
                if (completedChunks.Count >= chunks.Count)
                {
                    break;
                }

                await Task.Delay(100, ct); // Small delay to prevent tight loop
            }

            // Wait for remaining downloads
            await Task.WhenAll(downloadTasks);

            // Check completion
            if (completedChunks.Count < chunks.Count)
            {
                logger.LogWarning("[SwarmOrchestrator] Job {JobId}: Incomplete - {Completed}/{Total} chunks",
                    job.JobId, completedChunks.Count, chunks.Count);
                status.State = SwarmJobState.Failed;
                status.Error = $"Only {completedChunks.Count}/{chunks.Count} chunks completed";
                return;
            }

            // Assemble final file
            var outputPath = IOPath.Combine(IOPath.GetTempPath(), "slskdn-swarm-output", $"{job.JobId}_{IOPath.GetFileName(job.File.ContentId)}");
            IODirectory.CreateDirectory(IOPath.GetDirectoryName(outputPath));

            await AssembleFileAsync(chunks, completedChunks, tempDir, outputPath, ct);

            status.State = SwarmJobState.Completed;
            status.OutputPath = outputPath;
            logger.LogInformation("[SwarmOrchestrator] Job {JobId}: Completed successfully - {OutputPath}",
                job.JobId, outputPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SwarmOrchestrator] Job {JobId}: Failed with exception", job.JobId);
            status.State = SwarmJobState.Failed;
            status.Error = ex.Message;
        }
        finally
        {
            activeJobs.TryRemove(job.JobId, out _);
        }
    }

    private List<ChunkInfo> CalculateChunks(long fileSize, int chunkSize)
    {
        var chunks = new List<ChunkInfo>();
        var chunkIndex = 0;

        for (long offset = 0; offset < fileSize; offset += chunkSize)
        {
            var endOffset = Math.Min(offset + chunkSize, fileSize);
            chunks.Add(new ChunkInfo
            {
                Index = chunkIndex++,
                StartOffset = offset,
                EndOffset = endOffset,
            });
        }

        return chunks;
    }

    private async Task<ChunkResult> DownloadChunkAsync(
        SwarmJob job,
        ChunkInfo chunk,
        string peerId,
        string tempDir,
        CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var chunkSize = chunk.EndOffset - chunk.StartOffset;
        var tempFile = IOPath.Combine(tempDir, $"chunk_{chunk.Index}.tmp");

        try
        {
            // Find the source for this peer (by mesh peer ID)
            var source = job.Sources.FirstOrDefault(s => s.MeshPeerId == peerId);
            if (source == null)
            {
                return new ChunkResult
                {
                    ChunkIndex = chunk.Index,
                    Success = false,
                    Error = $"Source not found for mesh peer {peerId}",
                };
            }

            // For Soulseek transport, use Soulseek client with LimitedWriteStream
            if (source.Transport == "soulseek")
            {
                // Soulseek requires a username - get it from the source
                var soulseekUsername = source.SoulseekUsername;
                if (string.IsNullOrEmpty(soulseekUsername))
                {
                    return new ChunkResult
                    {
                        ChunkIndex = chunk.Index,
                        Success = false,
                        Error = $"No Soulseek username for mesh peer {peerId}",
                    };
                }
                
                logger.LogDebug("[SwarmOrchestrator] Downloading chunk {ChunkIndex} from {Username} (mesh {MeshId}, offset {Start}-{End}, size {Size})",
                    chunk.Index, soulseekUsername, peerId, chunk.StartOffset, chunk.EndOffset, chunkSize);

                // Use LimitedWriteStream to download only the chunk range
                // Soulseek doesn't support range requests, but we can start at the offset
                // and limit the stream to only write the chunk size
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30s timeout per chunk

                using var fileStream = IOFile.Create(tempFile);
                var limitedStream = new LimitedWriteStream(fileStream, chunkSize, cts);

                try
                {
                    // Download from the start offset, limited stream will stop after chunkSize bytes
                    // Use ContentId as filename if source doesn't have a specific path
                    var filename = job.File.ContentId;
                    await soulseekClient.DownloadAsync(
                        username: soulseekUsername,
                        remoteFilename: filename,
                        outputStreamFactory: () => Task.FromResult<System.IO.Stream>(limitedStream),
                        size: job.File.SizeBytes,
                        startOffset: chunk.StartOffset,
                        cancellationToken: cts.Token,
                        options: new Soulseek.TransferOptions(
                            maximumLingerTime: 3000,
                            disposeOutputStreamOnCompletion: false));
                }
                catch (OperationCanceledException) when (limitedStream.LimitReached)
                {
                    // Expected - we cancelled after getting our chunk
                    logger.LogDebug("[SwarmOrchestrator] Chunk {ChunkIndex} complete (cancelled remaining) from {Username} (mesh {MeshId})",
                        chunk.Index, soulseekUsername, peerId);
                }

                stopwatch.Stop();

                var bytesDownloaded = limitedStream.BytesWritten;
                var success = bytesDownloaded >= chunkSize;

                if (success)
                {
                    logger.LogInformation("[SwarmOrchestrator] ✓ Chunk {ChunkIndex} from {Username} (mesh {MeshId}): {Size}KB in {Time}ms @ {Speed:F0}KB/s",
                        chunk.Index, soulseekUsername, peerId, chunkSize / 1024, stopwatch.ElapsedMilliseconds,
                        (chunkSize * 1000.0 / stopwatch.ElapsedMilliseconds) / 1024.0);

                    // Read chunk data into memory for assembly
                    var chunkData = await IOFile.ReadAllBytesAsync(tempFile, ct);
                    
                    return new ChunkResult
                    {
                        ChunkIndex = chunk.Index,
                        Success = true,
                        Data = chunkData,
                    };
                }
                else
                {
                    // Clean up partial file
                    try { IOFile.Delete(tempFile); } catch { }

                    return new ChunkResult
                    {
                        ChunkIndex = chunk.Index,
                        Success = false,
                        Error = $"Incomplete chunk: got {bytesDownloaded}/{chunkSize} bytes",
                    };
                }
            }
            else if (source.Transport == "mesh" || source.Transport == "overlay")
            {
                // For mesh/overlay transport, use mesh data plane
                if (meshDataPlane == null)
                {
                    logger.LogWarning("[SwarmOrchestrator] Mesh data plane not available");
                    return new ChunkResult
                    {
                        ChunkIndex = chunk.Index,
                        Success = false,
                        Error = "Mesh data plane not initialized",
                    };
                }
                
                try
                {
                    var meshPeerId = slskd.Mesh.Identity.MeshPeerId.Parse(source.MeshPeerId);
                    var filename = job.File.ContentId; // Use ContentId as filename
                    var meshChunkSize = chunk.EndOffset - chunk.StartOffset;
                    
                    var data = await meshDataPlane.DownloadChunkAsync(
                        meshPeerId,
                        filename,
                        chunk.StartOffset,
                        (int)meshChunkSize,
                        ct);
                    
                    return new ChunkResult
                    {
                        ChunkIndex = chunk.Index,
                        Success = true,
                        Data = data,
                    };
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[SwarmOrchestrator] Mesh chunk download failed from {MeshPeer}",
                        source.MeshPeerId);
                    return new ChunkResult
                    {
                        ChunkIndex = chunk.Index,
                        Success = false,
                        Error = ex.Message,
                    };
                }
            }
            else
            {
                return new ChunkResult
                {
                    ChunkIndex = chunk.Index,
                    Success = false,
                    Error = $"Unsupported transport: {source.Transport}",
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "[SwarmOrchestrator] Error downloading chunk {ChunkIndex} from {PeerId}",
                chunk.Index, peerId);

            // Clean up on error
            try { if (IOFile.Exists(tempFile)) IOFile.Delete(tempFile); } catch { }

            return new ChunkResult
            {
                ChunkIndex = chunk.Index,
                Success = false,
                Error = ex.Message,
            };
        }
    }

    private async Task AssembleFileAsync(
        List<ChunkInfo> chunks,
        ConcurrentDictionary<int, ChunkResult> completedChunks,
        string tempDir,
        string outputPath,
        CancellationToken ct)
    {
        using var outputStream = IOFile.Create(outputPath);

        foreach (var chunk in chunks.OrderBy(c => c.Index))
        {
            if (completedChunks.TryGetValue(chunk.Index, out var chunkResult) && chunkResult.Success && chunkResult.Data != null)
            {
                // Write chunk data directly from memory
                await outputStream.WriteAsync(chunkResult.Data, 0, chunkResult.Data.Length, ct);
            }
            else
            {
                throw new InvalidOperationException($"Missing chunk {chunk.Index}");
            }
        }

        await outputStream.FlushAsync(ct);
    }
}

/// <summary>
/// Status of a swarm job.
/// </summary>
public class SwarmJobStatus
{
    public string JobId { get; set; }
    public SwarmJobState State { get; set; }
    public int TotalChunks { get; set; }
    public int CompletedChunks; // Field, not property, for Interlocked operations
    public string OutputPath { get; set; }
    public string Error { get; set; }
}

/// <summary>
/// State of a swarm job.
/// </summary>
public enum SwarmJobState
{
    Pending,
    Downloading,
    Completed,
    Failed,
}

/// <summary>
/// Information about a chunk.
/// </summary>
public class ChunkInfo
{
    public int Index { get; set; }
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
}

/// <summary>
/// Result of downloading a chunk.
/// </summary>
public class ChunkResult
{
    public int ChunkIndex { get; set; }
    public bool Success { get; set; }
    public byte[] Data { get; set; }
    public string Error { get; set; }
}
