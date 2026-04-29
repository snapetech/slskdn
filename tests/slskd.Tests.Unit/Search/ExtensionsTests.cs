// <copyright file="ExtensionsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Search;

using System;
using Soulseek;
using slskd.Search;
using Xunit;

public class ExtensionsTests
{
    [Fact]
    public void WithActions_StateChanged_InvokesBothAndAggregatesMultipleFailures()
    {
        var originalCalls = 0;
        var injectedCalls = 0;

        var options = new SearchOptions(
            stateChanged: args =>
            {
                originalCalls++;
                throw new InvalidOperationException("existing");
            });

        var bound = options.WithActions(stateChanged: args =>
        {
            injectedCalls++;
            throw new ArgumentException("new");
        });

        var exception = Assert.Throws<AggregateException>(() => bound.StateChanged!((SearchStates.None, default!)));

        Assert.Equal(1, originalCalls);
        Assert.Equal(1, injectedCalls);
        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
        Assert.IsType<ArgumentException>(exception.InnerExceptions[1]);
    }

    [Fact]
    public void WithActions_ResponseReceived_SingleFailurePreserved()
    {
        var originalCalls = 0;
        var options = new SearchOptions(
            responseReceived: response =>
            {
                originalCalls++;
                throw new InvalidOperationException("boom");
            });

        var bound = options.WithActions();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            bound.ResponseReceived!((default!, default!)));

        Assert.Equal(1, originalCalls);
        Assert.Equal("boom", exception.Message);
    }
}
