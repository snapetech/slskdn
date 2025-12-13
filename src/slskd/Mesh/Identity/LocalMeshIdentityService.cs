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
        
        // SECURITY: Ensure permissions are correct on existing keys (in case they were created before this fix)
        SetFilePermissions(_keyPath);
        
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

        // Ensure parent directory exists with secure permissions
        var parentDir = Path.GetDirectoryName(_keyPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
            SetDirectoryPermissions(parentDir);
        }

        // Save to file (private:public format)
        var keyData = $"{Convert.ToBase64String(privateKeyBytes)}:{Convert.ToBase64String(_publicKey)}";
        File.WriteAllText(_keyPath, keyData);
        
        // SECURITY: Set restrictive file permissions (owner read/write only)
        SetFilePermissions(_keyPath);
        
        _logger.LogInformation("Generated and saved new Ed25519 keypair to {Path}", _keyPath);
    }
    
    /// <summary>
    /// Sets secure file permissions (0600 on Unix, restricted ACL on Windows).
    /// </summary>
    private void SetFilePermissions(string filePath)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Unix: chmod 0600 (owner read/write only)
                File.SetUnixFileMode(filePath, 
                    System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite);
                _logger.LogDebug("Set file permissions to 0600 for {Path}", filePath);
            }
            else if (OperatingSystem.IsWindows())
            {
                // Windows: Remove all permissions, then add only current user
                var fileInfo = new FileInfo(filePath);
                var fileSecurity = fileInfo.GetAccessControl();
                
                // Disable inheritance
                fileSecurity.SetAccessRuleProtection(true, false);
                
                // Remove all existing rules
                foreach (System.Security.AccessControl.FileSystemAccessRule rule in fileSecurity.GetAccessRules(true, false, typeof(System.Security.Principal.NTAccount)))
                {
                    fileSecurity.RemoveAccessRule(rule);
                }
                
                // Add rule for current user only
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var userRule = new System.Security.AccessControl.FileSystemAccessRule(
                    identity.User!,
                    System.Security.AccessControl.FileSystemRights.Read | System.Security.AccessControl.FileSystemRights.Write,
                    System.Security.AccessControl.AccessControlType.Allow);
                
                fileSecurity.AddAccessRule(userRule);
                fileInfo.SetAccessControl(fileSecurity);
                _logger.LogDebug("Set Windows ACL for {Path} (current user only)", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set secure file permissions for {Path}", filePath);
        }
    }
    
    /// <summary>
    /// Sets secure directory permissions (0700 on Unix).
    /// </summary>
    private void SetDirectoryPermissions(string dirPath)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Unix: chmod 0700 (owner read/write/execute only)
                File.SetUnixFileMode(dirPath,
                    System.IO.UnixFileMode.UserRead | 
                    System.IO.UnixFileMode.UserWrite | 
                    System.IO.UnixFileMode.UserExecute);
                _logger.LogDebug("Set directory permissions to 0700 for {Path}", dirPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set secure directory permissions for {Path}", dirPath);
        }
    }
}















