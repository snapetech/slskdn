// <copyright file="IMeshOverlayMessageHandler.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System.Threading;
using System.Threading.Tasks;
using slskd.Mesh.Messages;

/// <summary>
/// Handles mesh protocol messages received over overlay connections.
/// This decouples mesh sync from Soulseek private messages.
/// </summary>
public interface IMeshOverlayMessageHandler
{
    /// <summary>
    /// Handles a mesh message received from a peer over the overlay.
    /// </summary>
    /// <param name="fromMeshPeerId">The sender's mesh peer ID.</param>
    /// <param name="message">The mesh message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response message, or null if no response needed.</returns>
    Task<MeshMessage?> HandleMessageAsync(
        string fromMeshPeerId, 
        MeshMessage message, 
        CancellationToken cancellationToken = default);
}















