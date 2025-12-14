// <copyright file="PodMembershipSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using slskd.Mesh.Overlay;

/// <summary>
/// Signs and verifies pod membership records using Ed25519.
/// </summary>
public interface IPodMembershipSigner
{
    /// <summary>
    /// Signs a membership record.
    /// </summary>
    Task<SignedMembershipRecord> SignMembershipAsync(
        string podId,
        string peerId,
        string role,
        string action, // "join", "leave", "ban"
        byte[]? signerPrivateKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies a signed membership record.
    /// </summary>
    Task<bool> VerifyMembershipAsync(
        SignedMembershipRecord record,
        byte[] signerPublicKey,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a new Ed25519 keypair for pod membership signing.
    /// </summary>
    Ed25519KeyPair GenerateKeyPair();
}

/// <summary>
/// Implements pod membership signing and verification.
/// </summary>
public class PodMembershipSigner : IPodMembershipSigner
{
    private readonly ILogger<PodMembershipSigner> logger;
    private readonly IKeyStore keyStore;

    public PodMembershipSigner(
        ILogger<PodMembershipSigner> logger,
        IKeyStore keyStore)
    {
        this.logger = logger;
        this.keyStore = keyStore;
    }

    public Task<SignedMembershipRecord> SignMembershipAsync(
        string podId,
        string peerId,
        string role,
        string action,
        byte[]? signerPrivateKey = null,
        CancellationToken ct = default)
    {
        try
        {
            // Use provided key or default from key store
            byte[] privateKey = signerPrivateKey ?? keyStore.Current.PrivateKey;
            byte[] publicKey = signerPrivateKey != null
                ? DerivePublicKey(privateKey)
                : keyStore.Current.PublicKey;

            var record = new SignedMembershipRecord
            {
                PodId = podId,
                PeerId = peerId,
                Role = role,
                Action = action,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PublicKey = Convert.ToBase64String(publicKey),
            };

            // Build signable payload
            var payload = BuildSignablePayload(record);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            // Sign using Ed25519
            using var key = Key.Import(SignatureAlgorithm.Ed25519, privateKey, KeyBlobFormat.RawPrivateKey);
            var signature = SignatureAlgorithm.Ed25519.Sign(key, payloadBytes);

            record.Signature = Convert.ToBase64String(signature);

            logger.LogDebug("[PodMembershipSigner] Signed membership record: {Action} {PeerId} -> {PodId}",
                action, peerId, podId);

            return Task.FromResult(record);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodMembershipSigner] Failed to sign membership record");
            throw;
        }
    }

    public Task<bool> VerifyMembershipAsync(
        SignedMembershipRecord record,
        byte[] signerPublicKey,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(record.Signature) || record.PublicKey == null)
            {
                logger.LogWarning("[PodMembershipSigner] Membership record missing signature or public key");
                return Task.FromResult(false);
            }

            // Verify public key matches
            var recordPublicKey = Convert.FromBase64String(record.PublicKey);
            if (!recordPublicKey.SequenceEqual(signerPublicKey))
            {
                logger.LogWarning("[PodMembershipSigner] Public key mismatch in membership record");
                return Task.FromResult(false);
            }

            // Build signable payload
            var payload = BuildSignablePayload(record);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            // Verify signature
            var signatureBytes = Convert.FromBase64String(record.Signature);
            if (signatureBytes.Length != 64) // Ed25519 signatures are 64 bytes
            {
                logger.LogWarning("[PodMembershipSigner] Invalid signature length: {Length}", signatureBytes.Length);
                return Task.FromResult(false);
            }

            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, signerPublicKey, KeyBlobFormat.RawPublicKey);
            var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);

            if (!isValid)
            {
                logger.LogWarning("[PodMembershipSigner] Signature verification failed for membership record");
            }

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PodMembershipSigner] Error verifying membership record");
            return Task.FromResult(false);
        }
    }

    public Ed25519KeyPair GenerateKeyPair()
    {
        return Ed25519KeyPair.Generate();
    }

    private static byte[] DerivePublicKey(byte[] privateKey)
    {
        // Ed25519: public key can be derived from private key
        using var key = Key.Import(SignatureAlgorithm.Ed25519, privateKey, KeyBlobFormat.RawPrivateKey);
        return key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
    }

    private static string BuildSignablePayload(SignedMembershipRecord record)
    {
        // Build deterministic payload for signing
        return $"{record.PodId}|{record.PeerId}|{record.Role}|{record.Action}|{record.TimestampUnixMs}";
    }
}

/// <summary>
/// Signed pod membership record.
/// </summary>
public class SignedMembershipRecord
{
    public string PodId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "owner", "mod", "member"
    public string Action { get; set; } = string.Empty; // "join", "leave", "ban"
    public long TimestampUnixMs { get; set; }
    public string PublicKey { get; set; } = string.Empty; // Base64 Ed25519 public key
    public string Signature { get; set; } = string.Empty; // Base64 Ed25519 signature
}
