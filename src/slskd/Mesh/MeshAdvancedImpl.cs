using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

namespace slskd.Mesh;

/// <summary>
/// Advanced mesh operations (placeholder implementation).
/// </summary>
public class MeshAdvanced : IMeshAdvanced
{
    private readonly ILogger<MeshAdvanced> logger;
    private readonly IMeshDirectory directory;
    private readonly MeshStatsCollector statsCollector;

    public MeshAdvanced(
        ILogger<MeshAdvanced> logger, 
        IMeshDirectory directory,
        MeshStatsCollector statsCollector)
    {
        this.logger = logger;
        this.directory = directory;
        this.statsCollector = statsCollector;
    }

    public async Task<IReadOnlyList<MeshPeerDescriptor>> DiscoverPeersAdvancedAsync(string contentId, MeshTransportPreference preference, CancellationToken ct = default)
    {
        var peers = await directory.FindPeersByContentAsync(contentId, ct);
        logger.LogDebug("[MeshAdvanced] Discover peers for {ContentId} pref={Pref} found={Count}", contentId, preference, peers.Count);
        return peers;
    }

    public Task<IReadOnlyList<MeshRouteDiagnostics>> TraceRoutesAsync(string peerId, CancellationToken ct = default)
    {
        var diag = new List<MeshRouteDiagnostics>();
        diag.Add(new MeshRouteDiagnostics(peerId, "dht", 1, false));
        return Task.FromResult<IReadOnlyList<MeshRouteDiagnostics>>(diag);
    }

    public Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct = default)
    {
        // Get real stats from collector
        var stats = statsCollector.GetStats();
        logger.LogDebug(
            "[MeshAdvanced] Transport stats: DHT={DhtNodes}, Overlay={OverlayConns}, NAT={NatType}",
            stats.ActiveDhtSessions,
            stats.ActiveOverlaySessions,
            stats.DetectedNatType);
        return Task.FromResult(stats);
    }
}
