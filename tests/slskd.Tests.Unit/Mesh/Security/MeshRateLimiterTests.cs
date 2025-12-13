// <copyright file="MeshRateLimiterTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System.Net;
using System.Threading;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Security;
using Xunit;

public class MeshRateLimiterTests
{
    [Fact]
    public void AllowPreAuth_FirstRequest_ReturnsTrue()
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);
        var ip = IPAddress.Parse("192.168.1.100");

        // Act
        var result = limiter.AllowPreAuth(ip);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AllowPreAuth_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);
        var ip = IPAddress.Parse("192.168.1.100");

        // Act - consume 101 requests (limit is 100)
        for (int i = 0; i < 100; i++)
        {
            limiter.AllowPreAuth(ip).Should().BeTrue();
        }

        var exceeds = limiter.AllowPreAuth(ip);

        // Assert
        exceeds.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void AllowPostAuth_FirstRequest_ReturnsTrue(string peerId)
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);

        // Act
        var result = limiter.AllowPostAuth(peerId);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void AllowPostAuth_ExceedsLimit_ReturnsFalse(string peerId)
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);

        // Act - consume 501 requests (limit is 500)
        for (int i = 0; i < 500; i++)
        {
            limiter.AllowPostAuth(peerId).Should().BeTrue();
        }

        var exceeds = limiter.AllowPostAuth(peerId);

        // Assert
        exceeds.Should().BeFalse();
    }

    [Fact]
    public void AllowPreAuth_DifferentIPs_Independent()
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);
        var ip1 = IPAddress.Parse("192.168.1.100");
        var ip2 = IPAddress.Parse("192.168.1.101");

        // Act - exhaust ip1
        for (int i = 0; i < 100; i++)
        {
            limiter.AllowPreAuth(ip1);
        }

        // Assert - ip2 should still be allowed
        limiter.AllowPreAuth(ip2).Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void AllowPostAuth_DifferentPeers_Independent(string peerId1, string peerId2)
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);

        // Act - exhaust peerId1
        for (int i = 0; i < 500; i++)
        {
            limiter.AllowPostAuth(peerId1);
        }

        // Assert - peerId2 should still be allowed
        limiter.AllowPostAuth(peerId2).Should().BeTrue();
    }

    [Fact]
    public void PurgeExpired_DoesNotThrow()
    {
        // Arrange
        var logger = new Mock<ILogger<MeshRateLimiter>>();
        var limiter = new MeshRateLimiter(logger.Object);
        var ip = IPAddress.Parse("192.168.1.100");
        limiter.AllowPreAuth(ip);

        // Act & Assert
        limiter.Invoking(l => l.PurgeExpired()).Should().NotThrow();
    }
}

