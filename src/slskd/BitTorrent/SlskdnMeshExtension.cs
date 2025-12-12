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
            // 1. Validate data size
            if (data.Length > 10 * 1024) // 10KB max
            {
                _logger.LogWarning(
                    "Handshake data too large from {Address}: {Size} bytes",
                    remoteAddress, data.Length);
                return;
            }
            
            // 2. Parse JSON with safety limits
            var options = new JsonSerializerOptions
            {
                MaxDepth = 5,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = false,
            };
            
            var json = Encoding.UTF8.GetString(data);
            var handshake = JsonSerializer.Deserialize<MeshExtensionHandshake>(json, options);
            
            if (handshake == null || string.IsNullOrEmpty(handshake.MeshPeerId))
            {
                _logger.LogWarning("Received invalid mesh extension handshake from {Address}", remoteAddress);
                return;
            }
            
            // 3. Validate all fields
            if (handshake.OverlayPort < 1 || handshake.OverlayPort > 65535)
            {
                _logger.LogWarning(
                    "Invalid overlay port from {Address}: {Port}",
                    remoteAddress, handshake.OverlayPort);
                return;
            }
            
            if (string.IsNullOrEmpty(handshake.PublicKey) || handshake.PublicKey.Length > 1024)
            {
                _logger.LogWarning("Invalid public key from {Address}", remoteAddress);
                return;
            }
            
            _logger.LogInformation(
                "Received mesh extension handshake from {Address}: MeshId={MeshId}, Port={Port}, Capabilities={Caps}",
                remoteAddress,
                handshake.MeshPeerId,
                handshake.OverlayPort,
                string.Join(",", handshake.Capabilities ?? new List<string>()));
            
            // 4. Parse and validate public key
            byte[] publicKey;
            try
            {
                publicKey = Convert.FromBase64String(handshake.PublicKey);
                
                if (publicKey.Length != 32) // Ed25519 key is 32 bytes
                {
                    _logger.LogWarning(
                        "Invalid public key length from {Address}: {Length} bytes (expected 32)",
                        remoteAddress, publicKey.Length);
                    return;
                }
            }
            catch (FormatException)
            {
                _logger.LogWarning("Invalid base64 public key from {Address}", remoteAddress);
                return;
            }
            
            // 5. Parse mesh peer ID
            MeshPeerId meshPeerId;
            try
            {
                meshPeerId = MeshPeerId.Parse(handshake.MeshPeerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid mesh peer ID from {Address}: {MeshId}",
                    remoteAddress, handshake.MeshPeerId);
                return;
            }
            
            // 6. Request signature proof via challenge-response
            // For now, we create an unsigned descriptor and mark it as "unverified"
            // TODO: Implement challenge-response to get a proper signature
            var endpoint = new IPEndPoint(remoteAddress, handshake.OverlayPort);
            
            var descriptor = new MeshPeerDescriptor
            {
                MeshPeerId = meshPeerId,
                PublicKey = publicKey,
                Signature = Array.Empty<byte>(), // Will be filled by challenge-response
                Endpoints = new[] { endpoint },
                Capabilities = handshake.Capabilities ?? new List<string>(),
                Timestamp = DateTimeOffset.UtcNow,
            };
            
            // 7. Verify signature if present
            // For BT-discovered peers without signature, we mark as unverified
            // They can be upgraded to verified via challenge-response
            if (descriptor.Signature.Length == 0)
            {
                _logger.LogInformation(
                    "BitTorrent peer {MeshId} at {Endpoint} registered as unverified (no signature). " +
                    "Will require proof before participating in mesh operations.",
                    meshPeerId.ToShortString(),
                    endpoint);
                
                // TODO: Initiate challenge-response to get signature
                // For now, we DON'T register unverified peers
                _logger.LogWarning(
                    "Skipping registration of unverified BitTorrent peer {MeshId} from {Address}. " +
                    "Signature verification required.",
                    meshPeerId.ToShortString(),
                    remoteAddress);
                return;
            }
            
            // Register peer only if verified
            await _meshPeerRegistry.RegisterOrUpdateAsync(descriptor, cancellationToken);
            
            _logger.LogInformation(
                "Registered verified mesh peer from BitTorrent: {MeshId} at {Endpoint}",
                meshPeerId.ToShortString(),
                endpoint);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed JSON handshake from {Address}", remoteAddress);
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
