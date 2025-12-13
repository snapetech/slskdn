// <copyright file="MeshOverlayConnectorStats.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

/// <summary>
/// Statistics for the mesh overlay connector.
/// </summary>
public class MeshOverlayConnectorStats
{
    /// <summary>
    /// Gets or sets the number of pending connection attempts.
    /// </summary>
    public int PendingConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of successful connections.
    /// </summary>
    public long SuccessfulConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of failed connections.
    /// </summary>
    public long FailedConnections { get; set; }

    /// <summary>
    /// Gets the connection success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessfulConnections + FailedConnections;
            return total > 0 ? (double)SuccessfulConnections / total : 0.0;
        }
    }
}

