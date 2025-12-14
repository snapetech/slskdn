// <copyright file="SecurityUtilsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace slskd.Tests.Unit.Mesh.Transport;

public class SecurityUtilsTests
{
    [Fact]
    public void ValidateCertificatePin_WithMatchingPin_ReturnsTrue()
    {
        // Arrange
        var certificate = CreateTestCertificate();
        var expectedPin = ExtractTestPin(certificate);

        // Act
        var result = SecurityUtils.ValidateCertificatePin(certificate, new[] { expectedPin });

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateCertificatePin_WithNonMatchingPin_ReturnsFalse()
    {
        // Arrange
        var certificate = CreateTestCertificate();

        // Act
        var result = SecurityUtils.ValidateCertificatePin(certificate, new[] { "different-pin" });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCertificatePin_WithNoPinsSpecified_ReturnsTrue()
    {
        // Arrange
        var certificate = CreateTestCertificate();

        // Act
        var result = SecurityUtils.ValidateCertificatePin(certificate, Array.Empty<string>());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ParseJsonSafely_WithValidJson_ReturnsObject()
    {
        // Arrange
        var json = "{\"test\": \"value\"}";

        // Act
        var result = SecurityUtils.ParseJsonSafely<Dictionary<string, string>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("value", result["test"]);
    }

    [Fact]
    public void ParseJsonSafely_WithOversizedPayload_ThrowsException()
    {
        // Arrange
        var largeJson = new string('x', SecurityUtils.MaxRemotePayloadSize + 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurityUtils.ParseJsonSafely<Dictionary<string, string>>(largeJson));
    }

    [Fact]
    public void ParseMessagePackSafely_WithValidData_ReturnsObject()
    {
        // Arrange
        var data = MessagePack.MessagePackSerializer.Serialize(new { test = "value" });

        // Act
        var result = SecurityUtils.ParseMessagePackSafely<Dictionary<string, string>>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("value", result["test"]);
    }

    [Fact]
    public void ParseMessagePackSafely_WithOversizedPayload_ThrowsException()
    {
        // Arrange
        var largeData = new byte[SecurityUtils.MaxRemotePayloadSize + 1];
        Random.Shared.NextBytes(largeData);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurityUtils.ParseMessagePackSafely<Dictionary<string, string>>(largeData));
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        // Create a self-signed test certificate
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=test",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(1));

        return certificate;
    }

    private static string ExtractTestPin(X509Certificate2 certificate)
    {
        // Extract SPKI and create pin
        var spki = certificate.PublicKey.EncodedKeyValue.RawData;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(spki);
        return Convert.ToBase64String(hash);
    }
}

public class ReplayCacheTests
{
    private readonly ReplayCache _cache;

    public ReplayCacheTests()
    {
        _cache = new ReplayCache(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void IsReplay_WithNewMessage_ReturnsFalse()
    {
        // Arrange
        var messageId = "msg-123";

        // Act
        var result = _cache.IsReplay(messageId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsReplay_WithRecentMessage_ReturnsTrue()
    {
        // Arrange
        var messageId = "msg-123";
        _cache.IsReplay(messageId); // First time

        // Act
        var result = _cache.IsReplay(messageId); // Second time

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsReplay_WithExpiredMessage_ReturnsFalse()
    {
        // Arrange
        var cache = new ReplayCache(TimeSpan.FromMilliseconds(1)); // Very short TTL
        var messageId = "msg-123";
        cache.IsReplay(messageId); // First time
        Thread.Sleep(10); // Wait for expiry

        // Act
        var result = cache.IsReplay(messageId); // After expiry

        // Assert
        Assert.False(result);
    }
}

public class ConnectionRateLimiterTests
{
    private readonly ConnectionRateLimiter _limiter;

    public ConnectionRateLimiterTests()
    {
        _limiter = new ConnectionRateLimiter(TimeSpan.FromSeconds(1), 2);
    }

    [Fact]
    public void IsConnectionAllowed_WithFirstAttempt_ReturnsTrue()
    {
        // Arrange
        var peerId = "peer-123";

        // Act
        var result = _limiter.IsConnectionAllowed(peerId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConnectionAllowed_AfterBackoffExpires_ReturnsTrue()
    {
        // Arrange
        var peerId = "peer-123";
        var limiter = new ConnectionRateLimiter(TimeSpan.FromMilliseconds(1), 1);

        // Fill up attempts
        limiter.IsConnectionAllowed(peerId); // Should succeed
        limiter.RecordFailure(peerId); // Put in backoff
        limiter.IsConnectionAllowed(peerId); // Should fail due to backoff
        Thread.Sleep(10); // Wait for backoff to expire

        // Act
        var result = limiter.IsConnectionAllowed(peerId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RecordSuccess_ResetsAttemptCount()
    {
        // Arrange
        var peerId = "peer-123";
        _limiter.IsConnectionAllowed(peerId);
        _limiter.RecordFailure(peerId);

        // Act
        _limiter.RecordSuccess(peerId);
        var allowed = _limiter.IsConnectionAllowed(peerId);

        // Assert
        Assert.True(allowed);
    }
}

