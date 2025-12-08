// <copyright file="IMeshOverlayServer.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for accepting inbound overlay connections from mesh peers.
/// Only runs if this client is beacon-capable (publicly reachable).
/// </summary>
public interface IMeshOverlayServer
{
    /// <summary>
    /// Whether the server is currently listening.
    /// </summary>
    bool IsListening { get; }
    
    /// <summary>
    /// Port the server is listening on.
    /// </summary>
    int ListenPort { get; }
    
    /// <summary>
    /// Number of active overlay connections.
    /// </summary>
    int ActiveConnections { get; }
    
    /// <summary>
    /// Total connections accepted since server started.
    /// </summary>
    long TotalConnectionsAccepted { get; }
    
    /// <summary>
    /// Total connections rejected (rate limited, blocked, etc).
    /// </summary>
    long TotalConnectionsRejected { get; }
    
    /// <summary>
    /// Start listening for inbound connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop listening and close all connections.
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Get statistics about the server.
    /// </summary>
    MeshOverlayServerStats GetStats();
}

/// <summary>
/// Server statistics.
/// </summary>
public sealed class MeshOverlayServerStats
{
    public bool IsListening { get; init; }
    public int ListenPort { get; init; }
    public int ActiveConnections { get; init; }
    public long TotalConnectionsAccepted { get; init; }
    public long TotalConnectionsRejected { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public TimeSpan Uptime => StartedAt.HasValue ? DateTimeOffset.UtcNow - StartedAt.Value : TimeSpan.Zero;
}

