// <copyright file="TransferProgressProxy.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Bridge;

using slskd.VirtualSoulfind.DisasterMode;
using System.Collections.Concurrent;

/// <summary>
/// Interface for proxying mesh transfer progress to legacy clients.
/// </summary>
public interface ITransferProgressProxy
{
    /// <summary>
    /// Start proxying mesh transfer progress.
    /// </summary>
    Task<string> StartProxyAsync(
        string meshTransferId,
        string legacyClientId,
        CancellationToken ct = default);

    /// <summary>
    /// Get legacy transfer progress.
    /// </summary>
    Task<LegacyTransferProgress?> GetLegacyProgressAsync(
        string proxyId,
        CancellationToken ct = default);

    /// <summary>
    /// Stop proxying.
    /// </summary>
    Task StopProxyAsync(string proxyId, CancellationToken ct = default);
}

/// <summary>
/// Legacy transfer progress (Soulseek protocol format).
/// </summary>
public class LegacyTransferProgress
{
    public string ProxyId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public long BytesTransferred { get; set; }
    public long FileSize { get; set; }
    public int PercentComplete { get; set; }
    public long AverageSpeed { get; set; }
    public string State { get; set; } = string.Empty;
    public int QueuePosition { get; set; } = 0;
}

/// <summary>
/// Transfer progress proxy for legacy clients.
/// </summary>
public class TransferProgressProxy : ITransferProgressProxy
{
    private readonly ILogger<TransferProgressProxy> logger;
    private readonly IMeshTransferService meshTransfer;
    private readonly IPeerIdAnonymizer peerAnonymizer;
    private readonly IFilenameGenerator filenameGenerator;
    private readonly ConcurrentDictionary<string, ProxySession> activeSessions = new();

    public TransferProgressProxy(
        ILogger<TransferProgressProxy> logger,
        IMeshTransferService meshTransfer,
        IPeerIdAnonymizer peerAnonymizer,
        IFilenameGenerator filenameGenerator)
    {
        this.logger = logger;
        this.meshTransfer = meshTransfer;
        this.peerAnonymizer = peerAnonymizer;
        this.filenameGenerator = filenameGenerator;
    }

    public Task<string> StartProxyAsync(
        string meshTransferId,
        string legacyClientId,
        CancellationToken ct)
    {
        var proxyId = Ulid.NewUlid().ToString();

        logger.LogInformation("[VSF-BRIDGE-PROXY] Starting proxy {ProxyId} for mesh transfer {MeshId}",
            proxyId, meshTransferId);

        var session = new ProxySession
        {
            ProxyId = proxyId,
            MeshTransferId = meshTransferId,
            LegacyClientId = legacyClientId,
            StartedAt = DateTimeOffset.UtcNow
        };

        activeSessions[proxyId] = session;

        // Subscribe to mesh transfer progress
        var subscription = meshTransfer.SubscribeToProgress(meshTransferId)
            .Subscribe(
                update => OnMeshProgressUpdate(proxyId, update),
                ex => logger.LogError(ex, "[VSF-BRIDGE-PROXY] Proxy {ProxyId} error", proxyId),
                () => logger.LogDebug("[VSF-BRIDGE-PROXY] Proxy {ProxyId} completed", proxyId));

        session.Subscription = subscription;

        return Task.FromResult(proxyId);
    }

    public async Task<LegacyTransferProgress?> GetLegacyProgressAsync(
        string proxyId,
        CancellationToken ct)
    {
        if (!activeSessions.TryGetValue(proxyId, out var session))
        {
            return null;
        }

        // Get current mesh transfer status
        var meshStatus = await meshTransfer.GetTransferStatusAsync(session.MeshTransferId, ct);
        if (meshStatus == null)
        {
            return session.LastProgress;
        }

        // Convert to legacy format
        var username = await peerAnonymizer.GetAnonymizedUsernameAsync(meshStatus.PeerId, ct);
        var filename = session.CachedFilename;
        if (string.IsNullOrWhiteSpace(filename))
        {
            filename = System.IO.Path.GetFileName(meshStatus.TargetPath);
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = session.MeshTransferId;
            }

            session.CachedFilename = filename;
        }

        var legacyProgress = new LegacyTransferProgress
        {
            ProxyId = proxyId,
            Username = username,
            Filename = filename,
            BytesTransferred = meshStatus.BytesTransferred,
            FileSize = meshStatus.FileSize > 0
                ? meshStatus.FileSize
                : session.LastProgress?.FileSize ?? 0,
            PercentComplete = (int)Math.Clamp(
                meshStatus.FileSize > 0
                    ? meshStatus.ProgressPercent
                    : session.LastProgress?.FileSize > 0
                        ? (meshStatus.BytesTransferred * 100.0) / session.LastProgress.FileSize
                        : meshStatus.ProgressPercent,
                0,
                100),
            AverageSpeed = meshStatus.TransferRateBps,
            State = MapMeshStateToLegacy(meshStatus.State),
            QueuePosition = 0 // No queue in mesh transfers
        };

        session.LastProgress = legacyProgress;

        return legacyProgress;
    }

    public Task StopProxyAsync(string proxyId, CancellationToken ct)
    {
        if (activeSessions.TryRemove(proxyId, out var session))
        {
            logger.LogInformation("[VSF-BRIDGE-PROXY] Stopping proxy {ProxyId}", proxyId);
            session.Subscription?.Dispose();
        }

        return Task.CompletedTask;
    }

    private void OnMeshProgressUpdate(string proxyId, TransferProgressUpdate update)
    {
        if (!activeSessions.TryGetValue(proxyId, out var session))
        {
            return;
        }

        var fileSize = session.LastProgress?.FileSize ?? 0L;
        var percent = fileSize > 0
            ? (int)(update.BytesTransferred * 100.0 / fileSize)
            : 0;

        logger.LogDebug("[VSF-BRIDGE-PROXY] {ProxyId}: {Percent}% ({Bytes}/{Total}) @ {Rate} Bps",
            proxyId,
            percent,
            update.BytesTransferred,
            session.LastProgress?.FileSize ?? 0,
            update.TransferRateBps);

        // Legacy Soulfind callbacks are unavailable in this bridge; API polling remains the supported path.
    }

    private string MapMeshStateToLegacy(MeshTransferState meshState)
    {
        return meshState switch
        {
            MeshTransferState.Initializing => "Queued",
            MeshTransferState.DiscoveringPeers => "Connecting",
            MeshTransferState.Transferring => "Downloading",
            MeshTransferState.Verifying => "Downloading",
            MeshTransferState.Completed => "Complete",
            MeshTransferState.Failed => "Errored",
            MeshTransferState.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }

    private class ProxySession
    {
        public string ProxyId { get; set; } = string.Empty;
        public string MeshTransferId { get; set; } = string.Empty;
        public string LegacyClientId { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public IDisposable? Subscription { get; set; }
        public LegacyTransferProgress? LastProgress { get; set; }
        public string? CachedFilename { get; set; }
    }
}
