// <copyright file="IMeshAdvanced.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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

public record MeshRouteDiagnostics(
    string PeerId,
    string Transport,
    int Hops,
    bool NatTraversalAttempted,
    IReadOnlyList<long>? HopLatenciesMs = null);

public record MeshTransportStats(
    int ActiveDhtSessions,
    int ActiveOverlaySessions,
    int ActiveMirroredSessions,
    NatType DetectedNatType = NatType.Unknown,
    int TotalPeers = 0,
    long MessagesSent = 0,
    long MessagesReceived = 0,
    double DhtOperationsPerSecond = 0.0,
    int RoutingTableSize = 0,
    int BootstrapPeers = 0,
    long PeerChurnEvents = 0);
