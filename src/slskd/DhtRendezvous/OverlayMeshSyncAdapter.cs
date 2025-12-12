// <copyright file="OverlayMeshSyncAdapter.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh;
using slskd.Mesh.Messages;

/// <summary>
/// Adapter that allows MeshSyncService to communicate over overlay connections
/// instead of Soulseek private messages. This bridges mesh-first identity with
/// the existing hash sync implementation.
/// </summary>
public sealed class OverlayMeshSyncAdapter : IMeshOverlayMessageHandler
{
    private readonly ILogger<OverlayMeshSyncAdapter> _logger;
    private readonly IMeshSyncService _meshSync;
    private readonly MeshNeighborRegistry _registry;
    private readonly slskd.Mesh.Identity.ISoulseekMeshIdentityMapper? _identityMapper;

    public OverlayMeshSyncAdapter(
        ILogger<OverlayMeshSyncAdapter> logger,
        IMeshSyncService meshSync,
        MeshNeighborRegistry registry,
        slskd.Mesh.Identity.ISoulseekMeshIdentityMapper? identityMapper = null)
    {
        _logger = logger;
        _meshSync = meshSync;
        _registry = registry;
        _identityMapper = identityMapper;
    }

    public async Task<MeshMessage?> HandleMessageAsync(
        string fromMeshPeerId,
        MeshMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to resolve mesh peer ID to Soulseek username for legacy compatibility
            // If no username is mapped, use mesh peer ID as the "username"
            string fromIdentifier = fromMeshPeerId;
            
            if (_identityMapper != null)
            {
                var meshId = slskd.Mesh.Identity.MeshPeerId.Parse(fromMeshPeerId);
                var username = await _identityMapper.TryGetSoulseekUsernameAsync(meshId, cancellationToken);
                if (!string.IsNullOrEmpty(username))
                {
                    fromIdentifier = username;
                }
            }
            
            // Forward to existing MeshSyncService
            // Note: MeshSyncService.HandleMessageAsync expects a username, but will work with mesh ID
            var response = await _meshSync.HandleMessageAsync(fromIdentifier, message, cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Error handling mesh message from {MeshPeerId}, type: {MessageType}", 
                fromMeshPeerId, 
                message.Type);
            return null;
        }
    }
    
    /// <summary>
    /// Initiates a mesh sync with a peer identified by mesh peer ID.
    /// </summary>
    public async Task<bool> TrySyncWithPeerAsync(
        string meshPeerId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = _registry.GetConnectionByMeshPeerId(meshPeerId);
            if (connection == null || !connection.IsConnected)
            {
                _logger.LogDebug("Cannot sync with {MeshPeerId}: not connected", meshPeerId);
                return false;
            }
            
            // Generate hello message from MeshSyncService
            var hello = _meshSync.GenerateHelloMessage();
            
            // Send over overlay connection
            await connection.WriteMessageAsync(hello, cancellationToken);
            
            // Wait for response (ReqDelta or PushDelta)
            // TODO: Implement proper request/response correlation
            // For now, responses will be handled by the overlay server's message loop
            
            _logger.LogInformation("Initiated mesh sync with {MeshPeerId}", meshPeerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initiate sync with {MeshPeerId}", meshPeerId);
            return false;
        }
    }
}
