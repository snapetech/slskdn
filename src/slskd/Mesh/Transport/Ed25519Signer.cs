// <copyright file="Ed25519Signer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;

namespace slskd.Mesh.Transport;

/// <summary>
/// Ed25519 signing and verification implementation.
/// Uses NSec or BouncyCastle in production - this is a placeholder.
/// </summary>
public class Ed25519Signer : IDisposable
{
    // Note: This is a placeholder implementation.
    // In production, you would use:
    // - NSec library: https://github.com/NSec/NSec
    // - BouncyCastle: https://www.bouncycastle.org/
    // - .NET 8+ built-in Ed25519 types

    private readonly ECDsa _ecdsa;

    public Ed25519Signer()
    {
        // Placeholder: Using ECDSA with P-256 curve as a substitute
        // This provides similar security properties but is NOT Ed25519
        // Replace with proper Ed25519 implementation for production
        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    /// <summary>
    /// Generates a new Ed25519 key pair.
    /// </summary>
    /// <returns>Tuple of (privateKey, publicKey) as byte arrays.</returns>
    public (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        // Placeholder: Generate ECDSA key pair
        // In production, generate proper Ed25519 keys
        var privateKey = _ecdsa.ExportECPrivateKey();
        var publicKey = _ecdsa.ExportSubjectPublicKeyInfo();

        return (privateKey, publicKey);
    }

    /// <summary>
    /// Signs data with Ed25519.
    /// </summary>
    /// <param name="data">The data to sign.</param>
    /// <param name="privateKey">The private key.</param>
    /// <returns>The signature.</returns>
    public byte[] Sign(byte[] data, byte[] privateKey)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        if (privateKey == null || privateKey.Length == 0)
        {
            throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
        }

        // Placeholder: Import private key and sign
        // In production, use proper Ed25519 signing
        try
        {
            _ecdsa.ImportECPrivateKey(privateKey, out _);
            return _ecdsa.SignData(data, HashAlgorithmName.SHA256);
        }
        catch (Exception ex)
        {
            throw new CryptographicException("Failed to sign data", ex);
        }
    }

    /// <summary>
    /// Verifies an Ed25519 signature.
    /// </summary>
    /// <param name="data">The original data.</param>
    /// <param name="signature">The signature to verify.</param>
    /// <param name="publicKey">The public key.</param>
    /// <returns>True if the signature is valid.</returns>
    public bool Verify(byte[] data, byte[] signature, byte[] publicKey)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        if (signature == null || signature.Length == 0)
        {
            throw new ArgumentException("Signature cannot be null or empty", nameof(signature));
        }

        if (publicKey == null || publicKey.Length == 0)
        {
            throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));
        }

        // Placeholder: Import public key and verify
        // In production, use proper Ed25519 verification
        try
        {
            _ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return _ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }
        catch (Exception)
        {
            // If verification fails due to key format issues, return false
            return false;
        }
    }

    /// <summary>
    /// Derives a self-certifying PeerId from an Ed25519 public key.
    /// </summary>
    /// <param name="publicKey">The Ed25519 public key.</param>
    /// <returns>The PeerId as a string.</returns>
    public static string DerivePeerId(byte[] publicKey)
    {
        if (publicKey == null || publicKey.Length != 32)
        {
            throw new ArgumentException("Ed25519 public key must be 32 bytes", nameof(publicKey));
        }

        // Hash the public key to create a self-certifying identifier
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(publicKey);

        // Take first 20 bytes and encode as base32 for human readability
        // This creates a 32-character identifier
        var peerIdBytes = hash.Take(20).ToArray();
        return Base32Encode(peerIdBytes).ToLowerInvariant();
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new System.Text.StringBuilder();

        var bits = 0;
        var bitCount = 0;

        foreach (var b in data)
        {
            bits = (bits << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                bitCount -= 5;
                result.Append(alphabet[(bits >> bitCount) & 31]);
            }
        }

        if (bitCount > 0)
        {
            result.Append(alphabet[(bits << (5 - bitCount)) & 31]);
        }

        return result.ToString();
    }

    public void Dispose()
    {
        _ecdsa?.Dispose();
    }
}

