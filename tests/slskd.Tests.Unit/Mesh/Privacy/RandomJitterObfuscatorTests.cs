// <copyright file="RandomJitterObfuscatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Privacy;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Privacy;

public class RandomJitterObfuscatorTests : IDisposable
{
    private readonly Mock<ILogger<RandomJitterObfuscator>> _loggerMock;

    public RandomJitterObfuscatorTests()
    {
        _loggerMock = new Mock<ILogger<RandomJitterObfuscator>>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act & Assert - Should not throw
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 10, 100);
        Assert.Equal(TimeSpan.FromMilliseconds(10), obfuscator.MinDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(100), obfuscator.MaxDelay);
    }

    [Fact]
    public void Constructor_WithNegativeMinDelay_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new RandomJitterObfuscator(_loggerMock.Object, -10, 100));
    }

    [Fact]
    public void Constructor_WithMaxDelayLessThanMinDelay_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new RandomJitterObfuscator(_loggerMock.Object, 100, 50));
    }

    [Fact]
    public void GetDelay_ReturnsDelayWithinRange()
    {
        // Arrange
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 100, 200);

        // Act
        var delay = obfuscator.GetDelay();

        // Assert
        Assert.True(delay >= TimeSpan.FromMilliseconds(100), $"Delay {delay.TotalMilliseconds}ms should be >= 100ms");
        Assert.True(delay <= TimeSpan.FromMilliseconds(200), $"Delay {delay.TotalMilliseconds}ms should be <= 200ms");
    }

    [Fact]
    public void GetDelay_WithZeroRange_ReturnsMinDelay()
    {
        // Arrange
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 150, 150);

        // Act
        var delay = obfuscator.GetDelay();

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(150), delay);
    }

    [Fact]
    public void RecordSend_DoesNotThrow()
    {
        // Arrange
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 10, 100);

        // Act & Assert - Should not throw
        obfuscator.RecordSend();
    }

    [Fact]
    public void AverageDelay_CalculatesCorrectly()
    {
        // Arrange
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 100, 300);

        // Act
        var average = obfuscator.AverageDelay;

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(200), average);
    }

    [Fact]
    public void GetDelay_MultipleCalls_ReturnVariedDelays()
    {
        // Arrange
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 100, 200);
        var delays = new List<TimeSpan>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            delays.Add(obfuscator.GetDelay());
        }

        // Assert - Should get some variation (not all identical)
        var distinctDelays = delays.Distinct().Count();
        Assert.True(distinctDelays > 1, "Should get varied delays, not all identical");
    }

    [Fact]
    public void GetDelay_AllDelaysWithinBounds()
    {
        // Arrange
        var obfuscator = new RandomJitterObfuscator(_loggerMock.Object, 50, 150);
        var delays = new List<TimeSpan>();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            delays.Add(obfuscator.GetDelay());
        }

        // Assert - All delays should be within bounds
        var minDelay = delays.Min();
        var maxDelay = delays.Max();

        Assert.True(minDelay >= TimeSpan.FromMilliseconds(50), $"Min delay {minDelay.TotalMilliseconds}ms should be >= 50ms");
        Assert.True(maxDelay <= TimeSpan.FromMilliseconds(150), $"Max delay {maxDelay.TotalMilliseconds}ms should be <= 150ms");
    }

    [Fact]
    public void Presets_Low_ReturnsCorrectConfiguration()
    {
        // Act
        var obfuscator = RandomJitterObfuscator.Presets.Low(_loggerMock.Object);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(10), obfuscator.MinDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(50), obfuscator.MaxDelay);
    }

    [Fact]
    public void Presets_Standard_ReturnsCorrectConfiguration()
    {
        // Act
        var obfuscator = RandomJitterObfuscator.Presets.Standard(_loggerMock.Object);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(50), obfuscator.MinDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(200), obfuscator.MaxDelay);
    }

    [Fact]
    public void Presets_High_ReturnsCorrectConfiguration()
    {
        // Act
        var obfuscator = RandomJitterObfuscator.Presets.High(_loggerMock.Object);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(100), obfuscator.MinDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(500), obfuscator.MaxDelay);
    }

    [Fact]
    public void Presets_Maximum_ReturnsCorrectConfiguration()
    {
        // Act
        var obfuscator = RandomJitterObfuscator.Presets.Maximum(_loggerMock.Object);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(200), obfuscator.MinDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), obfuscator.MaxDelay);
    }
}

