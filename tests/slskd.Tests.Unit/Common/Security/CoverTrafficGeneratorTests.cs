// <copyright file="CoverTrafficGeneratorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Diagnostics;
using System.Reflection;
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

    [Fact]
    public async Task StartAsync_WhenReplacingPreviousGenerationToken_CancelsOldTokenSource()
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

        using var previousGenerationCts = new CancellationTokenSource();
        SetPrivateField(generator, "_generationCts", previousGenerationCts);
        SetPrivateField(generator, "_generationTask", Task.CompletedTask);

        await generator.StartAsync();

        Assert.True(previousGenerationCts.IsCancellationRequested);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {instance.GetType().Name}.");

        field.SetValue(instance, value);
    }
}
