// <copyright file="MeshOverlayServerStats.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;

/// <summary>
/// Statistics for the mesh overlay server.
/// </summary>
public class MeshOverlayServerStats
{
    /// <summary>
    /// Gets or sets a value indicating whether the server is currently listening.
    /// </summary>
    public bool IsListening { get; set; }

    /// <summary>
    /// Gets or sets the configured listen port.
    /// </summary>
    public int? ListenPort { get; set; }

    /// <summary>
    /// Gets or sets the number of active connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of connections accepted.
    /// </summary>
    public long TotalConnectionsAccepted { get; set; }

    /// <summary>
    /// Gets or sets the total number of connections rejected.
    /// </summary>
    public long TotalConnectionsRejected { get; set; }

    /// <summary>
    /// Gets or sets the time the server was started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets the server uptime.
    /// </summary>
    public TimeSpan Uptime => StartedAt.HasValue ? DateTimeOffset.UtcNow - StartedAt.Value : TimeSpan.Zero;
}

