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
        // Currently no content->peer index; reuse directory stub.
        var peers = await directory.FindPeersByContentAsync(contentId, ct);
        logger.LogDebug("[MeshAdvanced] Discover peers for {ContentId} pref={Pref} found={Count}", contentId, preference, peers.Count);
        return peers;
    }

    public Task<IReadOnlyList<MeshRouteDiagnostics>> TraceRoutesAsync(string peerId, CancellationToken ct = default)
    {
        // Placeholder route diagnostics
        var diag = new List<MeshRouteDiagnostics>
        {
            new(peerId, "dht", 1, false)
        };
        return Task.FromResult<IReadOnlyList<MeshRouteDiagnostics>>(diag);
    }

    public Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct = default)
    {
        // Placeholder stats
        var stats = new MeshTransportStats(
            ActiveDhtSessions: 0,
            ActiveOverlaySessions: 0,
            ActiveMirroredSessions: 0);
        return Task.FromResult(stats);
    }
}
