// <copyright file="RetryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core;

using System;
using System.Threading.Tasks;
using Xunit;

public class RetryTests
{
    [Fact]
    public async Task Do_WhenOnFailureThrows_PreservesOriginalOperationException()
    {
        var exception = await Assert.ThrowsAsync<RetryException>(() => Retry.Do(
            task: () => Task.FromException(new InvalidOperationException("primary failure")),
            onFailure: (_, _) => throw new ApplicationException("callback failure"),
            maxAttempts: 1));

        var aggregate = Assert.IsType<AggregateException>(exception.InnerException);

        Assert.Contains(aggregate.InnerExceptions, ex => ex is InvalidOperationException ioe && ioe.Message == "primary failure");
        Assert.Contains(aggregate.InnerExceptions, ex => ex is ApplicationException ae && ae.Message == "callback failure");
    }

    [Fact]
    public async Task Do_WhenIsRetryableThrows_PreservesOriginalOperationException()
    {
        var exception = await Assert.ThrowsAsync<RetryException>(() => Retry.Do(
            task: () => Task.FromException(new InvalidOperationException("primary failure")),
            isRetryable: (_, _) => throw new ApplicationException("retryable failure"),
            maxAttempts: 1));

        var aggregate = Assert.IsType<AggregateException>(exception.InnerException);

        Assert.Contains(aggregate.InnerExceptions, ex => ex is InvalidOperationException ioe && ioe.Message == "primary failure");
        Assert.Contains(aggregate.InnerExceptions, ex => ex is ApplicationException ae && ae.Message == "retryable failure");
    }

    [Fact]
    public async Task Do_WhenOperationRetries_InvokesRetryCallbackWithNextAttempt()
    {
        var attempts = 0;
        var retryAttempt = 0;
        var retryDelay = -1;

        await Retry.Do(
            task: () =>
            {
                attempts++;

                if (attempts == 1)
                {
                    throw new InvalidOperationException("transient");
                }

                return Task.CompletedTask;
            },
            onRetry: (attempt, delay) =>
            {
                retryAttempt = attempt;
                retryDelay = delay;
            },
            maxAttempts: 2,
            baseDelayInMilliseconds: 0,
            maxDelayInMilliseconds: 0);

        Assert.Equal(2, attempts);
        Assert.Equal(2, retryAttempt);
        Assert.Equal(0, retryDelay);
    }
}
