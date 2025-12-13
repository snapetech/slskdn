// <copyright file="ISoulseekMeshIdentityMapper.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Maps between Soulseek usernames and mesh peer IDs.
/// Soulseek identity is an optional alias on top of mesh identity.
/// </summary>
public interface ISoulseekMeshIdentityMapper
{
    /// <summary>
    /// Creates or updates a mapping between a Soulseek username and mesh peer ID.
    /// </summary>
    /// <param name="soulseekUsername">The Soulseek username.</param>
    /// <param name="meshPeerId">The mesh peer ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MapAsync(string soulseekUsername, MeshPeerId meshPeerId, CancellationToken ct = default);

    /// <summary>
    /// Tries to get the mesh peer ID for a Soulseek username.
    /// </summary>
    /// <param name="soulseekUsername">The Soulseek username to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mesh peer ID if mapped, otherwise null.</returns>
    Task<MeshPeerId?> TryGetMeshPeerIdAsync(string soulseekUsername, CancellationToken ct = default);

    /// <summary>
    /// Tries to get the Soulseek username for a mesh peer ID.
    /// </summary>
    /// <param name="meshPeerId">The mesh peer ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Soulseek username if mapped, otherwise null.</returns>
    Task<string?> TryGetSoulseekUsernameAsync(MeshPeerId meshPeerId, CancellationToken ct = default);
}














