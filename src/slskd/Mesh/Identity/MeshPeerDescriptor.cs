// <copyright file="MeshPeerDescriptor.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System;
using System.Collections.Generic;
using System.Net;

/// <summary>
/// A signed descriptor for a mesh peer, published to DHT or exchanged via overlay handshake.
/// Contains the peer's public key, endpoints, capabilities, and a cryptographic signature.
/// </summary>
public sealed class MeshPeerDescriptor
{
    /// <summary>
    /// Gets or sets the mesh peer ID (derived from public key).
    /// </summary>
    public required MeshPeerId MeshPeerId { get; init; }
    
    /// <summary>
    /// Gets or sets the Ed25519 public key (32 bytes).
    /// </summary>
    public required byte[] PublicKey { get; init; }
    
    /// <summary>
    /// Gets or sets the Ed25519 signature over the descriptor content (64 bytes).
    /// Signature covers: MeshPeerId + Endpoints + Capabilities + Timestamp.
    /// </summary>
    public required byte[] Signature { get; init; }
    
    /// <summary>
    /// Gets or sets the list of known endpoints for this peer.
    /// May include both public and private addresses.
    /// </summary>
    public required IReadOnlyList<IPEndPoint> Endpoints { get; init; }
    
    /// <summary>
    /// Gets or sets the list of supported capabilities/features.
    /// Examples: "hash_sync", "bittorrent_backend", "multi_source_swarm"
    /// </summary>
    public required IReadOnlyList<string> Capabilities { get; init; }
    
    /// <summary>
    /// Gets or sets the timestamp when this descriptor was created.
    /// Used for freshness checks and replay attack prevention.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// Gets or sets optional metadata (e.g., client version, DHT node ID).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
    
    /// <summary>
    /// Verifies the signature on this descriptor using Ed25519.
    /// </summary>
    /// <returns>True if signature is valid, false otherwise.</returns>
    public bool VerifySignature()
    {
        // If no signature present, treat as invalid (unless in dev mode)
        if (Signature == null || Signature.Length == 0)
        {
            return false; // Require signatures in production
        }
        
        if (PublicKey == null || PublicKey.Length != 32)
        {
            return false;
        }
        
        try
        {
            // Build the signed payload (deterministic)
            var payload = BuildSignaturePayload();
            
            // Verify with NSec
            return LocalMeshIdentityService.Verify(payload, Signature, PublicKey);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Builds the payload that was signed (for verification).
    /// Must be deterministic and match the signing process.
    /// </summary>
    private byte[] BuildSignaturePayload()
    {
        // Signature covers: MeshPeerId + Endpoints + Capabilities + Timestamp
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        
        // Write mesh peer ID
        writer.Write(MeshPeerId.Value);
        
        // Write endpoints (sorted for determinism)
        var sortedEndpoints = Endpoints.OrderBy(e => e.ToString()).ToList();
        writer.Write(sortedEndpoints.Count);
        foreach (var endpoint in sortedEndpoints)
        {
            writer.Write(endpoint.ToString());
        }
        
        // Write capabilities (sorted for determinism)
        var sortedCapabilities = Capabilities.OrderBy(c => c).ToList();
        writer.Write(sortedCapabilities.Count);
        foreach (var capability in sortedCapabilities)
        {
            writer.Write(capability);
        }
        
        // Write timestamp
        writer.Write(Timestamp.ToUnixTimeSeconds());
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// Checks if this descriptor is still fresh (not expired).
    /// </summary>
    /// <param name="maxAge">Maximum age to consider fresh.</param>
    /// <returns>True if fresh, false if expired.</returns>
    public bool IsFresh(TimeSpan maxAge)
    {
        return DateTimeOffset.UtcNow - Timestamp < maxAge;
    }
}














