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
        // Act & Assert - Should not throw
        var generator = new CoverTrafficGenerator(_loggerMock.Object, 30.0, 5.0, 128);
        Assert.True(generator.ShouldGenerateCoverTraffic()); // Initially should be ready
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
    public void ShouldGenerateCoverTraffic_Initially_ReturnsTrue()
    {
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
        Assert.False(emptyMessage);
    }

    [Fact]
    public void TimeUntilNextCoverTraffic_Initially_ReturnsZero()
    {
        // Act
        var timeUntilNext = _generator.TimeUntilNextCoverTraffic();

        // Assert
        Assert.Equal(TimeSpan.Zero, timeUntilNext);
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

        // Assert - Should not throw and should be initially ready
        Assert.True(generator.ShouldGenerateCoverTraffic());
    }

    [Fact]
    public void Presets_Standard_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.Standard(_loggerMock.Object);

        // Assert
        Assert.True(generator.ShouldGenerateCoverTraffic());
    }

    [Fact]
    public void Presets_High_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.High(_loggerMock.Object);

        // Assert
        Assert.True(generator.ShouldGenerateCoverTraffic());
    }

    [Fact]
    public void Presets_Maximum_ReturnsCorrectConfiguration()
    {
        // Act
        var generator = CoverTrafficGenerator.Presets.Maximum(_loggerMock.Object);

        // Assert
        Assert.True(generator.ShouldGenerateCoverTraffic());
    }

    [Fact]
    public async Task GenerateCoverTrafficAsync_GeneratesMessagesWithCorrectSize()
    {
        // Arrange
        var generator = new CoverTrafficGenerator(_loggerMock.Object, 0.01, 0, 128); // Very short interval, no jitter
        var cts = new CancellationTokenSource();
        var messages = new List<byte[]>();

        // Act - Generate a few messages
        await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
        {
            messages.Add(message);
            if (messages.Count >= 3)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert
        Assert.Equal(3, messages.Count);
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

        // Act - Generate messages but record activity to suppress them
        var generationTask = Task.Run(async () =>
        {
            await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
            {
                messageCount++;
                generator.RecordActivity(); // Suppress further generation
                if (messageCount >= 5)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        await Task.Delay(100); // Let it run briefly
        cts.Cancel();
        await generationTask;

        // Assert - Should have generated very few messages due to activity suppression
        Assert.True(messageCount < 5, "Should generate fewer messages when activity is recorded");
    }
}

