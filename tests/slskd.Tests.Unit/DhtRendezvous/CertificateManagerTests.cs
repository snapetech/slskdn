// <copyright file="CertificateManagerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous.Security;
using Xunit;

public class CertificateManagerTests
{
    [Fact]
    public void GetOrCreateServerCertificate_WhenLegacyPasswordFileExists_RegeneratesWithoutReadingCleartextPassword()
    {
        using var tempDir = new TempDir();
        var certPath = Path.Combine(tempDir.Path, "overlay_cert.pfx");
        var legacyKeyPath = Path.Combine(tempDir.Path, "overlay_cert.key");

        var legacyPassword = "legacy-password-that-should-not-be-read";
        using var legacyCertificate = CreateLegacyPasswordProtectedCertificate(legacyPassword);
        var legacyThumbprint = legacyCertificate.GetCertHashString(HashAlgorithmName.SHA256);
        File.WriteAllBytes(certPath, legacyCertificate.Export(X509ContentType.Pfx, legacyPassword));
        File.Create(legacyKeyPath).Dispose();

        var manager = new CertificateManager(NullLogger<CertificateManager>.Instance, tempDir.Path);

        using var certificate = manager.GetOrCreateServerCertificate();

        Assert.False(File.Exists(legacyKeyPath));
        Assert.NotEqual(legacyThumbprint, certificate.GetCertHashString(HashAlgorithmName.SHA256));
        Assert.True(File.Exists(certPath));
    }

    private static X509Certificate2 CreateLegacyPasswordProtectedCertificate(string password)
    {
        using var rsa = RSA.Create(CertificateManager.KeySize);
        var request = new CertificateRequest(
            "CN=legacy-slskdn-overlay,O=slskdn",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, password),
            password,
            X509KeyStorageFlags.Exportable,
            new Pkcs12LoaderLimits());
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"slskdn-cert-tests-{System.Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
            {
                System.IO.Directory.Delete(Path, recursive: true);
            }
        }
    }
}
