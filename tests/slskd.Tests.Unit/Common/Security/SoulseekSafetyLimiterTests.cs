namespace slskd.Tests.Unit.Common.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Common.Security;
using Xunit;

public class SoulseekSafetyLimiterTests
{
    [Fact]
    public void Limiter_Starts_With_Zero_Usage()
    {
        // Arrange
        var limiter = CreateLimiter();

        // Act
        var metrics = limiter.GetMetrics();

        // Assert
        Assert.Equal(0, metrics.SearchesLastMinute);
        Assert.Equal(0, metrics.BrowsesLastMinute);
        Assert.Empty(metrics.SearchesBySource);
        Assert.Empty(metrics.BrowsesBySource);
    }

    [Fact]
    public void TryConsumeSearch_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = CreateLimiter(maxSearchesPerMinute: 10);

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            Assert.True(limiter.TryConsumeSearch("user"), $"Search {i + 1} should succeed");
        }

        var metrics = limiter.GetMetrics();
        Assert.Equal(10, metrics.SearchesLastMinute);
    }

    [Fact]
    public void TryConsumeSearch_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = CreateLimiter(maxSearchesPerMinute: 5);

        // Act - consume up to limit
        for (int i = 0; i < 5; i++)
        {
            limiter.TryConsumeSearch("user");
        }

        // Assert - next attempt should fail
        Assert.False(limiter.TryConsumeSearch("user"), "Search beyond limit should fail");

        var metrics = limiter.GetMetrics();
        Assert.Equal(5, metrics.SearchesLastMinute);
    }

    [Fact]
    public void TryConsumeBrowse_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = CreateLimiter(maxBrowsesPerMinute: 5);

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryConsumeBrowse("user"), $"Browse {i + 1} should succeed");
        }

        var metrics = limiter.GetMetrics();
        Assert.Equal(5, metrics.BrowsesLastMinute);
    }

    [Fact]
    public void TryConsumeBrowse_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = CreateLimiter(maxBrowsesPerMinute: 2);

        // Act - consume up to limit
        for (int i = 0; i < 2; i++)
        {
            limiter.TryConsumeBrowse("user");
        }

        // Assert - next attempt should fail
        Assert.False(limiter.TryConsumeBrowse("user"), "Browse beyond limit should fail");

        var metrics = limiter.GetMetrics();
        Assert.Equal(2, metrics.BrowsesLastMinute);
    }

    [Fact]
    public void PerSource_Tracking_Works_Independently()
    {
        // Arrange
        var limiter = CreateLimiter(maxSearchesPerMinute: 5);

        // Act - consume from different sources
        for (int i = 0; i < 3; i++)
        {
            limiter.TryConsumeSearch("user");
        }
        for (int i = 0; i < 2; i++)
        {
            limiter.TryConsumeSearch("mesh");
        }

        // Assert
        var metrics = limiter.GetMetrics();
        Assert.Equal(5, metrics.SearchesLastMinute);
        Assert.Equal(3, metrics.SearchesBySource["user"]);
        Assert.Equal(2, metrics.SearchesBySource["mesh"]);
    }

    [Fact]
    public void Disabled_Mode_Allows_Unlimited()
    {
        // Arrange
        var limiter = CreateLimiter(enabled: false, maxSearchesPerMinute: 5);

        // Act - try to consume way beyond limit
        for (int i = 0; i < 100; i++)
        {
            Assert.True(limiter.TryConsumeSearch("user"), $"Search {i + 1} should succeed when disabled");
        }

        // Note: metrics still track, but limits aren't enforced
    }

    [Fact]
    public void Zero_Limit_Allows_Unlimited()
    {
        // Arrange
        var limiter = CreateLimiter(maxSearchesPerMinute: 0);

        // Act
        for (int i = 0; i < 100; i++)
        {
            Assert.True(limiter.TryConsumeSearch("user"), $"Search {i + 1} should succeed with zero limit");
        }
    }

    [Fact]
    public void GetMetrics_Returns_Accurate_Counts()
    {
        // Arrange
        var limiter = CreateLimiter(maxSearchesPerMinute: 10, maxBrowsesPerMinute: 5);

        // Act
        limiter.TryConsumeSearch("user");
        limiter.TryConsumeSearch("user");
        limiter.TryConsumeSearch("mesh");
        limiter.TryConsumeBrowse("user");

        // Assert
        var metrics = limiter.GetMetrics();
        Assert.True(metrics.Enabled);
        Assert.Equal(10, metrics.MaxSearchesPerMinute);
        Assert.Equal(5, metrics.MaxBrowsesPerMinute);
        Assert.Equal(3, metrics.SearchesLastMinute);
        Assert.Equal(1, metrics.BrowsesLastMinute);
        Assert.Equal(2, metrics.SearchesBySource["user"]);
        Assert.Equal(1, metrics.SearchesBySource["mesh"]);
        Assert.Equal(1, metrics.BrowsesBySource["user"]);
    }

    [Fact]
    public async Task Thread_Safe_Concurrent_Consumption()
    {
        // Arrange
        var limiter = CreateLimiter(maxSearchesPerMinute: 1000);
        const int threadCount = 10;
        const int operationsPerThread = 50;

        // Act - hammer the limiter from multiple threads
        var tasks = Enumerable.Range(0, threadCount)
            .Select(async threadId =>
            {
                await Task.Yield(); // Force async execution
                for (int i = 0; i < operationsPerThread; i++)
                {
                    limiter.TryConsumeSearch($"thread-{threadId}");
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var metrics = limiter.GetMetrics();
        Assert.Equal(threadCount * operationsPerThread, metrics.SearchesLastMinute);
        Assert.Equal(threadCount, metrics.SearchesBySource.Count);
    }

    // Helper method to create limiter with test options
    private static SoulseekSafetyLimiter CreateLimiter(
        bool enabled = true,
        int maxSearchesPerMinute = 10,
        int maxBrowsesPerMinute = 5,
        int maxDownloadSlotsUsed = 50)
    {
        var options = new slskd.Options
        {
            Soulseek = new slskd.Options.SoulseekOptions
            {
                Safety = new slskd.Options.SoulseekOptions.SafetyOptions
                {
                    Enabled = enabled,
                    MaxSearchesPerMinute = maxSearchesPerMinute,
                    MaxBrowsesPerMinute = maxBrowsesPerMinute,
                    MaxDownloadSlotsUsed = maxDownloadSlotsUsed
                }
            }
        };

        var optionsMonitor = new TestOptionsMonitor<slskd.Options>(options);
        var logger = Mock.Of<ILogger<SoulseekSafetyLimiter>>();

        return new SoulseekSafetyLimiter(optionsMonitor, logger);
    }

    // Simple IOptionsMonitor implementation for testing
    private class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _currentValue;

        public TestOptionsMonitor(T currentValue)
        {
            _currentValue = currentValue;
        }

        public T CurrentValue => _currentValue;
        public T Get(string name) => _currentValue;
        public IDisposable OnChange(Action<T, string> listener) => null;
    }
}
