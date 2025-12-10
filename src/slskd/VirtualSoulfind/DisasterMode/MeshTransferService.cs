namespace slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Interface for mesh-only transfers (disaster mode).
/// </summary>
public interface IMeshTransferService
{
    /// <summary>
    /// Download file from mesh peer (overlay multi-swarm only).
    /// </summary>
    Task<string> DownloadAsync(
        string peerId,
        string mbRecordingId,
        string targetPath,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get transfer status.
    /// </summary>
    Task<MeshTransferStatus?> GetTransferStatusAsync(
        string transferId,
        CancellationToken ct = default);
}

/// <summary>
/// Mesh transfer status.
/// </summary>
public class MeshTransferStatus
{
    public string TransferId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string MbRecordingId { get; set; } = string.Empty;
    public TransferState State { get; set; }
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double Progress => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes : 0;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// Transfer state.
/// </summary>
public enum TransferState
{
    Pending,
    Connecting,
    Downloading,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Mesh-only transfer service (overlay multi-swarm only).
/// </summary>
public class MeshTransferService : IMeshTransferService
{
    private readonly ILogger<MeshTransferService> logger;

    public MeshTransferService(ILogger<MeshTransferService> logger)
    {
        this.logger = logger;
    }

    public async Task<string> DownloadAsync(
        string peerId,
        string mbRecordingId,
        string targetPath,
        CancellationToken ct)
    {
        logger.LogInformation("[VSF-MESH-TRANSFER] Starting mesh transfer: {MBID} from {PeerId}",
            mbRecordingId, peerId);

        var transferId = Ulid.NewUlid().ToString();

        // TODO: Implement actual overlay multi-swarm transfer
        // For now, this is a stub

        logger.LogDebug("[VSF-MESH-TRANSFER] Transfer {TransferId} queued", transferId);

        await Task.CompletedTask;
        return transferId;
    }

    public async Task<MeshTransferStatus?> GetTransferStatusAsync(
        string transferId,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-MESH-TRANSFER] Getting status for transfer {TransferId}", transferId);

        // TODO: Implement actual status tracking
        await Task.CompletedTask;
        return null;
    }
}
