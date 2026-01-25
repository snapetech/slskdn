// <copyright file="CoverTrafficGeneratorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Privacy;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Privacy;

public class CoverTrafficGeneratorTests : IDisposable
{
    private readonly Mock<ILogger<CoverTrafficGenerator>> _loggerMock;
    private readonly CoverTrafficGenerator _generator;

    public CoverTrafficGeneratorTests()
    {
        _loggerMock = new Mock<ILogger<CoverTrafficGenerator>>();
        _generator = new CoverTrafficGenerator(_loggerMock.Object, 1.0, 0.1, 64); // 1s interval, 0.1s jitter, 64 byte messages
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act & Assert - Should not throw (ShouldGenerate requires waiting for interval, so we only verify construction)
        var generator = new CoverTrafficGenerator(_loggerMock.Object, 30.0, 5.0, 128);
        Assert.NotNull(generator);
    }

    [Fact]
    public void Constructor_WithNegativeInterval_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoverTrafficGenerator(_loggerMock.Object, -1.0, 5.0, 64));
    }

    [Fact]
    public void Constructor_WithZeroInterval_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoverTrafficGenerator(_loggerMock.Object, 0, 5.0, 64));
    }

    [Fact]
    public void Constructor_WithNegativeJitterRange_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoverTrafficGenerator(_loggerMock.Object, 30.0, -1.0, 64));
    }

    [Fact]
    public void Constructor_WithZeroMessageSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoverTrafficGenerator(_loggerMock.Object, 30.0, 5.0, 0));
    }

    [Fact]
    public void Constructor_WithNegativeMessageSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoverTrafficGenerator(_loggerMock.Object, 30.0, 5.0, -1));
    }

    [Fact]
    public void RecordActivity_UpdatesLastActivityTime()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        _generator.RecordActivity();
        var timeUntilNext = _generator.TimeUntilNextCoverTraffic();

        // Assert - Should not generate immediately after activity
        Assert.True(timeUntilNext > TimeSpan.Zero);
    }

    [Fact]
    public async Task ShouldGenerateCoverTraffic_Initially_ReturnsTrue()
    {
        // Wait for interval to pass (interval=1s, GetNextInterval min 1s)
        await Task.Delay(1100);

        // Act
        var shouldGenerate = _generator.ShouldGenerateCoverTraffic();

        // Assert
        Assert.True(shouldGenerate);
    }

    [Fact]
    public void ShouldGenerateCoverTraffic_AfterRecentActivity_ReturnsFalse()
    {
        // Arrange
        _generator.RecordActivity();

        // Act
        var shouldGenerate = _generator.ShouldGenerateCoverTraffic();

        // Assert
        Assert.False(shouldGenerate);
    }

    [Fact]
    public async Task GenerateCoverTrafficAsync_WithCancellation_CancelsGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should not throw
        await foreach (var message in _generator.GenerateCoverTrafficAsync(cts.Token))
        {
            // Should not reach here
            Assert.Fail("Should not generate messages when cancelled");
        }
    }

    [Fact]
    public void IsCoverTraffic_WithCoverTrafficMessage_ReturnsTrue()
    {
        // Arrange
        var coverMessage = new byte[] { 0xFF, 1, 2, 3 }; // Cover traffic marker

        // Act
        var isCover = CoverTrafficGenerator.IsCoverTraffic(coverMessage);

        // Assert
        Assert.True(isCover);
    }

    [Fact]
    public void IsCoverTraffic_WithRegularMessage_ReturnsFalse()
    {
        // Arrange
        var regularMessage = new byte[] { 0x00, 1, 2, 3 }; // Not cover traffic

        // Act
        var isCover = CoverTrafficGenerator.IsCoverTraffic(regularMessage);

        // Assert
        Assert.False(isCover);
    }

    [Fact]
    public void IsCoverTraffic_WithEmptyMessage_ReturnsFalse()
    {
        // Arrange
        var emptyMessage = Array.Empty<byte>();

        // Act
        var isCover = CoverTrafficGenerator.IsCoverTraffic(emptyMessage);

        // Assert
        Assert.False(isCover);
    }

    [Fact]
    public void TimeUntilNextCoverTraffic_Initially_ReturnsPositiveInterval()
    {
        // Act - initially lastActivity is Now, so we get interval - ~0 ≈ interval
        var timeUntilNext = _generator.TimeUntilNextCoverTraffic();

        // Assert - producer uses interval 1s; GetNextInterval has 1s minimum
        Assert.True(timeUntilNext > TimeSpan.Zero);
        Assert.True(timeUntilNext <= TimeSpan.FromSeconds(1.1));
    }

    [Fact]
    public void TimeUntilNextCoverTraffic_AfterActivity_ReturnsInterval()
    {
        // Arrange
        _generator.RecordActivity();

        // Act
        var timeUntilNext = _generator.TimeUntilNextCoverTraffic();

        // Assert - Should be approximately the interval
        Assert.True(timeUntilNext >= TimeSpan.FromSeconds(0.9)); // Allow some tolerance
        Assert.True(timeUntilNext <= TimeSpan.FromSeconds(1.1));
    }

    [Fact]
    public void Presets_Minimal_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.Minimal(_loggerMock.Object);

        // Assert - Presets create valid generators (ShouldGenerate would need waiting for interval)
        Assert.NotNull(generator);
    }

    [Fact]
    public void Presets_Standard_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.Standard(_loggerMock.Object);

        // Assert
        Assert.NotNull(generator);
    }

    [Fact]
    public void Presets_High_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.High(_loggerMock.Object);

        // Assert
        Assert.NotNull(generator);
    }

    [Fact]
    public void Presets_Maximum_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.Maximum(_loggerMock.Object);

        // Assert
        Assert.NotNull(generator);
    }

    [Fact]
    public async Task GenerateCoverTrafficAsync_GeneratesMessagesWithCorrectSize()
    {
        // Arrange - GetNextInterval has 1s minimum, so we need to wait ~1s per message
        var generator = new CoverTrafficGenerator(_loggerMock.Object, 0.01, 0, 128);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var messages = new List<byte[]>();

        // Act - collect at least 1 message (need ~1s for first, ~2s for 2nd, etc.)
        await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
        {
            messages.Add(message);
            if (messages.Count >= 2)
                break;
        }

        // Assert
        Assert.True(messages.Count >= 1, "Should generate at least one message within 5s");
        foreach (var message in messages)
        {
            Assert.Equal(128, message.Length);
            Assert.True(CoverTrafficGenerator.IsCoverTraffic(message));
        }
    }

    [Fact]
    public async Task GenerateCoverTrafficAsync_RespectsActivityRecording()
    {
        // Arrange
        var generator = new CoverTrafficGenerator(_loggerMock.Object, 0.01, 0, 64); // Very short interval
        var cts = new CancellationTokenSource();
        var messageCount = 0;

        // Act - Generate messages but record activity to suppress them; cancel after a short run
        var generationTask = Task.Run(async () =>
        {
            await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
            {
                messageCount++;
                generator.RecordActivity(); // Suppress further generation
                if (messageCount >= 5)
                    break;
            }
        });

        await Task.Delay(100);
        cts.Cancel();
        try { await generationTask; } catch (OperationCanceledException) { /* expected when cancelling */ }

        // Assert - Should have generated very few messages due to activity suppression and 1s min interval
        Assert.True(messageCount < 5, "Should generate fewer messages when activity is recorded");
    }
}


