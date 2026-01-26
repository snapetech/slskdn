// <copyright file="ILanDiscoveryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Service for LAN discovery via mDNS.</summary>
public interface ILanDiscoveryService
{
    /// <summary>Start advertising this peer via mDNS.</summary>
    Task StartAdvertisingAsync(CancellationToken ct = default);

    /// <summary>Stop advertising.</summary>
    Task StopAdvertisingAsync();

    /// <summary>Browse for nearby peers.</summary>
    Task<IReadOnlyList<DiscoveredPeer>> BrowseAsync(CancellationToken ct = default);

    /// <summary>Event fired when a peer is discovered.</summary>
    event EventHandler<DiscoveredPeer>? PeerDiscovered;
}

/// <summary>A peer discovered via mDNS.</summary>
public sealed class DiscoveredPeer
{
    public string PeerCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty; // "https://ip:port"
    public int Capabilities { get; set; }
}
