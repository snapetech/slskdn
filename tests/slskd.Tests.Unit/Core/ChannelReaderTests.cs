// <copyright file="ChannelReaderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core;

using System;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using slskd.Shares;
using Xunit;

public class ChannelReaderTests
{
    [Fact]
    public async Task Completed_WhenHandlerThrows_FaultsWithOriginalException()
    {
        var channel = Channel.CreateUnbounded<int>();
        var reader = new slskd.Shares.ChannelReader<int>(
            channel,
            _ => throw new InvalidOperationException("handler failed"));

        await channel.Writer.WriteAsync(1);
        channel.Writer.Complete();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.Completed);
        Assert.Equal("handler failed", exception.Message);
    }

    [Fact]
    public async Task Completed_WhenExceptionHandlerThrows_FaultsWithAggregateContainingOriginalException()
    {
        var channel = Channel.CreateUnbounded<int>();
        var reader = new slskd.Shares.ChannelReader<int>(
            channel,
            _ => throw new InvalidOperationException("handler failed"),
            _ => throw new ApplicationException("exception handler failed"));

        await channel.Writer.WriteAsync(1);
        channel.Writer.Complete();

        var exception = await Assert.ThrowsAsync<AggregateException>(() => reader.Completed);

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Contains(exception.InnerExceptions, ex => ex is InvalidOperationException ioe && ioe.Message == "handler failed");
        Assert.Contains(exception.InnerExceptions, ex => ex is ApplicationException ae && ae.Message == "exception handler failed");
    }
}
