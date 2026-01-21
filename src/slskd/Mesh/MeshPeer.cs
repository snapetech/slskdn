// <copyright file="MeshPeer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;

namespace slskd.Mesh;

/// <summary>
/// Represents a peer in the mesh network.
/// </summary>
public class MeshPeer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeshPeer"/> class.
    /// </summary>
    /// <param name="peerId">The unique peer ID.</param>
    /// <param name="addresses">The peer's network addresses.</param>
    public MeshPeer(string peerId, List<IPEndPoint> addresses)
    {
        PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
        Addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));

        LastSeen = DateTimeOffset.UtcNow;
        TrustScore = 0.5; // Default neutral trust
        LatencyMs = 0;
        BandwidthMbps = 0;
    }

    /// <summary>
    /// Gets the unique peer ID.
    /// </summary>
    public string PeerId { get; }

    /// <summary>
    /// Gets the peer's network addresses.
    /// </summary>
    public List<IPEndPoint> Addresses { get; }

    /// <summary>
    /// Gets or sets the last time this peer was seen.
    /// </summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>
    /// Gets or sets the trust score (0.0 to 1.0).
    /// </summary>
    public double TrustScore { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the available bandwidth in Mbps.
    /// </summary>
    public double BandwidthMbps { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this peer supports onion routing.
    /// </summary>
    public bool SupportsOnionRouting { get; set; }

    /// <summary>
    /// Gets or sets the peer's mesh version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets the best address to use for connecting to this peer.
    /// </summary>
    /// <returns>The preferred address.</returns>
    public IPEndPoint GetBestAddress()
    {
        // For now, just return the first address
        // In a real implementation, this would consider factors like:
        // - IPv4 vs IPv6 preference
        // - Recent connection success
        // - Geographic proximity
        return Addresses.FirstOrDefault() ?? throw new InvalidOperationException("No addresses available");
    }

    /// <summary>
    /// Calculates a quality score for circuit selection.
    /// Higher scores indicate better peers for routing.
    /// </summary>
    /// <returns>The quality score (0.0 to 1.0).</returns>
    public double GetQualityScore()
    {
        if (!SupportsOnionRouting)
        {
            return 0.0; // Can't use peers that don't support onion routing
        }

        var recencyScore = Math.Max(0, 1.0 - (DateTimeOffset.UtcNow - LastSeen).TotalHours / 24.0); // Decay over 24 hours
        var trustScore = TrustScore;
        var latencyScore = LatencyMs == 0 ? 0.5 : Math.Max(0, 1.0 - (LatencyMs / 1000.0)); // Prefer < 1 second latency
        var bandwidthScore = Math.Min(1.0, BandwidthMbps / 10.0); // Prefer >= 10 Mbps

        // Weighted combination
        return (recencyScore * 0.3) + (trustScore * 0.3) + (latencyScore * 0.2) + (bandwidthScore * 0.2);
    }

    /// <summary>
    /// Updates peer information from a heartbeat or discovery message.
    /// </summary>
    /// <param name="addresses">Updated addresses.</param>
    /// <param name="version">Peer version.</param>
    /// <param name="supportsOnionRouting">Whether the peer supports onion routing.</param>
    public void UpdateInfo(List<IPEndPoint>? addresses = null, string? version = null, bool? supportsOnionRouting = null)
    {
        LastSeen = DateTimeOffset.UtcNow;

        if (addresses != null && addresses.Count > 0)
        {
            Addresses.Clear();
            Addresses.AddRange(addresses);
        }

        if (version != null)
        {
            Version = version;
        }

        if (supportsOnionRouting.HasValue)
        {
            SupportsOnionRouting = supportsOnionRouting.Value;
        }
    }

    /// <summary>
    /// Records a successful connection to this peer.
    /// </summary>
    /// <param name="latencyMs">Connection latency.</param>
    public void RecordSuccessfulConnection(int latencyMs)
    {
        LatencyMs = (LatencyMs + latencyMs) / 2; // Simple moving average
        LastSeen = DateTimeOffset.UtcNow;

        // Increase trust score slightly
        TrustScore = Math.Min(1.0, TrustScore + 0.01);
    }

    /// <summary>
    /// Records a failed connection to this peer.
    /// </summary>
    public void RecordFailedConnection()
    {
        // Decrease trust score
        TrustScore = Math.Max(0.0, TrustScore - 0.05);
    }
}


