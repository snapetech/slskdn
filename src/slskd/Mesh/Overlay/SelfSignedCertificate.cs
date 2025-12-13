using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Generates a self-signed certificate for QUIC/TLS usage (ephemeral).
/// </summary>
public static class SelfSignedCertificate
{
    public static X509Certificate2 Create(string subjectName = "CN=mesh-overlay")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return cert;
    }
}
















