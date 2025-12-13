// <copyright file="IdentityKeyStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

/// <summary>
/// Stores the stable identity key for this mesh node.
/// This key NEVER rotates and defines the node's permanent PeerId.
/// </summary>
public interface IIdentityKeyStore
{
    /// <summary>
    /// Gets the public key (32 bytes for Ed25519).
    /// </summary>
    byte[] PublicKey { get; }

    /// <summary>
    /// Gets the private key (32 bytes for Ed25519).
    /// </summary>
    byte[] PrivateKey { get; }

    /// <summary>
    /// Computes the stable PeerId from the public key.
    /// PeerId = hex(SHA256(publicKey)).
    /// </summary>
    string ComputePeerId();

    /// <summary>
    /// Signs data with the identity private key.
    /// </summary>
    byte[] Sign(byte[] data);
}

/// <summary>
/// File-based implementation of identity key storage.
/// Keys are persisted to disk and never rotate.
/// </summary>
public class FileIdentityKeyStore : IIdentityKeyStore
{
    private readonly ILogger<FileIdentityKeyStore> logger;
    private readonly string keyPath;
    private byte[] publicKey;
    private byte[] privateKey;

    public FileIdentityKeyStore(ILogger<FileIdentityKeyStore> logger, string keyPath = "mesh-identity.key")
    {
        this.logger = logger;
        this.keyPath = keyPath;
        LoadOrGenerate();
    }

    public byte[] PublicKey => publicKey;

    public byte[] PrivateKey => privateKey;

    public string ComputePeerId()
    {
        var hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public byte[] Sign(byte[] data)
    {
        using var key = Key.Import(SignatureAlgorithm.Ed25519, privateKey, KeyBlobFormat.RawPrivateKey);
        return SignatureAlgorithm.Ed25519.Sign(key, data);
    }

    private void LoadOrGenerate()
    {
        if (!File.Exists(keyPath))
        {
            Generate();
            return;
        }

        try
        {
            var json = File.ReadAllText(keyPath);
            var model = JsonSerializer.Deserialize<KeyFileModel>(json);
            if (model == null)
            {
                throw new InvalidOperationException("Invalid identity key file");
            }

            publicKey = Convert.FromBase64String(model.PublicKey);
            privateKey = Convert.FromBase64String(model.PrivateKey);

            if (publicKey.Length != 32 || privateKey.Length != 32)
            {
                throw new InvalidOperationException("Invalid key lengths");
            }

            logger.LogInformation("[IdentityKeyStore] Loaded identity from {Path}, PeerId={PeerId}",
                keyPath, ComputePeerId());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IdentityKeyStore] Failed to load identity, generating new");
            Generate();
        }
    }

    private void Generate()
    {
        using var key = Key.Create(SignatureAlgorithm.Ed25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        privateKey = key.Export(KeyBlobFormat.RawPrivateKey);

        var model = new KeyFileModel
        {
            PublicKey = Convert.ToBase64String(publicKey),
            PrivateKey = Convert.ToBase64String(privateKey),
            CreatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(keyPath, json);

        // Set file permissions on Linux
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[IdentityKeyStore] Could not set file permissions on {Path}", keyPath);
            }
        }

        logger.LogInformation("[IdentityKeyStore] Generated new identity at {Path}, PeerId={PeerId}",
            keyPath, ComputePeerId());
    }

    private class KeyFileModel
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public long CreatedMs { get; set; }
    }
}

