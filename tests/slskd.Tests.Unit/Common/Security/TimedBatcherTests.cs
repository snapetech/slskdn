using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
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
}
