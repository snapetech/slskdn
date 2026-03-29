using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using System;
using System.Reflection;
using System.Threading;
using Xunit;

namespace slskd.Tests.Unit.Common.Security;

public class TimedBatcherTests
{
    [Fact]
    public async Task GetNextBatchAsync_WhenBatchWindowExpires_ReturnsQueuedMessages()
    {
        var logger = Mock.Of<ILogger<TimedBatcher>>();
        var batcher = new TimedBatcher(
            new MessageBatchingOptions
            {
                BatchWindowMs = 100,
                MaxBatchSize = 10,
            },
            logger);

        await batcher.AddMessageAsync(new byte[] { 1, 2, 3 });

        var batch = await batcher.GetNextBatchAsync();

        var message = Assert.Single(batch);
        Assert.Equal(new byte[] { 1, 2, 3 }, message.Data);
    }

    [Fact]
    public async Task AddMessageAsync_WhenBatchReachesMaxSize_DisposesExistingBatchTimer()
    {
        var logger = Mock.Of<ILogger<TimedBatcher>>();
        var batcher = new TimedBatcher(
            new MessageBatchingOptions
            {
                BatchWindowMs = 1000,
                MaxBatchSize = 2,
            },
            logger);

        await batcher.AddMessageAsync(new byte[] { 1 });
        var timer = GetCurrentBatchTimer(batcher);
        Assert.NotNull(timer);

        await batcher.AddMessageAsync(new byte[] { 2 });

        Assert.Null(GetCurrentBatchTimer(batcher));
        Assert.Throws<ObjectDisposedException>(() => _ = timer!.Token);
    }

    [Fact]
    public async Task FlushAsync_DisposesExistingBatchTimer()
    {
        var logger = Mock.Of<ILogger<TimedBatcher>>();
        var batcher = new TimedBatcher(
            new MessageBatchingOptions
            {
                BatchWindowMs = 1000,
                MaxBatchSize = 10,
            },
            logger);

        await batcher.AddMessageAsync(new byte[] { 1 });
        var timer = GetCurrentBatchTimer(batcher);
        Assert.NotNull(timer);

        await batcher.FlushAsync();

        Assert.Null(GetCurrentBatchTimer(batcher));
        Assert.Throws<ObjectDisposedException>(() => _ = timer!.Token);
    }

    private static CancellationTokenSource? GetCurrentBatchTimer(TimedBatcher batcher)
    {
        var field = typeof(TimedBatcher).GetField("_currentBatchTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (CancellationTokenSource?)field!.GetValue(batcher);
    }
}
