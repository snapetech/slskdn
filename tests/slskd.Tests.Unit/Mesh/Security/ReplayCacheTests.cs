// <copyright file="ReplayCacheTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Overlay;
using slskd.Mesh.Security;
using Xunit;

public class ReplayCacheTests
{
    [Theory]
    [AutoData]
    public void ValidateAndRecord_FirstTime_ReturnsTrue(string peerId, string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        // Act
        var result = cache.ValidateAndRecord(peerId, envelope);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void ValidateAndRecord_Replay_ReturnsFalse(string peerId, string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        // Act
        var firstResult = cache.ValidateAndRecord(peerId, envelope);
        var replayResult = cache.ValidateAndRecord(peerId, envelope);

        // Assert
        firstResult.Should().BeTrue();
        replayResult.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void ValidateAndRecord_WithTimestampSkew_ReturnsFalse(string peerId, string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            // 5 minutes in the past (exceeds 2 minute window)
            TimestampUnixMs = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
        };

        // Act
        var result = cache.ValidateAndRecord(peerId, envelope);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void ValidateAndRecord_WithFutureTimestamp_ReturnsFalse(string peerId, string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            // 5 minutes in the future
            TimestampUnixMs = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds(),
        };

        // Act
        var result = cache.ValidateAndRecord(peerId, envelope);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void ValidateAndRecord_WithinTimestampWindow_ReturnsTrue(string peerId, string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            // 1 minute in the past (within 2 minute window)
            TimestampUnixMs = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
        };

        // Act
        var result = cache.ValidateAndRecord(peerId, envelope);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void ValidateAndRecord_DifferentPeers_IndependentCaches(string peerId1, string peerId2, string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        var messageId = Guid.NewGuid().ToString("N");
        var envelope1 = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = messageId,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var envelope2 = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = messageId, // Same MessageId
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        // Act
        var peer1Result = cache.ValidateAndRecord(peerId1, envelope1);
        var peer2Result = cache.ValidateAndRecord(peerId2, envelope2);

        // Assert
        peer1Result.Should().BeTrue();
        peer2Result.Should().BeTrue(); // Same MessageId but different peer, should succeed
    }

    [Fact]
    public void PurgeExpired_RemovesOldEntries()
    {
        // Arrange
        var logger = new Mock<ILogger<ReplayCache>>();
        var cache = new ReplayCache(logger.Object);

        // This test just ensures PurgeExpired doesn't throw
        // Act
        cache.PurgeExpired();

        // Assert
        // No exception thrown
    }
}

