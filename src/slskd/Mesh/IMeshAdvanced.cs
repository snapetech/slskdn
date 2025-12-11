namespace slskd.Mesh;

/// <summary>
/// Advanced mesh operations (power users / research).
/// </summary>
public interface IMeshAdvanced
{
    Task<IReadOnlyList<MeshPeerDescriptor>> DiscoverPeersAdvancedAsync(string contentId, MeshTransportPreference preference, CancellationToken ct = default);
    Task<IReadOnlyList<MeshRouteDiagnostics>> TraceRoutesAsync(string peerId, CancellationToken ct = default);
    Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct = default);
}

public record MeshRouteDiagnostics(string PeerId, string Transport, int Hops, bool NatTraversalAttempted);

public record MeshTransportStats(
    int ActiveDhtSessions, 
    int ActiveOverlaySessions, 
    int ActiveMirroredSessions, 
    NatType DetectedNatType = NatType.Unknown);

