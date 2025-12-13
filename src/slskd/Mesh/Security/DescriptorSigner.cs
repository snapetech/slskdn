// <copyright file="DescriptorSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using slskd.Mesh.Dht;

/// <summary>
/// Signs and verifies MeshPeerDescriptor instances.
/// </summary>
public interface IDescriptorSigner
{
    /// <summary>
    /// Signs a descriptor with the identity private key.
    /// </summary>
    void Sign(MeshPeerDescriptor descriptor, byte[] identityPrivateKey);

    /// <summary>
    /// Verifies a descriptor's signature and PeerId derivation.
    /// </summary>
    bool Verify(MeshPeerDescriptor descriptor);
}

/// <summary>
/// Implementation of descriptor signing and verification.
/// </summary>
public class DescriptorSigner : IDescriptorSigner
{
    private readonly ILogger<DescriptorSigner> logger;

    public DescriptorSigner(ILogger<DescriptorSigner> logger)
    {
        this.logger = logger;
    }

    public void Sign(MeshPeerDescriptor descriptor, byte[] identityPrivateKey)
    {
        var payload = BuildCanonicalPayload(descriptor);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

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
            // Verify PeerId matches identity public key
            var identityPubKey = Convert.FromBase64String(descriptor.IdentityPublicKey);
            var derivedPeerId = ComputePeerId(identityPubKey);
            if (derivedPeerId != descriptor.PeerId)
            {
                logger.LogWarning("[DescriptorSigner] PeerId mismatch: expected {Expected}, got {Actual}",
                    derivedPeerId, descriptor.PeerId);
                return false;
            }

            // Verify signature
            var payload = BuildCanonicalPayload(descriptor);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
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

    private static string BuildCanonicalPayload(MeshPeerDescriptor desc)
    {
        // Canonicalize fields (exclude Signature itself)
        var endpoints = string.Join(",", desc.Endpoints.OrderBy(e => e));
        var signingKeys = string.Join(",", desc.ControlSigningPublicKeys.OrderBy(k => k));

        return $"{desc.PeerId}|{endpoints}|{desc.NatType ?? ""}|{desc.RelayRequired}|" +
               $"{desc.TimestampUnixMs}|{desc.IdentityPublicKey}|" +
               $"{desc.TlsControlSpkiSha256}|{desc.TlsDataSpkiSha256}|{signingKeys}";
    }

    private static string ComputePeerId(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

