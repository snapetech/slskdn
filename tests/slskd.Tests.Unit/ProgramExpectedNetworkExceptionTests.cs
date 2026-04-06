// <copyright file="ProgramExpectedNetworkExceptionTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit;

using System;
using Xunit;

[Collection("ProgramAppDirectory")]
public class ProgramExpectedNetworkExceptionTests
{
    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForPeerTimeoutFailures()
    {
        var exception = new AggregateException(
            new TimeoutException("The wait timed out after 10000 milliseconds in Soulseek.Network.PeerConnectionManager."));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForDistributedOperationCanceledFailures()
    {
        var exception = new AggregateException(
            new OperationCanceledException("Operation canceled in Soulseek.Network.DistributedConnectionManager."));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }
}
