// <copyright file="CertificatePinManagerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class CertificatePinManagerTests : IDisposable
{
    private readonly Mock<ILogger<CertificatePinManager>> _loggerMock;
    private readonly Mock<IOptionsMonitor<MeshOptions>> _optionsMock;
    private readonly string _tempDir;
    private readonly CertificatePinManager _pinManager;

    public CertificatePinManagerTests()
    {
        _loggerMock = new Mock<ILogger<CertificatePinManager>>();
        _optionsMock = new Mock<IOptionsMonitor<MeshOptions>>();
        _optionsMock.Setup(x => x.CurrentValue).Returns(new MeshOptions { DataDirectory = "test-data" });

        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        // Override the data directory to use our temp dir
        var options = new MeshOptions { DataDirectory = _tempDir };
        _optionsMock.Setup(x => x.CurrentValue).Returns(options);

        _pinManager = new CertificatePinManager(_loggerMock.Object, _optionsMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void ValidateCertificatePin_NewPeer_AcceptsAndPinsCertificate()
    {
        // Arrange
        var peerId = "peer:test:new";
        var cert = CreateTestCertificate();

        // Act
        var result = _pinManager.ValidateCertificatePin(peerId, cert);

        // Assert
        Assert.True(result);
        var certInfo = _pinManager.GetPeerCertificateInfo(peerId);
        Assert.NotNull(certInfo);
        Assert.Single(certInfo!.CurrentPins);
    }

    [Fact]
    public void ValidateCertificatePin_ExistingPeer_AcceptsValidPin()
    {
        // Arrange
        var peerId = "peer:test:existing";
        var cert = CreateTestCertificate();

        // First validation pins the certificate
        _pinManager.ValidateCertificatePin(peerId, cert);

        // Act - Second validation with same cert
        var result = _pinManager.ValidateCertificatePin(peerId, cert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateCertificatePin_PinMismatch_RejectsCertificate()
    {
        // Arrange
        var peerId = "peer:test:mismatch";
        var cert1 = CreateTestCertificate();
        var cert2 = CreateTestCertificate(); // Different certificate

        // Pin first certificate
        _pinManager.ValidateCertificatePin(peerId, cert1);

        // Act - Try to validate with different certificate
        var result = _pinManager.ValidateCertificatePin(peerId, cert2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCertificatePin_PreviousPinDuringTransition_AcceptsCertificate()
    {
        // Arrange
        var peerId = "peer:test:transition";
        var oldCert = CreateTestCertificate();
        var newCert = CreateTestCertificate();

        // Pin old certificate
        _pinManager.ValidateCertificatePin(peerId, oldCert);

        // Rotate to new certificate
        _pinManager.RotatePin(peerId, SecurityUtils.ExtractSpkiPin(newCert)!);

        // Act - Validate with old certificate (should still work during transition)
        var result = _pinManager.ValidateCertificatePin(peerId, oldCert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateCertificatePin_ExpiredPreviousPin_RejectsCertificate()
    {
        // Arrange
        var peerId = "peer:test:expired";
        var oldCert = CreateTestCertificate();
        var newCert = CreateTestCertificate();

        // Pin old certificate
        _pinManager.ValidateCertificatePin(peerId, oldCert);

        // Rotate and simulate expiration
        _pinManager.RotatePin(peerId, SecurityUtils.ExtractSpkiPin(newCert)!);

        // Manually expire the previous pin by setting old rotation date
        var certInfo = _pinManager.GetPeerCertificateInfo(peerId);
        if (certInfo != null)
        {
            typeof(PeerCertificateInfo)
                .GetProperty("LastRotation")!
                .SetValue(certInfo, DateTimeOffset.UtcNow.AddDays(-31));
        }

        // Act - Try to validate with expired previous certificate
        var result = _pinManager.ValidateCertificatePin(peerId, oldCert);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RemovePeerPins_RemovesAllPinData()
    {
        // Arrange
        var peerId = "peer:test:remove";
        var cert = CreateTestCertificate();

        _pinManager.ValidateCertificatePin(peerId, cert);

        // Act
        _pinManager.RemovePeerPins(peerId);

        // Assert
        var certInfo = _pinManager.GetPeerCertificateInfo(peerId);
        Assert.Null(certInfo);
    }

    [Fact]
    public void CleanupExpiredPins_RemovesOldPreviousPins()
    {
        // Arrange
        var peerId = "peer:test:cleanup";
        var cert = CreateTestCertificate();

        _pinManager.ValidateCertificatePin(peerId, cert);
        _pinManager.RotatePin(peerId, SecurityUtils.ExtractSpkiPin(CreateTestCertificate())!);

        // Manually set old rotation date
        var certInfo = _pinManager.GetPeerCertificateInfo(peerId);
        if (certInfo != null)
        {
            typeof(PeerCertificateInfo)
                .GetProperty("LastRotation")!
                .SetValue(certInfo, DateTimeOffset.UtcNow.AddDays(-31));
        }

        // Act
        _pinManager.CleanupExpiredPins();

        // Assert
        certInfo = _pinManager.GetPeerCertificateInfo(peerId);
        Assert.NotNull(certInfo);
        Assert.Empty(certInfo!.PreviousPins); // Previous pins should be cleaned up
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        var cert1 = CreateTestCertificate();
        var cert2 = CreateTestCertificate();

        _pinManager.ValidateCertificatePin("peer1", cert1);
        _pinManager.ValidateCertificatePin("peer2", cert2);

        // Act
        var stats = _pinManager.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalPeers);
        Assert.Equal(2, stats.PeersWithCurrentPins);
        Assert.Equal(0, stats.PeersWithPreviousPins);
        Assert.Equal(2, stats.TotalCurrentPins);
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=TestCertificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return cert;
    }
}


