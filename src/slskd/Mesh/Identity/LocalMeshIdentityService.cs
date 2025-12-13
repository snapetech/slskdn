// <copyright file="LocalMeshIdentityService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

/// <summary>
/// Manages the local node's Ed25519 keypair and derived mesh peer ID.
/// This is the node's permanent mesh identity.
/// </summary>
public sealed class LocalMeshIdentityService
{
    private readonly ILogger<LocalMeshIdentityService> _logger;
    private readonly string _keyPath;
    private Key? _key;
    private byte[]? _publicKey;
    private MeshPeerId? _meshPeerId;

    public LocalMeshIdentityService(ILogger<LocalMeshIdentityService> logger, string keyPath)
    {
        _logger = logger;
        _keyPath = keyPath;
        InitializeKeys();
    }

    /// <summary>
    /// Gets the local mesh peer ID.
    /// </summary>
    public MeshPeerId MeshPeerId => _meshPeerId ?? throw new InvalidOperationException("Keys not initialized");

    /// <summary>
    /// Gets the public key for this node.
    /// </summary>
    public byte[] PublicKey => _publicKey ?? throw new InvalidOperationException("Keys not initialized");

    /// <summary>
    /// Signs data with the local private key using Ed25519.
    /// </summary>
    public byte[] Sign(byte[] data)
    {
        if (_key == null)
        {
            throw new InvalidOperationException("Private key not loaded");
        }

        return SignatureAlgorithm.Ed25519.Sign(_key, data);
    }

    /// <summary>
    /// Verifies a signature against a public key using Ed25519.
    /// </summary>
    public static bool Verify(byte[] data, byte[] signature, byte[] publicKey)
    {
        try
        {
            var pubKey = NSec.Cryptography.PublicKey.Import(
                SignatureAlgorithm.Ed25519,
                publicKey,
                KeyBlobFormat.RawPublicKey);
            
            return SignatureAlgorithm.Ed25519.Verify(pubKey, data, signature);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Creates a signed MeshPeerDescriptor for this node.
    /// </summary>
    public MeshPeerDescriptor CreateSignedDescriptor(
        System.Collections.Generic.IReadOnlyList<System.Net.IPEndPoint> endpoints,
        System.Collections.Generic.IReadOnlyList<string> capabilities)
    {
        // Create unsigned descriptor first
        var timestamp = DateTimeOffset.UtcNow;
        var descriptor = new MeshPeerDescriptor
        {
            MeshPeerId = MeshPeerId,
            PublicKey = PublicKey,
            Signature = Array.Empty<byte>(), // Temporary
            Endpoints = endpoints,
            Capabilities = capabilities,
            Timestamp = timestamp,
        };
        
        // Build the payload to sign
        var payload = BuildDescriptorPayload(descriptor);
        
        // Sign it
        var signature = Sign(payload);
        
        // Create new descriptor with signature
        return new MeshPeerDescriptor
        {
            MeshPeerId = MeshPeerId,
            PublicKey = PublicKey,
            Signature = signature,
            Endpoints = endpoints,
            Capabilities = capabilities,
            Timestamp = timestamp,
        };
    }
    
    private static byte[] BuildDescriptorPayload(MeshPeerDescriptor descriptor)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        
        writer.Write(descriptor.MeshPeerId.Value);
        
        var sortedEndpoints = descriptor.Endpoints.OrderBy(e => e.ToString()).ToList();
        writer.Write(sortedEndpoints.Count);
        foreach (var endpoint in sortedEndpoints)
        {
            writer.Write(endpoint.ToString());
        }
        
        var sortedCapabilities = descriptor.Capabilities.OrderBy(c => c).ToList();
        writer.Write(sortedCapabilities.Count);
        foreach (var capability in sortedCapabilities)
        {
            writer.Write(capability);
        }
        
        writer.Write(descriptor.Timestamp.ToUnixTimeSeconds());
        
        return ms.ToArray();
    }

    private void InitializeKeys()
    {
        if (File.Exists(_keyPath))
        {
            try
            {
                LoadKeys();
                _logger.LogInformation("Loaded existing mesh identity: {PeerId}", _meshPeerId?.ToShortString());
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing keys, generating new ones");
            }
        }

        GenerateAndSaveKeys();
        _logger.LogInformation("Generated new mesh identity: {PeerId}", _meshPeerId?.ToShortString());
    }

    private void LoadKeys()
    {
        var keyData = File.ReadAllText(_keyPath);
        var parts = keyData.Split(':');
        
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Invalid key file format");
        }

        var privateKeyBytes = Convert.FromBase64String(parts[0]);
        _publicKey = Convert.FromBase64String(parts[1]);

        if (privateKeyBytes.Length != 32 || _publicKey.Length != 32)
        {
            throw new InvalidOperationException("Invalid key lengths (expected 32 bytes each)");
        }

        // Import Ed25519 key from raw bytes
        _key = Key.Import(
            SignatureAlgorithm.Ed25519,
            privateKeyBytes,
            KeyBlobFormat.RawPrivateKey);

        _meshPeerId = MeshPeerId.FromPublicKey(_publicKey);
        
        _logger.LogDebug("Loaded Ed25519 keypair from {Path}", _keyPath);
    }

    private void GenerateAndSaveKeys()
    {
        // Generate proper Ed25519 keypair with NSec
        var creationParams = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport, // Allow export for storage
        };
        
        _key = Key.Create(SignatureAlgorithm.Ed25519, creationParams);
        
        // Export keys
        var privateKeyBytes = _key.Export(KeyBlobFormat.RawPrivateKey);
        _publicKey = _key.Export(KeyBlobFormat.RawPublicKey);

        _meshPeerId = MeshPeerId.FromPublicKey(_publicKey);

        // Save to file (private:public format)
        var keyData = $"{Convert.ToBase64String(privateKeyBytes)}:{Convert.ToBase64String(_publicKey)}";
        File.WriteAllText(_keyPath, keyData);
        
        _logger.LogInformation("Generated and saved new Ed25519 keypair to {Path}", _keyPath);
    }
}















