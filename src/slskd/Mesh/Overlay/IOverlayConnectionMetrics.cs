// <copyright file="IOverlayConnectionMetrics.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Overlay;

/// <summary>
/// Provides runtime overlay connection metrics.
/// </summary>
public interface IOverlayConnectionMetrics
{
    /// <summary>
    /// Gets the number of active overlay connections.
    /// </summary>
    int GetActiveConnectionCount();
}
