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

    public MeshAdvanced(ILogger<MeshAdvanced> logger, IMeshDirectory directory)
    {
        this.logger = logger;
        this.directory = directory;
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
        // Minimal counters for now (can be wired to real metrics later)
        var stats = new MeshTransportStats(
            ActiveDhtSessions: 0,
            ActiveOverlaySessions: 0,
            ActiveMirroredSessions: 0);
        return Task.FromResult(stats);
    }
}
