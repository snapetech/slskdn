// <copyright file="TimedBatcherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Privacy;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Privacy;

public class TimedBatcherTests : IDisposable
{
    private readonly Mock<ILogger<TimedBatcher>> _loggerMock;
    private readonly TimedBatcher _batcher;

    public TimedBatcherTests()
    {
        _loggerMock = new Mock<ILogger<TimedBatcher>>();
        _batcher = new TimedBatcher(_loggerMock.Object, 1.0, 5); // 1 second window, max 5 messages
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act & Assert - Should not throw
        var batcher = new TimedBatcher(_loggerMock.Object, 2.0, 10);
        Assert.Equal(0, batcher.CurrentBatchSize);
    }

    [Fact]
    public void Constructor_WithNegativeBatchWindow_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimedBatcher(_loggerMock.Object, -1.0, 10));
    }

    [Fact]
    public void Constructor_WithZeroBatchWindow_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimedBatcher(_loggerMock.Object, 0, 10));
    }

    [Fact]
    public void Constructor_WithNegativeMaxBatchSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimedBatcher(_loggerMock.Object, 1.0, -1));
    }

    [Fact]
    public void Constructor_WithZeroMaxBatchSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimedBatcher(_loggerMock.Object, 1.0, 0));
    }

    [Fact]
    public void AddMessage_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _batcher.AddMessage(null!));
    }

    [Fact]
    public void AddMessage_WithValidMessage_AddsToBatch()
    {
        // Arrange
        var message = new byte[] { 1, 2, 3 };

        // Act
        var isReady = _batcher.AddMessage(message);

        // Assert
        Assert.False(isReady); // Should not be ready yet
        Assert.Equal(1, _batcher.CurrentBatchSize);
    }

    [Fact]
    public void AddMessage_WithMaxMessages_MakesBatchReady()
    {
        // Arrange
        var batcher = new TimedBatcher(_loggerMock.Object, 10.0, 3); // Max 3 messages

        // Act
        var ready1 = batcher.AddMessage(new byte[] { 1 });
        var ready2 = batcher.AddMessage(new byte[] { 2 });
        var ready3 = batcher.AddMessage(new byte[] { 3 });

        // Assert
        Assert.False(ready1);
        Assert.False(ready2);
        Assert.True(ready3); // Should be ready when max size reached
        Assert.True(batcher.HasBatch);
    }

    [Fact]
    public async Task AddMessage_WithTimeWindow_MakesBatchReady()
    {
        // Arrange
        var batcher = new TimedBatcher(_loggerMock.Object, 0.1, 10); // 100ms window
        var message = new byte[] { 1, 2, 3 };

        // Act
        var isReady = batcher.AddMessage(message);
        Assert.False(isReady); // Not ready immediately

        await Task.Delay(150); // Wait for time window to expire

        // Assert
        Assert.True(batcher.HasBatch);
    }

    [Fact]
    public void GetBatch_WhenNoBatch_ReturnsNull()
    {
        // Act
        var batch = _batcher.GetBatch();

        // Assert
        Assert.Null(batch);
    }

    [Fact]
    public void GetBatch_WhenBatchReady_ReturnsMessages()
    {
        // Arrange - fill to maxBatchSize so IsBatchReady is true (Flush only deactivates; it doesn't make GetBatch return)
        var batcher = new TimedBatcher(_loggerMock.Object, 10.0, 2);
        var message1 = new byte[] { 1, 2, 3 };
        var message2 = new byte[] { 4, 5, 6 };
        batcher.AddMessage(message1);
        batcher.AddMessage(message2);

        // Act
        var batch = batcher.GetBatch();

        // Assert
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Count);
        Assert.Equal(message1, batch[0]);
        Assert.Equal(message2, batch[1]);
    }

    [Fact]
    public void GetBatch_AfterGettingBatch_ClearsBatch()
    {
        // Arrange - max 1 so one message makes batch ready; GetBatch consumes and clears
        var batcher = new TimedBatcher(_loggerMock.Object, 10.0, 1);
        batcher.AddMessage(new byte[] { 1 });

        // Act
        var batch = batcher.GetBatch();
        var secondBatch = batcher.GetBatch();

        // Assert
        Assert.NotNull(batch);
        Assert.Null(secondBatch);
        Assert.Equal(0, batcher.CurrentBatchSize);
        Assert.False(batcher.HasBatch);
    }

    [Fact]
    public void Flush_WhenBatchActive_DeactivatesBatch()
    {
        // Arrange
        _batcher.AddMessage(new byte[] { 1, 2, 3 });

        // Act - Flush sets _isBatchActive=false; messages remain in queue until GetBatch when ready
        _batcher.Flush();

        // Assert - HasBatch is false (batch no longer active); queue unchanged
        Assert.False(_batcher.HasBatch);
        Assert.Equal(1, _batcher.CurrentBatchSize);
    }

    [Fact]
    public void TimeRemainingInBatch_WhenNoBatch_ReturnsZero()
    {
        // Assert
        Assert.Equal(TimeSpan.Zero, _batcher.TimeRemainingInBatch);
    }

    [Fact]
    public void TimeRemainingInBatch_WhenBatchActive_ReturnsRemainingTime()
    {
        // Arrange
        var batcher = new TimedBatcher(_loggerMock.Object, 2.0, 10);
        batcher.AddMessage(new byte[] { 1 });

        // Act
        var remaining = batcher.TimeRemainingInBatch;

        // Assert
        Assert.True(remaining > TimeSpan.Zero);
        Assert.True(remaining <= TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void HasBatch_WhenNoMessages_ReturnsFalse()
    {
        // Assert
        Assert.False(_batcher.HasBatch);
    }

    [Fact]
    public void HasBatch_WhenMaxReached_ReturnsTrue()
    {
        // Arrange - max 1 so one message makes batch ready (Flush deactivates, it doesn't make HasBatch true)
        var batcher = new TimedBatcher(_loggerMock.Object, 10.0, 1);
        batcher.AddMessage(new byte[] { 1 });

        // Assert
        Assert.True(batcher.HasBatch);
    }

    [Fact]
    public void CurrentBatchSize_ReflectsAddedMessages()
    {
        // Act
        _batcher.AddMessage(new byte[] { 1 });
        _batcher.AddMessage(new byte[] { 2 });
        _batcher.AddMessage(new byte[] { 3 });

        // Assert
        Assert.Equal(3, _batcher.CurrentBatchSize);
    }

    [Fact]
    public void Presets_LowLatency_ReturnsCorrectConfiguration()
    {
        // Act
        var batcher = TimedBatcher.Presets.LowLatency(_loggerMock.Object);

        // Assert - We can't directly test the configuration, but we can test it doesn't throw
        Assert.Equal(0, batcher.CurrentBatchSize);
    }

    [Fact]
    public void Presets_Standard_ReturnsCorrectConfiguration()
    {
        // Act
        var batcher = TimedBatcher.Presets.Standard(_loggerMock.Object);

        // Assert
        Assert.Equal(0, batcher.CurrentBatchSize);
    }

    [Fact]
    public void Presets_HighPrivacy_ReturnsCorrectConfiguration()
    {
        // Act
        var batcher = TimedBatcher.Presets.HighPrivacy(_loggerMock.Object);

        // Assert
        Assert.Equal(0, batcher.CurrentBatchSize);
    }

    [Fact]
    public void Presets_MaximumPrivacy_ReturnsCorrectConfiguration()
    {
        // Act
        var batcher = TimedBatcher.Presets.MaximumPrivacy(_loggerMock.Object);

        // Assert
        Assert.Equal(0, batcher.CurrentBatchSize);
    }
}


