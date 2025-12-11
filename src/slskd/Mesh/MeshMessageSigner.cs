// <copyright file="MeshMessageSigner.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Mesh
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using NSec.Cryptography;
    using slskd.Mesh.Messages;
    using slskd.Mesh.Overlay;

    /// <summary>
    ///     Signs and verifies mesh sync messages using Ed25519.
    ///     SECURITY: Prevents message forgery and impersonation attacks.
    /// </summary>
    public interface IMeshMessageSigner
    {
        /// <summary>
        ///     Signs a mesh message with Ed25519 signature.
        /// </summary>
        /// <param name="message">The message to sign.</param>
        /// <returns>The signed message (with publicKey, signature, timestamp fields populated).</returns>
        MeshMessage SignMessage(MeshMessage message);

        /// <summary>
        ///     Verifies the signature on a mesh message.
        /// </summary>
        /// <param name="message">The message to verify.</param>
        /// <returns>True if signature is valid, false otherwise.</returns>
        bool VerifyMessage(MeshMessage message);
    }

    /// <summary>
    ///     Implementation of mesh message signing using Ed25519.
    /// </summary>
    public class MeshMessageSigner : IMeshMessageSigner
    {
        private readonly IKeyStore keyStore;
        private readonly ILogger<MeshMessageSigner> logger;

        public MeshMessageSigner(IKeyStore keyStore, ILogger<MeshMessageSigner> logger)
        {
            this.keyStore = keyStore;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public MeshMessage SignMessage(MeshMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                var keyPair = keyStore.Current;
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Set timestamp and public key
                message.TimestampUnixMs = timestamp;
                message.PublicKey = keyPair.PublicKeyBase64;

                // Build signable payload: type|timestamp|payload_json
                // Note: We serialize the message WITHOUT signature/publicKey fields for signing
                var payloadJson = SerializeMessageForSigning(message);
                var signablePayload = $"{message.Type}|{timestamp}|{payloadJson}";
                var payloadBytes = Encoding.UTF8.GetBytes(signablePayload);

                // Sign using Ed25519
                using var key = Key.Import(SignatureAlgorithm.Ed25519, keyPair.PrivateKey, KeyBlobFormat.RawPrivateKey);
                var signature = SignatureAlgorithm.Ed25519.Sign(key, payloadBytes);

                // Set signature (Base64-encoded)
                message.Signature = Convert.ToBase64String(signature);

                logger.LogDebug("[MeshSigner] Signed {Type} message", message.Type);
                return message;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[MeshSigner] Failed to sign message");
                throw;
            }
        }

        /// <inheritdoc/>
        public bool VerifyMessage(MeshMessage message)
        {
            if (message == null)
            {
                logger.LogWarning("[MeshSigner] Cannot verify null message");
                return false;
            }

            // Check required fields
            if (string.IsNullOrWhiteSpace(message.PublicKey))
            {
                logger.LogWarning("[MeshSigner] Message missing public key");
                return false;
            }

            if (string.IsNullOrWhiteSpace(message.Signature))
            {
                logger.LogWarning("[MeshSigner] Message missing signature");
                return false;
            }

            if (message.TimestampUnixMs == 0)
            {
                logger.LogWarning("[MeshSigner] Message missing timestamp");
                return false;
            }

            try
            {
                // Check timestamp freshness (reject messages older than 1 hour or in future)
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var age = Math.Abs(now - message.TimestampUnixMs);
                if (age > 3600_000) // 1 hour in milliseconds
                {
                    logger.LogWarning("[MeshSigner] Message timestamp too old or in future: age={Age}ms", age);
                    return false;
                }

                // Import public key
                var publicKeyBytes = Convert.FromBase64String(message.PublicKey);
                if (publicKeyBytes.Length != 32)
                {
                    logger.LogWarning("[MeshSigner] Invalid public key length: {Length}", publicKeyBytes.Length);
                    return false;
                }

                var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);

                // Import signature
                var signatureBytes = Convert.FromBase64String(message.Signature);
                if (signatureBytes.Length != 64)
                {
                    logger.LogWarning("[MeshSigner] Invalid signature length: {Length}", signatureBytes.Length);
                    return false;
                }

                // Build signable payload (same as signing)
                var payloadJson = SerializeMessageForSigning(message);
                var signablePayload = $"{message.Type}|{message.TimestampUnixMs}|{payloadJson}";
                var payloadBytes = Encoding.UTF8.GetBytes(signablePayload);

                // Verify signature
                var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);

                if (!isValid)
                {
                    logger.LogWarning("[MeshSigner] Signature verification failed for {Type} message", message.Type);
                }
                else
                {
                    logger.LogDebug("[MeshSigner] Signature verified for {Type} message", message.Type);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MeshSigner] Error verifying message signature");
                return false;
            }
        }

        /// <summary>
        ///     Serializes a message for signing (excludes signature/publicKey/timestamp fields).
        ///     SECURITY: The signature covers the message content, not the signature itself.
        /// </summary>
        private static string SerializeMessageForSigning(MeshMessage message)
        {
            // Use JsonDocument to remove signature fields
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            };

            var fullJson = JsonSerializer.Serialize(message, message.GetType(), options);
            using var doc = JsonDocument.Parse(fullJson);
            var root = doc.RootElement;

            // Rebuild JSON without signature fields
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();

            foreach (var prop in root.EnumerateObject())
            {
                // Skip signature fields
                if (prop.Name.Equals("publicKey", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("signature", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("timestampMs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}

