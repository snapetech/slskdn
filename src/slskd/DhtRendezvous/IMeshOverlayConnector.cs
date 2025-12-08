// <copyright file="IMeshOverlayConnector.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for making outbound overlay connections to mesh peers.
/// Used by seekers to connect to beacons discovered via DHT.
/// </summary>
public interface IMeshOverlayConnector
{
    /// <summary>
    /// Number of pending connection attempts.
    /// </summary>
    int PendingConnections { get; }
    
    /// <summary>
    /// Number of successful connections made.
    /// </summary>
    long SuccessfulConnections { get; }
    
    /// <summary>
    /// Number of failed connection attempts.
    /// </summary>
    long FailedConnections { get; }
    
    /// <summary>
    /// Attempt to connect to a list of candidate endpoints.
    /// Stops when enough neighbors are connected or all candidates exhausted.
    /// </summary>
    /// <param name="candidates">Endpoints to try (typically from DHT discovery).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of successful new connections.</returns>
    Task<int> ConnectToCandidatesAsync(
        IEnumerable<IPEndPoint> candidates,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Attempt to connect to a specific endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to connect to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection if successful, null if failed.</returns>
    Task<MeshOverlayConnection?> ConnectToEndpointAsync(
        IPEndPoint endpoint,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get connector statistics.
    /// </summary>
    MeshOverlayConnectorStats GetStats();
}

/// <summary>
/// Connector statistics.
/// </summary>
public sealed class MeshOverlayConnectorStats
{
    public int PendingConnections { get; init; }
    public long SuccessfulConnections { get; init; }
    public long FailedConnections { get; init; }
    public double SuccessRate => SuccessfulConnections + FailedConnections > 0
        ? (double)SuccessfulConnections / (SuccessfulConnections + FailedConnections)
        : 0;
}

