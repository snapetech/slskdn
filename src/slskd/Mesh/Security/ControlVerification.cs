// <copyright file="ControlVerification.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using slskd.Mesh.Overlay;

/// <summary>
/// Verifies control envelopes against known peer public keys.
/// Unlike KeyedSigner.Verify, this does NOT trust the self-asserted PublicKey in the envelope.
/// </summary>
public interface IControlVerification
{
    /// <summary>
    /// Verifies a control envelope signature against a set of allowed public keys for a peer.
    /// </summary>
    /// <param name="envelope">The envelope to verify.</param>
    /// <param name="allowedPublicKeys">List of allowed Ed25519 public keys (32 bytes each) for the peer.</param>
    /// <returns>True if signature is valid with any allowed key.</returns>
    bool Verify(ControlEnvelope envelope, IReadOnlyList<byte[]> allowedPublicKeys);
}

public class ControlVerification : IControlVerification
{
    private readonly ILogger<ControlVerification> _logger;

    public ControlVerification(ILogger<ControlVerification> logger)
    {
        _logger = logger;
    }

    public bool Verify(ControlEnvelope envelope, IReadOnlyList<byte[]> allowedPublicKeys)
    {
        if (allowedPublicKeys == null || allowedPublicKeys.Count == 0)
        {
            _logger.LogWarning("[ControlVerification] No allowed public keys provided for verification");
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Signature))
        {
            _logger.LogWarning("[ControlVerification] Envelope has no signature");
            return false;
        }

        try
        {
            // Parse signature
            var signatureBytes = Convert.FromBase64String(envelope.Signature);
            if (signatureBytes.Length != 64)
            {
                _logger.LogWarning("[ControlVerification] Invalid signature length: {Length}", signatureBytes.Length);
                return false;
            }

            // Build signable payload (must match KeyedSigner.BuildSignablePayload)
            var payload = $"{envelope.Type}|{envelope.TimestampUnixMs}|{envelope.MessageId}|{Convert.ToBase64String(envelope.Payload)}";
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            // Try each allowed public key (to support key rotation)
            foreach (var publicKeyBytes in allowedPublicKeys)
            {
                if (publicKeyBytes.Length != 32)
                {
                    _logger.LogDebug("[ControlVerification] Skipping invalid public key (length: {Length})", publicKeyBytes.Length);
                    continue;
                }

                try
                {
                    var publicKey = PublicKey.Import(
                        SignatureAlgorithm.Ed25519,
                        publicKeyBytes,
                        KeyBlobFormat.RawPublicKey);

                    if (SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes))
                    {
                        // Optional: log which key was used if SignerKeyId is present
                        if (!string.IsNullOrWhiteSpace(envelope.SignerKeyId))
                        {
                            _logger.LogDebug("[ControlVerification] Verified envelope with SignerKeyId: {KeyId}", envelope.SignerKeyId);
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[ControlVerification] Failed to verify with one key, trying next");
                }
            }

            _logger.LogWarning("[ControlVerification] Signature did not match any allowed keys ({Count} tried)", allowedPublicKeys.Count);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ControlVerification] Signature verification failed");
            return false;
        }
    }
}

