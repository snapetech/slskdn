// <copyright file="IMeshPeerManager.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;

namespace slskd.Mesh;

/// <summary>
/// Interface for managing mesh peers and peer discovery.
/// </summary>
public interface IMeshPeerManager
{
    /// <summary>
    /// Gets all available peers in the mesh.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of available peers.</returns>
    Task<List<MeshPeer>> GetAvailablePeersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a peer by ID.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The peer, or null if not found.</returns>
    MeshPeer? GetPeer(string peerId);

    /// <summary>
    /// Adds or updates a peer in the mesh.
    /// </summary>
    /// <param name="peer">The peer to add or update.</param>
    void AddOrUpdatePeer(MeshPeer peer);

    /// <summary>
    /// Removes a peer from the mesh.
    /// </summary>
    /// <param name="peerId">The peer ID to remove.</param>
    void RemovePeer(string peerId);

    /// <summary>
    /// Gets peers suitable for circuit building (high quality, onion routing support).
    /// </summary>
    /// <param name="minQualityScore">Minimum quality score required.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of suitable peers.</returns>
    Task<List<MeshPeer>> GetCircuitPeersAsync(double minQualityScore = 0.3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates peer information from a discovery message.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="addresses">The peer addresses.</param>
    /// <param name="version">The peer version.</param>
    /// <param name="supportsOnionRouting">Whether the peer supports onion routing.</param>
    void UpdatePeerInfo(string peerId, List<IPEndPoint>? addresses = null, string? version = null, bool? supportsOnionRouting = null);

    /// <summary>
    /// Records a successful connection to a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="latencyMs">The connection latency.</param>
    void RecordConnectionSuccess(string peerId, int latencyMs);

    /// <summary>
    /// Records a failed connection to a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    void RecordConnectionFailure(string peerId);

    /// <summary>
    /// Gets peer manager statistics.
    /// </summary>
    /// <returns>Peer statistics.</returns>
    PeerStatistics GetStatistics();
}

/// <summary>
/// Statistics about mesh peers.
/// </summary>
public class PeerStatistics
{
    /// <summary>
    /// Gets or sets the total number of known peers.
    /// </summary>
    public int TotalPeers { get; set; }

    /// <summary>
    /// Gets or sets the number of active peers.
    /// </summary>
    public int ActivePeers { get; set; }

    /// <summary>
    /// Gets or sets the number of peers that support onion routing.
    /// </summary>
    public int OnionRoutingPeers { get; set; }

    /// <summary>
    /// Gets or sets the average peer quality score.
    /// </summary>
    public double AverageQualityScore { get; set; }

    /// <summary>
    /// Gets or sets the average peer latency.
    /// </summary>
    public double AverageLatencyMs { get; set; }
}
