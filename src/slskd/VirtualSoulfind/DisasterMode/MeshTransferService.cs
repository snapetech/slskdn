using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Reactive.Subjects;
using slskd.VirtualSoulfind.ShadowIndex;

namespace slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Interface for mesh-only (overlay multi-swarm) transfers.
/// </summary>
public interface IMeshTransferService
{
    /// <summary>
    /// Start a mesh-only transfer.
    /// </summary>
    Task<string> StartTransferAsync(
        string peerId,
        string fileHash,
        long fileSize,
        string targetPath,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get transfer status.
    /// </summary>
    Task<MeshTransferStatus?> GetTransferStatusAsync(string transferId, CancellationToken ct = default);
    
    /// <summary>
    /// Cancel a transfer.
    /// </summary>
    Task CancelTransferAsync(string transferId, CancellationToken ct = default);
    
    /// <summary>
    /// Get all active transfers.
    /// </summary>
    Task<List<MeshTransferStatus>> GetActiveTransfersAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Subscribe to transfer progress updates.
    /// </summary>
    IObservable<TransferProgressUpdate> SubscribeToProgress(string transferId);
}

/// <summary>
/// Mesh transfer status.
/// </summary>
public class MeshTransferStatus
{
    public string TransferId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public MeshTransferState State { get; set; }
    public long BytesTransferred { get; set; }
    public double ProgressPercent => FileSize > 0 ? (double)BytesTransferred / FileSize * 100 : 0;
    public int ActivePeerCount { get; set; }
    public long TransferRateBps { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Mesh transfer state.
/// </summary>
public enum MeshTransferState
{
    Initializing,
    DiscoveringPeers,
    Transferring,
    Verifying,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Transfer progress update event.
/// </summary>
public class TransferProgressUpdate
{
    public string TransferId { get; set; } = string.Empty;
    public long BytesTransferred { get; set; }
    public int ActivePeerCount { get; set; }
    public long TransferRateBps { get; set; }
    public MeshTransferState State { get; set; }
}

/// <summary>
/// Mesh-only transfer service (overlay multi-swarm).
/// </summary>
public class MeshTransferService : IMeshTransferService
{
    private readonly ILogger<MeshTransferService> logger;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IScenePeerDiscovery scenePeers;
    private readonly ConcurrentDictionary<string, MeshTransferStatus> activeTransfers = new();
    private readonly ConcurrentDictionary<string, Subject<TransferProgressUpdate>> progressSubjects = new();

    public MeshTransferService(
        ILogger<MeshTransferService> logger,
        IShadowIndexQuery shadowIndex,
        IScenePeerDiscovery scenePeers)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
        this.scenePeers = scenePeers;
    }

    public async Task<string> StartTransferAsync(
        string peerId,
        string fileHash,
        long fileSize,
        string targetPath,
        CancellationToken ct)
    {
        var transferId = Ulid.NewUlid().ToString();

        logger.LogInformation("[VSF-MESH-TRANSFER] Starting mesh transfer {TransferId}: {FileHash} ({Size} bytes)",
            transferId, fileHash, fileSize);

        var status = new MeshTransferStatus
        {
            TransferId = transferId,
            PeerId = peerId,
            FileHash = fileHash,
            FileSize = fileSize,
            TargetPath = targetPath,
            State = MeshTransferState.Initializing,
            StartedAt = DateTimeOffset.UtcNow
        };

        activeTransfers[transferId] = status;
        progressSubjects[transferId] = new Subject<TransferProgressUpdate>();

        // Start transfer asynchronously
        _ = Task.Run(async () => await ExecuteTransferAsync(transferId, ct), ct);

        return transferId;
    }

    public Task<MeshTransferStatus?> GetTransferStatusAsync(string transferId, CancellationToken ct)
    {
        activeTransfers.TryGetValue(transferId, out var status);
        return Task.FromResult(status);
    }

    public Task CancelTransferAsync(string transferId, CancellationToken ct)
    {
        if (activeTransfers.TryGetValue(transferId, out var status))
        {
            logger.LogInformation("[VSF-MESH-TRANSFER] Cancelling transfer {TransferId}", transferId);
            status.State = MeshTransferState.Cancelled;
            PublishProgress(transferId, status);
        }

        return Task.CompletedTask;
    }

    public Task<List<MeshTransferStatus>> GetActiveTransfersAsync(CancellationToken ct)
    {
        var transfers = activeTransfers.Values
            .Where(t => t.State != MeshTransferState.Completed &&
                       t.State != MeshTransferState.Failed &&
                       t.State != MeshTransferState.Cancelled)
            .ToList();

        return Task.FromResult(transfers);
    }

    public IObservable<TransferProgressUpdate> SubscribeToProgress(string transferId)
    {
        if (!progressSubjects.TryGetValue(transferId, out var subject))
        {
            subject = new Subject<TransferProgressUpdate>();
            progressSubjects[transferId] = subject;
        }

        return subject;
    }

    private async Task ExecuteTransferAsync(string transferId, CancellationToken ct)
    {
        var status = activeTransfers[transferId];

        try
        {
            // Phase 1: Discover peers
            status.State = MeshTransferState.DiscoveringPeers;
            PublishProgress(transferId, status);

            var peers = await DiscoverPeersAsync(status.FileHash, ct);
            logger.LogInformation("[VSF-MESH-TRANSFER] {TransferId}: Discovered {PeerCount} peers",
                transferId, peers.Count);

            if (peers.Count == 0)
            {
                throw new Exception("No peers found for file");
            }

            status.ActivePeerCount = peers.Count;

            // Phase 2: Multi-swarm transfer
            status.State = MeshTransferState.Transferring;
            PublishProgress(transferId, status);

            await PerformMultiSwarmTransferAsync(transferId, status, peers, ct);

            // Phase 3: Verify integrity
            status.State = MeshTransferState.Verifying;
            PublishProgress(transferId, status);

            await VerifyFileIntegrityAsync(status, ct);

            // Phase 4: Complete
            status.State = MeshTransferState.Completed;
            status.CompletedAt = DateTimeOffset.UtcNow;
            PublishProgress(transferId, status);

            logger.LogInformation("[VSF-MESH-TRANSFER] {TransferId}: Transfer completed in {Duration}s",
                transferId, (status.CompletedAt.Value - status.StartedAt).TotalSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-MESH-TRANSFER] {TransferId}: Transfer failed: {Message}",
                transferId, ex.Message);

            status.State = MeshTransferState.Failed;
            status.ErrorMessage = ex.Message;
            PublishProgress(transferId, status);
        }
    }

    private async Task<List<string>> DiscoverPeersAsync(string fileHash, CancellationToken ct)
    {
        // Query shadow index for peers with this file
        // In a real implementation, this would query by file hash
        // For now, we'll use a placeholder

        await Task.Delay(500, ct); // Simulate DHT lookup

        var peers = new List<string> { "peer:vsf:abc123", "peer:vsf:def456" };
        return peers;
    }

    private async Task PerformMultiSwarmTransferAsync(
        string transferId,
        MeshTransferStatus status,
        List<string> peers,
        CancellationToken ct)
    {
        // Multi-swarm transfer: request chunks from multiple peers in parallel
        var chunkSize = 256 * 1024; // 256 KB chunks
        var totalChunks = (int)Math.Ceiling((double)status.FileSize / chunkSize);

        logger.LogDebug("[VSF-MESH-TRANSFER] {TransferId}: Transferring {ChunkCount} chunks",
            transferId, totalChunks);

        var startTime = DateTimeOffset.UtcNow;

        for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            if (status.State == MeshTransferState.Cancelled)
            {
                throw new OperationCanceledException("Transfer cancelled");
            }

            // Simulate chunk download
            await Task.Delay(50, ct); // Simulate network transfer

            var chunkBytes = Math.Min(chunkSize, status.FileSize - status.BytesTransferred);
            status.BytesTransferred += chunkBytes;

            // Calculate transfer rate
            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            if (elapsed > 0)
            {
                status.TransferRateBps = (long)(status.BytesTransferred / elapsed);
            }

            // Publish progress every 10 chunks
            if (chunkIndex % 10 == 0 || chunkIndex == totalChunks - 1)
            {
                PublishProgress(transferId, status);
            }
        }

        logger.LogInformation("[VSF-MESH-TRANSFER] {TransferId}: Transfer complete, writing to disk",
            transferId);

        // Simulate writing to disk
        await Task.Delay(200, ct);
    }

    private async Task VerifyFileIntegrityAsync(MeshTransferStatus status, CancellationToken ct)
    {
        // In a real implementation, this would verify file hash
        logger.LogDebug("[VSF-MESH-TRANSFER] {TransferId}: Verifying file integrity",
            status.TransferId);

        await Task.Delay(100, ct); // Simulate hash verification

        // TODO: Actual hash verification
        // if (computedHash != status.FileHash) throw new Exception("Hash mismatch");
    }

    private void PublishProgress(string transferId, MeshTransferStatus status)
    {
        if (progressSubjects.TryGetValue(transferId, out var subject))
        {
            subject.OnNext(new TransferProgressUpdate
            {
                TransferId = transferId,
                BytesTransferred = status.BytesTransferred,
                ActivePeerCount = status.ActivePeerCount,
                TransferRateBps = status.TransferRateBps,
                State = status.State
            });
        }
    }
}
