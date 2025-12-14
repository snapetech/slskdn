// <copyright file="IDhtRendezvousService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for discovering and connecting to mesh peers via BitTorrent DHT rendezvous.
/// Uses the DHT purely as a peer discovery mechanism - no content hashes are stored in DHT.
/// </summary>
public interface IDhtRendezvousService
{
    /// <summary>
    /// Whether this client is beacon-capable (publicly reachable).
    /// Beacons announce themselves to the DHT and accept inbound connections.
    /// </summary>
    bool IsBeaconCapable { get; }
    
    /// <summary>
    /// Whether the DHT node is currently running.
    /// </summary>
    bool IsDhtRunning { get; }
    
    /// <summary>
    /// Number of DHT nodes in our routing table.
    /// </summary>
    int DhtNodeCount { get; }
    
    /// <summary>
    /// Number of mesh peers discovered via DHT.
    /// </summary>
    int DiscoveredPeerCount { get; }
    
    /// <summary>
    /// Number of active mesh overlay connections.
    /// </summary>
    int ActiveMeshConnections { get; }
    
    /// <summary>
    /// Start the DHT node and begin announce/discovery loops.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop the DHT node and close all connections.
    /// </summary>
    new Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Force a peer discovery cycle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of new peers discovered.</returns>
    Task<int> DiscoverPeersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Force a DHT announcement (beacon mode only).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnnounceAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get list of currently discovered peer endpoints.
    /// </summary>
    IReadOnlyList<IPEndPoint> GetDiscoveredPeers();
    
    /// <summary>
    /// Get list of active mesh connections.
    /// </summary>
    IReadOnlyList<MeshPeerInfo> GetMeshPeers();
    
    /// <summary>
    /// Get DHT rendezvous service statistics.
    /// </summary>
    DhtRendezvousStats GetStats();
}

/// <summary>
/// Information about a connected mesh peer.
/// </summary>
public sealed class MeshPeerInfo
{
    /// <summary>Mesh peer ID (canonical identity, required).</summary>
    public required string MeshPeerId { get; init; }
    
    /// <summary>Soulseek username (optional alias).</summary>
    public string? Username { get; init; }
    
    /// <summary>Remote endpoint.</summary>
    public required IPEndPoint Endpoint { get; init; }
    
    /// <summary>Supported features.</summary>
    public required IReadOnlyList<string> Features { get; init; }
    
    /// <summary>When connection was established.</summary>
    public required DateTimeOffset ConnectedAt { get; init; }
    
    /// <summary>Last activity time.</summary>
    public required DateTimeOffset LastActivity { get; init; }
    
    /// <summary>Certificate thumbprint.</summary>
    public string? CertificateThumbprint { get; init; }
    
    /// <summary>Peer protocol version (if exchanged during handshake).</summary>
    public int? PeerVersion { get; init; }
}

/// <summary>
/// DHT rendezvous statistics.
/// </summary>
public sealed class DhtRendezvousStats
{
    public bool IsBeaconCapable { get; init; }
    public bool IsDhtRunning { get; init; }
    public int DhtNodeCount { get; init; }
    public string DhtState { get; init; } = "Unknown";
    public int DiscoveredPeerCount { get; init; }
    public int ActiveMeshConnections { get; init; }
    
    /// <summary>Count of verified slskdn beacons (successful handshakes).</summary>
    public int VerifiedBeaconCount { get; init; }
    
    public long TotalPeersDiscovered { get; init; }
    public long TotalConnectionsAttempted { get; init; }
    public long TotalConnectionsSucceeded { get; init; }
    public DateTimeOffset? LastAnnounceTime { get; init; }
    public DateTimeOffset? LastDiscoveryTime { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    
    /// <summary>The rendezvous infohashes we announce/query (hex).</summary>
    public IReadOnlyList<string> RendezvousInfohashes { get; init; } = Array.Empty<string>();
}

