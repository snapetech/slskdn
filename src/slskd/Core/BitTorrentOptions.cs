// <copyright file="BitTorrentOptions.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Core;

/// <summary>
/// Configuration options for BitTorrent rendezvous functionality.
/// </summary>
public sealed class BitTorrentOptions
{
    /// <summary>
    /// Enable BitTorrent rendezvous for peer discovery.
    /// When enabled, slskdn joins a well-known rendezvous torrent swarm
    /// and exchanges mesh peer information via BT extension protocol.
    /// Default: false (opt-in feature)
    /// </summary>
    public bool EnableRendezvousTorrent { get; set; } = false;
    
    /// <summary>
    /// Port for BitTorrent client. If 0, a random port is used.
    /// Default: 0 (random)
    /// </summary>
    public int Port { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of peers in rendezvous swarm.
    /// Default: 50
    /// </summary>
    public int MaxRendezvousPeers { get; set; } = 50;
    
    /// <summary>
    /// Enable DHT for BitTorrent (separate from mesh DHT).
    /// Default: true
    /// </summary>
    public bool EnableDht { get; set; } = true;
    
    /// <summary>
    /// Enable PEX (Peer Exchange) for discovering more peers.
    /// Default: true
    /// </summary>
    public bool EnablePex { get; set; } = true;
}














