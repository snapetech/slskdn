// <copyright file="MeshPeerManager.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;

namespace slskd.Mesh;

/// <summary>
/// Manages mesh peers and peer discovery.
/// </summary>
public class MeshPeerManager : IMeshPeerManager
{
    private readonly ILogger<MeshPeerManager> _logger;
    private readonly Dictionary<string, MeshPeer> _peers = new();
    private readonly object _peersLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshPeerManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MeshPeerManager(ILogger<MeshPeerManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all available peers in the mesh.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of available peers.</returns>
    public Task<List<MeshPeer>> GetAvailablePeersAsync(CancellationToken cancellationToken = default)
    {
        lock (_peersLock)
        {
            // Return peers that have been seen recently (within last 24 hours)
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var availablePeers = _peers.Values
                .Where(p => p.LastSeen > cutoff)
                .ToList();

            return Task.FromResult(availablePeers);
        }
    }

    /// <summary>
    /// Gets a peer by ID.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The peer, or null if not found.</returns>
    public MeshPeer? GetPeer(string peerId)
    {
        lock (_peersLock)
        {
            _peers.TryGetValue(peerId, out var peer);
            return peer;
        }
    }

    /// <summary>
    /// Adds or updates a peer in the mesh.
    /// </summary>
    /// <param name="peer">The peer to add or update.</param>
    public void AddOrUpdatePeer(MeshPeer peer)
    {
        lock (_peersLock)
        {
            _peers[peer.PeerId] = peer;
            _logger.LogDebug("Added/updated peer {PeerId} with {AddressCount} addresses",
                peer.PeerId, peer.Addresses.Count);
        }
    }

    /// <summary>
    /// Removes a peer from the mesh.
    /// </summary>
    /// <param name="peerId">The peer ID to remove.</param>
    public void RemovePeer(string peerId)
    {
        lock (_peersLock)
        {
            if (_peers.Remove(peerId))
            {
                _logger.LogDebug("Removed peer {PeerId}", peerId);
            }
        }
    }

    /// <summary>
    /// Gets peers suitable for circuit building (high quality, onion routing support).
    /// </summary>
    /// <param name="minQualityScore">Minimum quality score required.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of suitable peers.</returns>
    public async Task<List<MeshPeer>> GetCircuitPeersAsync(double minQualityScore = 0.3, CancellationToken cancellationToken = default)
    {
        var allPeers = await GetAvailablePeersAsync(cancellationToken);

        return allPeers
            .Where(p => p.GetQualityScore() >= minQualityScore && p.SupportsOnionRouting)
            .OrderByDescending(p => p.GetQualityScore())
            .ToList();
    }

    /// <summary>
    /// Updates peer information from a discovery message.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="addresses">The peer addresses.</param>
    /// <param name="version">The peer version.</param>
    /// <param name="supportsOnionRouting">Whether the peer supports onion routing.</param>
    public void UpdatePeerInfo(string peerId, List<IPEndPoint>? addresses = null, string? version = null, bool? supportsOnionRouting = null)
    {
        lock (_peersLock)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.UpdateInfo(addresses, version, supportsOnionRouting);
                _logger.LogDebug("Updated peer {PeerId} info", peerId);
            }
            else
            {
                // Create new peer if it doesn't exist
                if (addresses != null && addresses.Count > 0)
                {
                    peer = new MeshPeer(peerId, addresses);
                    peer.Version = version ?? string.Empty;
                    peer.SupportsOnionRouting = supportsOnionRouting ?? false;
                    _peers[peerId] = peer;
                    _logger.LogDebug("Created new peer {PeerId}", peerId);
                }
            }
        }
    }

    /// <summary>
    /// Records a successful connection to a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="latencyMs">The connection latency.</param>
    public void RecordConnectionSuccess(string peerId, int latencyMs)
    {
        lock (_peersLock)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.RecordSuccessfulConnection(latencyMs);
                _logger.LogDebug("Recorded successful connection to {PeerId} ({LatencyMs}ms)", peerId, latencyMs);
            }
        }
    }

    /// <summary>
    /// Records a failed connection to a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    public void RecordConnectionFailure(string peerId)
    {
        lock (_peersLock)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.RecordFailedConnection();
                _logger.LogDebug("Recorded failed connection to {PeerId}", peerId);
            }
        }
    }

    /// <summary>
    /// Gets peer manager statistics.
    /// </summary>
    /// <returns>Peer statistics.</returns>
    public PeerStatistics GetStatistics()
    {
        lock (_peersLock)
        {
            var activePeers = _peers.Values.Where(p => (DateTimeOffset.UtcNow - p.LastSeen).TotalHours < 24).ToList();
            var onionPeers = activePeers.Where(p => p.SupportsOnionRouting).ToList();

            return new PeerStatistics
            {
                TotalPeers = _peers.Count,
                ActivePeers = activePeers.Count,
                OnionRoutingPeers = onionPeers.Count,
                AverageQualityScore = activePeers.Any() ? activePeers.Average(p => p.GetQualityScore()) : 0,
                AverageLatencyMs = activePeers.Where(p => p.LatencyMs > 0).Any()
                    ? activePeers.Where(p => p.LatencyMs > 0).Average(p => p.LatencyMs)
                    : 0
            };
        }
    }

    /// <summary>
    /// Performs maintenance operations (cleanup old peers, etc.).
    /// </summary>
    public void PerformMaintenance()
    {
        lock (_peersLock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-7); // Remove peers not seen in 7 days
            var oldPeers = _peers.Where(kvp => kvp.Value.LastSeen < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var peerId in oldPeers)
            {
                _peers.Remove(peerId);
            }

            if (oldPeers.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old peers", oldPeers.Count);
            }
        }
    }
}
