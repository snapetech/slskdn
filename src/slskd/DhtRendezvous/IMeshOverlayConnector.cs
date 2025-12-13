// <copyright file="IMeshOverlayConnector.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System.Net;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Makes outbound overlay connections to mesh peers discovered via DHT.
/// </summary>
public interface IMeshOverlayConnector
{
    /// <summary>
    /// Gets the number of pending connection attempts.
    /// </summary>
    int PendingConnections { get; }

    /// <summary>
    /// Gets the total number of successful connections.
    /// </summary>
    long SuccessfulConnections { get; }

    /// <summary>
    /// Gets the total number of failed connections.
    /// </summary>
    long FailedConnections { get; }

    /// <summary>
    /// Attempt to connect to multiple candidate endpoints.
    /// </summary>
    /// <param name="candidates">The endpoints to try connecting to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of successful connections.</returns>
    Task<int> ConnectToCandidatesAsync(System.Collections.Generic.IEnumerable<IPEndPoint> candidates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempt to connect to a specific endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to connect to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection if successful, null otherwise.</returns>
    Task<MeshOverlayConnection?> ConnectToEndpointAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current connector statistics.
    /// </summary>
    /// <returns>Connector statistics.</returns>
    MeshOverlayConnectorStats GetStats();
}

