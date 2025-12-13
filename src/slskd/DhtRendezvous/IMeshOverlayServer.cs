// <copyright file="IMeshOverlayServer.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// TCP server for accepting inbound overlay connections from mesh peers.
/// </summary>
public interface IMeshOverlayServer
{
    /// <summary>
    /// Gets a value indicating whether the server is currently listening.
    /// </summary>
    bool IsListening { get; }

    /// <summary>
    /// Gets the number of active connections.
    /// </summary>
    int ActiveConnections { get; }

    /// <summary>
    /// Gets the total number of connections accepted.
    /// </summary>
    long TotalConnectionsAccepted { get; }

    /// <summary>
    /// Gets the total number of connections rejected.
    /// </summary>
    long TotalConnectionsRejected { get; }

    /// <summary>
    /// Start listening for incoming overlay connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop listening for incoming overlay connections.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Get current server statistics.
    /// </summary>
    /// <returns>Server statistics.</returns>
    MeshOverlayServerStats GetStats();
}

