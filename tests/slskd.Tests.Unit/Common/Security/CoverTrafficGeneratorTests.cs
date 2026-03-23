using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Common.Security;

public class CoverTrafficGeneratorTests
{
    [Fact]
    public async Task StopAsync_AfterStart_CancelsGenerationPromptly()
    {
        var logger = Mock.Of<ILogger<CoverTrafficGenerator>>();
        using var generator = new CoverTrafficGenerator(
            new CoverTrafficOptions
            {
                Enabled = true,
                IntervalSeconds = 60,
                OnlyWhenIdle = true,
            },
            () => new byte[] { 0x01 },
            () => Task.CompletedTask,
            logger);

        await generator.StartAsync();

        var stopwatch = Stopwatch.StartNew();
        await generator.StopAsync();
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"StopAsync took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task StartAsync_AfterStop_RestartsGeneration()
    {
        var logger = Mock.Of<ILogger<CoverTrafficGenerator>>();
        using var generator = new CoverTrafficGenerator(
            new CoverTrafficOptions
            {
                Enabled = true,
                IntervalSeconds = 60,
                OnlyWhenIdle = true,
            },
            () => new byte[] { 0x01 },
            () => Task.CompletedTask,
            logger);

        await generator.StartAsync();
        await generator.StopAsync();
        Assert.False(generator.GetStats().IsActive);

        await generator.StartAsync();

        Assert.True(generator.GetStats().IsActive);
    }
}
