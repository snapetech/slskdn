// <copyright file="DescriptorSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Linq;
using System.Security.Cryptography;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using slskd.Mesh.Dht;

/// <summary>
/// Signs and verifies MeshPeerDescriptor instances using canonical MessagePack serialization.
/// </summary>
public interface IDescriptorSigner
{
    /// <summary>
    /// Signs a descriptor with the identity private key.
    /// </summary>
    void Sign(MeshPeerDescriptor descriptor, byte[] identityPrivateKey);

    /// <summary>
    /// Verifies a descriptor's signature, PeerId derivation, expiration, and schema version.
    /// Also checks rotation bounds.
    /// </summary>
    bool Verify(MeshPeerDescriptor descriptor);
}

/// <summary>
/// Implementation of descriptor signing and verification with canonical signing.
/// </summary>
public class DescriptorSigner : IDescriptorSigner
{
    private readonly ILogger<DescriptorSigner> logger;

    // Configuration constants
    private const int MaxControlSigningKeys = 3;
    private const int MaxTlsPins = 2;
    private const int MaxExpirationDays = 30;
    private const int ClockSkewToleranceMinutes = 5;

    public DescriptorSigner(ILogger<DescriptorSigner> logger)
    {
        this.logger = logger;
    }

    public void Sign(MeshPeerDescriptor descriptor, byte[] identityPrivateKey)
    {
        // Validate rotation bounds before signing
        if (descriptor.ControlSigningKeys.Count > MaxControlSigningKeys)
        {
            throw new InvalidOperationException($"Too many control signing keys: {descriptor.ControlSigningKeys.Count} (max {MaxControlSigningKeys})");
        }

        if (descriptor.TlsControlPins.Count > MaxTlsPins)
        {
            throw new InvalidOperationException($"Too many control TLS pins: {descriptor.TlsControlPins.Count} (max {MaxTlsPins})");
        }

        if (descriptor.TlsDataPins.Count > MaxTlsPins)
        {
            throw new InvalidOperationException($"Too many data TLS pins: {descriptor.TlsDataPins.Count} (max {MaxTlsPins})");
        }

        // Canonical signing: serialize DescriptorToSign (excludes Signature)
        var toSign = descriptor.ToSigningPayload();
        var payloadBytes = MessagePackSerializer.Serialize(toSign);

        using var key = Key.Import(SignatureAlgorithm.Ed25519, identityPrivateKey, KeyBlobFormat.RawPrivateKey);
        var signature = SignatureAlgorithm.Ed25519.Sign(key, payloadBytes);

        descriptor.Signature = Convert.ToBase64String(signature);
    }

    public bool Verify(MeshPeerDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.IdentityPublicKey) ||
            string.IsNullOrWhiteSpace(descriptor.Signature))
        {
            logger.LogWarning("[DescriptorSigner] Missing identity or signature");
            return false;
        }

        try
        {
            // 1. Schema version check
            if (descriptor.SchemaVersion != 1)
            {
                logger.LogWarning("[DescriptorSigner] Unsupported schema version: {Version}", descriptor.SchemaVersion);
                return false;
            }

            // 2. Rotation bounds check
            if (descriptor.ControlSigningKeys.Count > MaxControlSigningKeys)
            {
                logger.LogWarning("[DescriptorSigner] Too many control signing keys: {Count}", descriptor.ControlSigningKeys.Count);
                return false;
            }

            if (descriptor.TlsControlPins.Count > MaxTlsPins || descriptor.TlsDataPins.Count > MaxTlsPins)
            {
                logger.LogWarning("[DescriptorSigner] Too many TLS pins");
                return false;
            }

            // 3. Expiration check (with clock skew tolerance)
            var now = DateTimeOffset.UtcNow;
            var issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(descriptor.IssuedAtUnixMs);
            var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(descriptor.ExpiresAtUnixMs);

            if (now.AddMinutes(ClockSkewToleranceMinutes) < issuedAt)
            {
                logger.LogWarning("[DescriptorSigner] Descriptor issued in the future: {IssuedAt}", issuedAt);
                return false;
            }

            if (now > expiresAt.AddMinutes(ClockSkewToleranceMinutes))
            {
                logger.LogWarning("[DescriptorSigner] Descriptor expired: {ExpiresAt}", expiresAt);
                return false;
            }

            var lifetime = expiresAt - issuedAt;
            if (lifetime.TotalDays > MaxExpirationDays)
            {
                logger.LogWarning("[DescriptorSigner] Descriptor lifetime too long: {Days} days", lifetime.TotalDays);
                return false;
            }

            // 4. Verify PeerId matches identity public key
            var identityPubKey = Convert.FromBase64String(descriptor.IdentityPublicKey);
            var derivedPeerId = ComputePeerId(identityPubKey);
            if (derivedPeerId != descriptor.PeerId)
            {
                logger.LogWarning("[DescriptorSigner] PeerId mismatch: expected {Expected}, got {Actual}",
                    derivedPeerId, descriptor.PeerId);
                return false;
            }

            // 5. Verify signature (canonical MessagePack payload)
            var toSign = descriptor.ToSigningPayload();
            var payloadBytes = MessagePackSerializer.Serialize(toSign);
            var signatureBytes = Convert.FromBase64String(descriptor.Signature);

            if (signatureBytes.Length != 64)
            {
                logger.LogWarning("[DescriptorSigner] Invalid signature length: {Length}", signatureBytes.Length);
                return false;
            }

            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, identityPubKey, KeyBlobFormat.RawPublicKey);
            return SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DescriptorSigner] Verification failed");
            return false;
        }
    }

    private static string ComputePeerId(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
