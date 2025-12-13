// <copyright file="IMeshPeerRegistry.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using slskd.Mesh.Dht;

/// <summary>
/// Registry for tracking and managing mesh peer identities.
/// This is the central authority for mesh peer discovery and verification.
/// </summary>
public interface IMeshPeerRegistry
{
    /// <summary>
    /// Registers or updates a mesh peer from a discovered descriptor.
    /// This method:
    /// - Verifies the descriptor signature (if enabled)
    /// - Checks SecurityCore for bans/denials
    /// - Stores/updates the peer record
    /// </summary>
    /// <param name="descriptor">The peer descriptor from DHT/BT discovery.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The registered/updated MeshPeer, or null if verification failed or peer is denied.</returns>
    Task<MeshPeer?> RegisterOrUpdateAsync(MeshPeerDescriptor descriptor, CancellationToken ct = default);

    /// <summary>
    /// Tries to get a registered mesh peer by ID.
    /// </summary>
    /// <param name="id">The mesh peer ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The MeshPeer if found and verified, otherwise null.</returns>
    Task<MeshPeer?> TryGetAsync(MeshPeerId id, CancellationToken ct = default);

    /// <summary>
    /// Gets all registered mesh peers.
    /// Only returns verified, non-banned peers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream of verified mesh peers.</returns>
    IAsyncEnumerable<MeshPeer> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a mesh peer ID is registered and verified.
    /// </summary>
    /// <param name="id">The mesh peer ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if registered and verified, false otherwise.</returns>
    Task<bool> IsVerifiedAsync(MeshPeerId id, CancellationToken ct = default);

    /// <summary>
    /// Updates the Soulseek username alias for a mesh peer.
    /// This should be called when a mesh peer logs into Soulseek.
    /// </summary>
    /// <param name="id">The mesh peer ID.</param>
    /// <param name="soulseekUsername">The Soulseek username to associate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateSoulseekAliasAsync(MeshPeerId id, string soulseekUsername, CancellationToken ct = default);

    /// <summary>
    /// Gets the count of registered, verified peers.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken ct = default);
}















