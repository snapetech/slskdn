// <copyright file="LoggingUtilsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class LoggingUtilsTests
{
    private readonly Mock<ILogger<TestClass>> _loggerMock;

    public LoggingUtilsTests()
    {
        _loggerMock = new Mock<ILogger<TestClass>>();
    }

    [Fact]
    public void SafePeerId_ShortId_ReturnsFullId()
    {
        // Arrange
        var shortId = "abc";

        // Act
        var result = LoggingUtils.SafePeerId(shortId);

        // Assert
        Assert.Equal(shortId, result);
    }

    [Fact]
    public void SafePeerId_LongId_ReturnsTruncatedId()
    {
        // Arrange
        var longId = "very-long-peer-identifier-12345";

        // Act
        var result = LoggingUtils.SafePeerId(longId);

        // Assert
        Assert.Equal("very...2345", result);
    }

    [Fact]
    public void SafePeerId_NullOrEmpty_ReturnsNull()
    {
        // Act & Assert
        Assert.Equal("[null]", LoggingUtils.SafePeerId(null));
        Assert.Equal("[null]", LoggingUtils.SafePeerId(string.Empty));
    }

    [Fact]
    public void SafeEndpoint_Localhost_ReturnsFullEndpoint()
    {
        // Arrange
        var localhost = "127.0.0.1:8080";

        // Act
        var result = LoggingUtils.SafeEndpoint(localhost);

        // Assert
        Assert.Equal(localhost, result);
    }

    [Fact]
    public void SafeEndpoint_PublicIP_ReturnsRedacted()
    {
        // Arrange
        var publicIp = "192.168.1.100:8080";

        // Act
        var result = LoggingUtils.SafeEndpoint(publicIp);

        // Assert
        Assert.Equal("xxx.xxx.xxx.100:8080", result);
    }

    [Fact]
    public void SafeEndpoint_OnionAddress_ReturnsFullAddress()
    {
        // Arrange
        var onionAddr = "abcdefghijklmnop.onion:8080";

        // Act
        var result = LoggingUtils.SafeEndpoint(onionAddr);

        // Assert
        Assert.Equal(onionAddr, result);
    }

    [Fact]
    public void SafeEndpoint_I2PAddress_ReturnsFullAddress()
    {
        // Arrange
        var i2pAddr = "abcdefghijklmnop.i2p:8080";

        // Act
        var result = LoggingUtils.SafeEndpoint(i2pAddr);

        // Assert
        Assert.Equal(i2pAddr, result);
    }

    [Fact]
    public void SafeCertificate_ReturnsRedactedFormat()
    {
        // Arrange
        var cert = CreateTestCertificate();

        // Act
        var result = LoggingUtils.SafeCertificate(cert);

        // Assert
        Assert.StartsWith("[cert:", result);
        Assert.EndsWith("]", result);
        Assert.Contains("...", result);
    }

    [Fact]
    public void SafeTransportEndpoint_ReturnsSafeFormat()
    {
        // Arrange
        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.DirectQuic,
            Host = "192.168.1.100",
            Port = 8080
        };

        // Act
        var result = LoggingUtils.SafeTransportEndpoint(endpoint);

        // Assert
        Assert.Equal("DirectQuic:xxx.xxx.xxx.100:8080", result);
    }

    [Fact]
    public void LogSafe_RedactsSensitiveData()
    {
        // Arrange
        var args = new object?[]
        {
            "normal string",
            "privatekey=secret123",
            "password=testpass",
            "cert=ABCDEFGH123456"
        };

        // Act
        LoggingUtils.LogSafe(_loggerMock.Object, LogLevel.Information, "Test message", args);

        // Assert - Verify logger was called with redacted args
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogConnectionEstablished_LogsWithSafeData()
    {
        // Arrange
        var peerId = "peer:test:connection";
        var endpoint = "192.168.1.100:8080";

        // Act
        LoggingUtils.LogConnectionEstablished(_loggerMock.Object, peerId, endpoint, TransportType.DirectQuic);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogConnectionFailed_LogsWithSafeData()
    {
        // Arrange
        var peerId = "peer:test:failed";
        var endpoint = "192.168.1.100:8080";
        var error = "Connection timeout";

        // Act
        LoggingUtils.LogConnectionFailed(_loggerMock.Object, peerId, endpoint, error);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogCertificateValidation_LogsWithSafeData()
    {
        // Arrange
        var cert = CreateTestCertificate();

        // Act
        LoggingUtils.LogCertificateValidation(_loggerMock.Object, "peer:test:cert", cert, true);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void SafeException_ReturnsSafeFormat()
    {
        // Arrange
        var exception = new Exception("Test error with privatekey=secret");

        // Act
        var result = LoggingUtils.SafeException(exception);

        // Assert
        Assert.Contains("Exception:", result);
        Assert.Contains("Test error with", result);
        Assert.DoesNotContain("privatekey=secret", result);
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

    // Test class for logger mock
    private class TestClass { }
}

