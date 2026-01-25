// <copyright file="KeyedSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Signs and verifies control envelopes using Ed25519.
/// </summary>
public interface IControlSigner
{
    ControlEnvelope Sign(ControlEnvelope envelope);
    bool Verify(ControlEnvelope envelope);
}

public class ControlSigner : IControlSigner
{
    private readonly ILogger<ControlSigner> logger;
    private readonly IKeyStore keyStore;

    public ControlSigner(ILogger<ControlSigner> logger, IKeyStore keyStore)
    {
        this.logger = logger;
        this.keyStore = keyStore;
    }

    public ControlEnvelope Sign(ControlEnvelope envelope)
    {
        envelope.PublicKey = keyStore.Current.PublicKeyBase64;
        envelope.Signature = ComputeSignature(envelope, keyStore.Current.PrivateKey);
        return envelope;
    }

    public bool Verify(ControlEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.PublicKey) || string.IsNullOrWhiteSpace(envelope.Signature))
        {
            return false;
        }

        try
        {
            // Import public key from base64
            var publicKeyBytes = Convert.FromBase64String(envelope.PublicKey);
            if (publicKeyBytes.Length != 32)
            {
                logger.LogWarning("[ControlSigner] Invalid public key length: {Length}", publicKeyBytes.Length);
                return false;
            }

            // Import signature from base64
            var signatureBytes = Convert.FromBase64String(envelope.Signature);
            if (signatureBytes.Length != 64)
            {
                logger.LogWarning("[ControlSigner] Invalid signature length: {Length}", signatureBytes.Length);
                return false;
            }

            // Verify: try canonical (GetSignableData) then legacy for backward compatibility
            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var signableData = envelope.GetSignableData();
            if (SignatureAlgorithm.Ed25519.Verify(publicKey, signableData, signatureBytes))
            {
                return true;
            }

            var legacyData = envelope.GetLegacySignableData();
            return SignatureAlgorithm.Ed25519.Verify(publicKey, legacyData, signatureBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ControlSigner] Signature verification failed");
            return false;
        }
    }

    private string ComputeSignature(ControlEnvelope envelope, byte[] privateKey)
    {
        try
        {
            var payloadBytes = envelope.GetSignableData();

            // Import private key and sign using NSec (libsodium)
            using var key = Key.Import(SignatureAlgorithm.Ed25519, privateKey, KeyBlobFormat.RawPrivateKey);
            var signature = SignatureAlgorithm.Ed25519.Sign(key, payloadBytes);
            
            return Convert.ToBase64String(signature);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ControlSigner] Failed to compute signature");
            throw;
        }
    }
}
