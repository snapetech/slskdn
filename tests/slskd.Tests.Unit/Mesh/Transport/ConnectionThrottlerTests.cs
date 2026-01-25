// <copyright file="ConnectionThrottlerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Transport;
using Xunit;
using MeshRateLimiter = slskd.Mesh.Transport.RateLimiter;

namespace slskd.Tests.Unit.Mesh.Transport;

public class ConnectionThrottlerTests
{
    private readonly Mock<ILogger<ConnectionThrottler>> _loggerMock;
    private readonly MeshRateLimiter _rateLimiter;
    private readonly ConnectionThrottler _throttler;

    public ConnectionThrottlerTests()
    {
        _loggerMock = new Mock<ILogger<ConnectionThrottler>>();
        _rateLimiter = new MeshRateLimiter(new Mock<ILogger<MeshRateLimiter>>().Object);
        _throttler = new ConnectionThrottler(_rateLimiter, _loggerMock.Object);
    }

    [Fact]
    public void ShouldAllowConnection_WithinLimits_ReturnsTrue()
    {
        // Arrange
        var endpoint = "192.168.1.100:8080";

        // Act
        var result = _throttler.ShouldAllowConnection(endpoint, TransportType.DirectQuic);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldAllowConnection_ExceedsGlobalLimit_ReturnsFalse()
    {
        // Arrange - Exhaust global connection tokens
        for (int i = 0; i < 1000; i++)
        {
            _throttler.ShouldAllowConnection($"192.168.1.{i % 255}:8080", TransportType.DirectQuic);
        }

        // Act - Try one more connection
        var result = _throttler.ShouldAllowConnection("192.168.1.200:8080", TransportType.DirectQuic);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldAllowConnection_ExceedsEndpointLimit_ReturnsFalse()
    {
        // Arrange
        var endpoint = "192.168.1.100:8080";

        // Exhaust per-endpoint tokens
        for (int i = 0; i < 10; i++)
        {
            _throttler.ShouldAllowConnection(endpoint, TransportType.DirectQuic);
        }

        // Act - Try one more from same endpoint
        var result = _throttler.ShouldAllowConnection(endpoint, TransportType.DirectQuic);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldAllowConnection_ExceedsTransportLimit_ReturnsFalse()
    {
        // Arrange
        // Exhaust per-transport tokens
        for (int i = 0; i < 100; i++)
        {
            _throttler.ShouldAllowConnection($"192.168.1.{i % 255}:8080", TransportType.DirectQuic);
        }

        // Act - Try one more with same transport
        var result = _throttler.ShouldAllowConnection("192.168.1.200:8080", TransportType.DirectQuic);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldAllowDescriptorFetch_WithinLimits_ReturnsTrue()
    {
        // Arrange
        var peerId = "peer:test:fetch";

        // Act
        var result = _throttler.ShouldAllowDescriptorFetch(peerId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldAllowDescriptorFetch_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var peerId = "peer:test:fetch";

        // Exhaust descriptor fetch tokens
        for (int i = 0; i < 100; i++)
        {
            _throttler.ShouldAllowDescriptorFetch(peerId);
        }

        // Act - Try one more
        var result = _throttler.ShouldAllowDescriptorFetch(peerId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldAllowEnvelopeProcessing_WithinLimits_ReturnsTrue()
    {
        // Arrange
        var peerId = "peer:test:envelope";

        // Act
        var result = _throttler.ShouldAllowEnvelopeProcessing(peerId, "test-type");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldAllowEnvelopeProcessing_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var peerId = "peer:test:envelope";

        // Exhaust envelope processing tokens
        for (int i = 0; i < 60; i++)
        {
            _throttler.ShouldAllowEnvelopeProcessing(peerId, "test-type");
        }

        // Act - Try one more
        var result = _throttler.ShouldAllowEnvelopeProcessing(peerId, "test-type");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ReportSuccessfulAuth_RecordsSuccess()
    {
        // Arrange
        var peerId = "peer:test:auth";

        // Act - Should not throw
        _throttler.ReportSuccessfulAuth(peerId);

        // Assert - No exception thrown
    }

    [Fact]
    public void ReportFailedAuth_AppliesBackoff()
    {
        // Arrange
        var endpoint = "192.168.1.100:8080";

        // Report more failures than auth-failure bucket capacity (5/minute)
        for (int i = 0; i < 6; i++)
        {
            _throttler.ReportFailedAuth(endpoint, "test failure");
        }

        // Act - Auth-failure rate limit is a separate bucket; when exceeded, RateLimiter blocks
        var stats = _throttler.GetStatistics();

        // Assert - At least one ReportFailedAuth exceeded the auth-failure limit and was counted as blocked
        Assert.True(stats.TotalRequestsBlocked >= 1);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectData()
    {
        // Arrange
        _throttler.ShouldAllowConnection("192.168.1.100:8080", TransportType.DirectQuic);

        // Act
        var stats = _throttler.GetStatistics();

        // Assert
        Assert.True(stats.ActiveBuckets >= 1);
        Assert.True(stats.TotalRequestsBlocked >= 0);
        Assert.True(stats.GlobalConnectionTokens >= 0);
    }
}


