// <copyright file="PersistentCertificate.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Helper for creating and loading persistent TLS certificates.
/// Certificates are stored as PFX files and reused across restarts.
/// </summary>
public static class PersistentCertificate
{
    /// <summary>
    /// Loads an existing certificate or creates a new one if it doesn't exist.
    /// Uses ECDSA P-256 for smaller key sizes.
    /// </summary>
    /// <param name="path">Path to the PFX file.</param>
    /// <param name="password">Password for the PFX (optional).</param>
    /// <param name="subjectCN">Subject CN for the certificate.</param>
    /// <param name="validityYears">Validity period in years.</param>
    /// <returns>The loaded or created certificate with private key.</returns>
    public static X509Certificate2 LoadOrCreate(
        string path,
        string? password,
        string subjectCN,
        int validityYears = 5)
    {
        if (File.Exists(path))
        {
            try
            {
                return new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
            }
            catch
            {
                // If load fails, regenerate
                File.Delete(path);
            }
        }

        return CreateAndSave(path, password, subjectCN, validityYears);
    }

    private static X509Certificate2 CreateAndSave(
        string path,
        string? password,
        string subjectCN,
        int validityYears)
    {
        // Create ECDSA P-256 key (preferred over RSA for smaller keys)
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            subjectCN,
            ecdsa,
            HashAlgorithmName.SHA256);

        // Add extensions for TLS server
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement,
                false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // serverAuth
                false));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // Create self-signed certificate
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(validityYears);

        var cert = request.CreateSelfSigned(notBefore, notAfter);

        // Export as PFX
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(path, pfxBytes);

        // Set file permissions on Linux/macOS
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Best effort
            }
        }

        // Return certificate with private key
        return new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
    }
}

