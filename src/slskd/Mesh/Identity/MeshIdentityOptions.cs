// <copyright file="MeshIdentityOptions.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

/// <summary>
/// Configuration options for mesh identity and peer management.
/// </summary>
public sealed class MeshIdentityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable DHT-first mesh joining.
    /// When true, peers can join mesh via DHT/BT without requiring Soulseek login.
    /// </summary>
    public bool EnableDhtFirstJoin { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to require descriptor signature verification.
    /// When true, only peers with valid Ed25519 signatures can join mesh.
    /// </summary>
    public bool RequireDescriptorSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of mesh peers to track in registry.
    /// Prevents unbounded memory growth.
    /// </summary>
    public int MaxTrackedPeers { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how long (in seconds) to retain inactive peer records.
    /// Peers not seen for this duration are eligible for cleanup.
    /// </summary>
    public int PeerRetentionSeconds { get; set; } = 30 * 24 * 60 * 60; // 30 days
}














