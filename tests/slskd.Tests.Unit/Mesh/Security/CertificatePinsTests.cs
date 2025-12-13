// <copyright file="CertificatePinsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using slskd.Mesh.Security;
using Xunit;

public class CertificatePinsTests
{
    [Fact]
    public void ComputeSpkiSha256_IsDeterministic()
    {
        // Arrange
        var cert = CreateTestCertificate();

        // Act
        var hash1 = CertificatePins.ComputeSpkiSha256(cert);
        var hash2 = CertificatePins.ComputeSpkiSha256(cert);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeSpkiSha256_Returns32Bytes()
    {
        // Arrange
        var cert = CreateTestCertificate();

        // Act
        var hash = CertificatePins.ComputeSpkiSha256(cert);

        // Assert - SHA256 produces 32 bytes
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void ComputeSpkiSha256Base64_ReturnsValidBase64()
    {
        // Arrange
        var cert = CreateTestCertificate();

        // Act
        var hash = CertificatePins.ComputeSpkiSha256Base64(cert);

        // Assert - should be valid base64 string
        Assert.NotNull(hash);
        Assert.True(hash.Length > 0);
        
        // Verify it's valid base64 by attempting to decode
        var decoded = Convert.FromBase64String(hash);
        Assert.Equal(32, decoded.Length);
    }

    [Fact]
    public void ComputeSpkiSha256_DifferentForDifferentCerts()
    {
        // Arrange
        var cert1 = CreateTestCertificate();
        var cert2 = CreateTestCertificate(); // Different cert

        // Act
        var hash1 = CertificatePins.ComputeSpkiSha256(cert1);
        var hash2 = CertificatePins.ComputeSpkiSha256(cert2);

        // Assert - different certs should have different hashes
        Assert.NotEqual(hash1, hash2);
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            "CN=test",
            ecdsa,
            HashAlgorithmName.SHA256);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
    }
}

