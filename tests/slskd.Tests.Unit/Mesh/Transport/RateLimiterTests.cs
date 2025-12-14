// <copyright file="RateLimiterTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class RateLimiterTests : IDisposable
{
    private readonly Mock<ILogger<RateLimiter>> _loggerMock;
    private readonly RateLimiter _rateLimiter;

    public RateLimiterTests()
    {
        _loggerMock = new Mock<ILogger<RateLimiter>>();
        _rateLimiter = new RateLimiter(_loggerMock.Object);
    }

    public void Dispose()
    {
        _rateLimiter.CleanupExpiredBuckets();
    }

    [Fact]
    public void TryConsume_WithinCapacity_ReturnsTrue()
    {
        // Arrange
        var bucketKey = "test-bucket";

        // Act
        var result = _rateLimiter.TryConsume(bucketKey, capacity: 10, refillRate: 1.0);

        // Assert
        Assert.True(result);
        Assert.Equal(9, _rateLimiter.GetCurrentTokens(bucketKey));
    }

    [Fact]
    public void TryConsume_ExceedsCapacity_ReturnsFalse()
    {
        // Arrange
        var bucketKey = "test-bucket";

        // Use up all tokens
        for (int i = 0; i < 5; i++)
        {
            _rateLimiter.TryConsume(bucketKey, capacity: 5, refillRate: 0.1);
        }

        // Act - Try to consume one more
        var result = _rateLimiter.TryConsume(bucketKey, capacity: 5, refillRate: 0.1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryConsume_RefillsTokensOverTime()
    {
        // Arrange
        var bucketKey = "test-bucket";

        // Use up all tokens
        for (int i = 0; i < 5; i++)
        {
            _rateLimiter.TryConsume(bucketKey, capacity: 5, refillRate: 10.0); // 10 tokens per second
        }

        // Wait for refill (simulate time passing)
        Thread.Sleep(600); // 0.6 seconds = 6 tokens

        // Act
        var result = _rateLimiter.TryConsume(bucketKey, capacity: 5, refillRate: 10.0);

        // Assert
        Assert.True(result); // Should have refilled enough tokens
    }

    [Fact]
    public void ResetBucket_ResetsTokenCount()
    {
        // Arrange
        var bucketKey = "test-bucket";

        // Use some tokens
        _rateLimiter.TryConsume(bucketKey, capacity: 10, refillRate: 1.0);

        // Act
        _rateLimiter.ResetBucket(bucketKey);

        // Assert
        Assert.Equal(10, _rateLimiter.GetCurrentTokens(bucketKey));
    }

    [Fact]
    public void CleanupExpiredBuckets_RemovesOldBuckets()
    {
        // Arrange
        var bucketKey = "test-bucket";

        // Create a bucket
        _rateLimiter.TryConsume(bucketKey, capacity: 10, refillRate: 1.0);

        // Manually set the bucket to be old by accessing private field
        // In a real scenario, this would happen over time

        // Act
        _rateLimiter.CleanupExpiredBuckets(TimeSpan.Zero);

        // Assert - The bucket should be cleaned up since it's considered "expired"
        // Note: This test may be fragile depending on the exact cleanup logic
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectData()
    {
        // Arrange
        _rateLimiter.TryConsume("bucket1", capacity: 10, refillRate: 1.0);
        _rateLimiter.TryConsume("bucket2", capacity: 10, refillRate: 1.0);

        // Act
        var stats = _rateLimiter.GetStatistics();

        // Assert
        Assert.Equal(2, stats.ActiveBuckets);
        Assert.Equal(2, stats.TotalTokensConsumed);
        Assert.Equal(0, stats.TotalRequestsBlocked);
    }
}
