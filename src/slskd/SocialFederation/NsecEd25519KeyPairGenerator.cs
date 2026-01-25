// <copyright file="NsecEd25519KeyPairGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Text;
    using NSec.Cryptography;

    /// <summary>
    ///     Ed25519 keypair generator using NSec. Prefers PKIX private key export;
    ///     falls back to Raw if PKIX is not supported (e.g. some NSec builds).
    /// </summary>
    public sealed class NsecEd25519KeyPairGenerator : IEd25519KeyPairGenerator
    {
        /// <inheritdoc/>
        public (string PublicKeyPem, string PrivateKeyPem) GenerateKeypair()
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            using var key = Key.Create(algorithm);

            var publicKeyBytes = key.Export(KeyBlobFormat.PkixPublicKey);
            var publicKeyPem = ToPem(publicKeyBytes, "PUBLIC KEY");

            string privateKeyPem;
            try
            {
                var privateKeyBytes = key.Export(KeyBlobFormat.PkixPrivateKey);
                privateKeyPem = ToPem(privateKeyBytes, "PRIVATE KEY");
            }
            catch (NotSupportedException)
            {
                var rawBytes = key.Export(KeyBlobFormat.RawPrivateKey);
                privateKeyPem = ToPem(rawBytes, "RAW PRIVATE KEY");
            }
            catch (InvalidOperationException)
            {
                try
                {
                    var rawBytes = key.Export(KeyBlobFormat.RawPrivateKey);
                    privateKeyPem = ToPem(rawBytes, "RAW PRIVATE KEY");
                }
                catch (InvalidOperationException ex)
                {
                    throw new NotSupportedException(
                        "NSec Key.Export(PkixPrivateKey and RawPrivateKey) is not supported in this environment. " +
                        "ActivityPub keypair creation cannot proceed.", ex);
                }
            }

            return (publicKeyPem, privateKeyPem);
        }

        private static string ToPem(byte[] keyBytes, string label)
        {
            var base64 = Convert.ToBase64String(keyBytes);
            var pem = new StringBuilder();
            pem.AppendLine($"-----BEGIN {label}-----");
            for (var i = 0; i < base64.Length; i += 64)
            {
                var length = Math.Min(64, base64.Length - i);
                pem.AppendLine(base64.Substring(i, length));
            }

            pem.AppendLine($"-----END {label}-----");
            return pem.ToString();
        }
    }
}
