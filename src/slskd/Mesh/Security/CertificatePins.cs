// <copyright file="CertificatePins.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Utilities for computing SPKI (Subject Public Key Info) hashes for certificate pinning.
/// </summary>
public static class CertificatePins
{
    /// <summary>
    /// Computes the SHA-256 hash of the certificate's SPKI.
    /// This is used for certificate pinning.
    /// </summary>
    /// <param name="cert">The certificate to hash.</param>
    /// <returns>SHA-256 hash of the SPKI (32 bytes).</returns>
    public static byte[] ComputeSpkiSha256(X509Certificate2 cert)
    {
        // Try ECDSA first (preferred)
        var ecdsa = cert.GetECDsaPublicKey();
        if (ecdsa != null)
        {
            var spki = ecdsa.ExportSubjectPublicKeyInfo();
            return SHA256.HashData(spki);
        }

        // Fallback to RSA
        var rsa = cert.GetRSAPublicKey();
        if (rsa != null)
        {
            var spki = rsa.ExportSubjectPublicKeyInfo();
            return SHA256.HashData(spki);
        }

        throw new InvalidOperationException("Certificate has no supported public key (ECDSA or RSA)");
    }

    /// <summary>
    /// Computes the SHA-256 hash of the certificate's SPKI as a base64 string.
    /// </summary>
    /// <param name="cert">The certificate to hash.</param>
    /// <returns>Base64-encoded SHA-256 hash of the SPKI.</returns>
    public static string ComputeSpkiSha256Base64(X509Certificate2 cert)
    {
        return Convert.ToBase64String(ComputeSpkiSha256(cert));
    }
}

