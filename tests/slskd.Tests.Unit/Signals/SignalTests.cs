namespace slskd.Tests.Unit.Signals;

using System;
using System.Collections.Generic;
using slskd.Signals;
using Xunit;

public class SignalTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var signalId = Guid.NewGuid().ToString("N");
        var fromPeerId = "peer-1";
        var toPeerId = "peer-2";
        var sentAt = DateTimeOffset.UtcNow;
        var type = "Test.Signal";
        var body = new Dictionary<string, object> { ["key"] = "value" };
        var ttl = TimeSpan.FromMinutes(5);
        var channels = new List<SignalChannel> { SignalChannel.Mesh };

        // Act
        var signal = new Signal(signalId, fromPeerId, toPeerId, sentAt, type, body, ttl, channels);

        // Assert
        Assert.Equal(signalId, signal.SignalId);
        Assert.Equal(fromPeerId, signal.FromPeerId);
        Assert.Equal(toPeerId, signal.ToPeerId);
        Assert.Equal(sentAt, signal.SentAt);
        Assert.Equal(type, signal.Type);
        Assert.Equal(body, signal.Body);
        Assert.Equal(ttl, signal.Ttl);
        Assert.Equal(channels, signal.PreferredChannels);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSignalIdIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Signal(
            null!,
            "peer-1",
            "peer-2",
            DateTimeOffset.UtcNow,
            "Test.Signal",
            new Dictionary<string, object>(),
            TimeSpan.FromMinutes(5),
            new List<SignalChannel> { SignalChannel.Mesh }));
    }

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenSignalIsNotExpired()
    {
        // Arrange
        var signal = new Signal(
            Guid.NewGuid().ToString("N"),
            "peer-1",
            "peer-2",
            DateTimeOffset.UtcNow,
            "Test.Signal",
            new Dictionary<string, object>(),
            TimeSpan.FromMinutes(5),
            new List<SignalChannel> { SignalChannel.Mesh });

        // Act
        var isExpired = signal.IsExpired(DateTimeOffset.UtcNow);

        // Assert
        Assert.False(isExpired);
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenSignalIsExpired()
    {
        // Arrange
        var signal = new Signal(
            Guid.NewGuid().ToString("N"),
            "peer-1",
            "peer-2",
            DateTimeOffset.UtcNow.AddMinutes(-10), // Sent 10 minutes ago
            "Test.Signal",
            new Dictionary<string, object>(),
            TimeSpan.FromMinutes(5), // TTL is only 5 minutes
            new List<SignalChannel> { SignalChannel.Mesh });

        // Act
        var isExpired = signal.IsExpired(DateTimeOffset.UtcNow);

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenSignalIsJustAtTTL()
    {
        // Arrange
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var signal = new Signal(
            Guid.NewGuid().ToString("N"),
            "peer-1",
            "peer-2",
            sentAt,
            "Test.Signal",
            new Dictionary<string, object>(),
            TimeSpan.FromMinutes(5),
            new List<SignalChannel> { SignalChannel.Mesh });

        // Act
        var isExpired = signal.IsExpired(sentAt + TimeSpan.FromMinutes(5));

        // Assert
        Assert.False(isExpired); // Exactly at TTL, not expired yet
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenSignalIsJustPastTTL()
    {
        // Arrange
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var signal = new Signal(
            Guid.NewGuid().ToString("N"),
            "peer-1",
            "peer-2",
            sentAt,
            "Test.Signal",
            new Dictionary<string, object>(),
            TimeSpan.FromMinutes(5),
            new List<SignalChannel> { SignalChannel.Mesh });

        // Act
        var isExpired = signal.IsExpired(sentAt + TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1));

        // Assert
        Assert.True(isExpired); // Just past TTL
    }
}
















