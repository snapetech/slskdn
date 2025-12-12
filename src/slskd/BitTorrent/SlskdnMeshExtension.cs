// <copyright file="SlskdnMeshExtension.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.BitTorrent;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using slskd.Mesh.Identity;

/// <summary>
/// BitTorrent extension for exchanging slskdn mesh peer information.
/// Allows peers to discover each other's mesh identities and capabilities
/// without requiring Soulseek presence.
/// </summary>
public sealed class SlskdnMeshExtension
{
    public const string ExtensionName = "slskdn_mesh";
    public const byte ExtensionMessageId = 20; // Custom extension message ID
    
    private readonly ILogger<SlskdnMeshExtension> _logger;
    private readonly LocalMeshIdentityService _localIdentity;
    private readonly IMeshPeerRegistry _meshPeerRegistry;
    private readonly int _overlayPort;
    
    public SlskdnMeshExtension(
        ILogger<SlskdnMeshExtension> logger,
        LocalMeshIdentityService localIdentity,
        IMeshPeerRegistry meshPeerRegistry,
        int overlayPort)
    {
        _logger = logger;
        _localIdentity = localIdentity;
        _meshPeerRegistry = meshPeerRegistry;
        _overlayPort = overlayPort;
    }
    
    /// <summary>
    /// Initializes the BT extension.
    /// </summary>
    public Task InitializeAsync()
    {
        _logger.LogInformation(
            "BitTorrent mesh extension initialized. MeshPeerId: {MeshId}, OverlayPort: {Port}",
            _localIdentity.MeshPeerId.ToShortString(),
            _overlayPort);
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Creates the extension handshake data to send to remote peers.
    /// This is sent during the BT extended handshake.
    /// </summary>
    public byte[] CreateHandshakeData()
    {
        var handshake = new MeshExtensionHandshake
        {
            MeshPeerId = _localIdentity.MeshPeerId.ToString(),
            PublicKey = Convert.ToBase64String(_localIdentity.PublicKey),
            OverlayPort = _overlayPort,
            Capabilities = new List<string> { "overlay", "hash-sync", "swarm" },
            Version = "1.0",
        };
        
        var json = JsonSerializer.Serialize(handshake);
        return Encoding.UTF8.GetBytes(json);
    }
    
    /// <summary>
    /// Handles incoming extension handshake data from a remote peer.
    /// </summary>
    public async Task HandleHandshakeAsync(
        byte[] data,
        IPAddress remoteAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            var handshake = JsonSerializer.Deserialize<MeshExtensionHandshake>(json);
            
            if (handshake == null || string.IsNullOrEmpty(handshake.MeshPeerId))
            {
                _logger.LogWarning("Received invalid mesh extension handshake from {Address}", remoteAddress);
                return;
            }
            
            _logger.LogInformation(
                "Received mesh extension handshake from {Address}: MeshId={MeshId}, Port={Port}, Capabilities={Caps}",
                remoteAddress,
                handshake.MeshPeerId,
                handshake.OverlayPort,
                string.Join(",", handshake.Capabilities ?? new List<string>()));
            
            // Parse public key
            byte[] publicKey;
            try
            {
                publicKey = Convert.FromBase64String(handshake.PublicKey);
            }
            catch
            {
                _logger.LogWarning("Invalid public key in mesh extension handshake from {Address}", remoteAddress);
                return;
            }
            
            // Create mesh peer descriptor
            var meshPeerId = MeshPeerId.Parse(handshake.MeshPeerId);
            var endpoint = new IPEndPoint(remoteAddress, handshake.OverlayPort);
            
            var descriptor = new MeshPeerDescriptor
            {
                MeshPeerId = meshPeerId,
                PublicKey = publicKey,
                Signature = Array.Empty<byte>(), // BT extension doesn't include full descriptor signature
                Endpoints = new[] { endpoint },
                Capabilities = handshake.Capabilities ?? new List<string>(),
                Timestamp = DateTimeOffset.UtcNow,
            };
            
            // Register peer (without signature verification for BT-discovered peers)
            await _meshPeerRegistry.RegisterOrUpdateAsync(descriptor, cancellationToken);
            
            _logger.LogInformation(
                "Registered mesh peer from BitTorrent: {MeshId} at {Endpoint}",
                meshPeerId.ToShortString(),
                endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling mesh extension handshake from {Address}", remoteAddress);
        }
    }
}

/// <summary>
/// Mesh extension handshake message format.
/// </summary>
internal sealed class MeshExtensionHandshake
{
    public string MeshPeerId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public int OverlayPort { get; set; }
    public List<string> Capabilities { get; set; } = new();
    public string Version { get; set; } = "1.0";
}
