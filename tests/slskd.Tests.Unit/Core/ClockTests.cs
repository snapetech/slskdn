// <copyright file="ClockTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using Xunit;

public class ClockTests
{
    [Fact]
    public void Fire_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        var invokedHealthySubscriber = false;
        EventHandler<ClockEventArgs> handlers = (_, _) => throw new InvalidOperationException("boom");
        handlers += (_, _) => invokedHealthySubscriber = true;

        var fireMethod = typeof(Clock).GetMethod(
            "Fire",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Clock.Fire method was not found.");

        fireMethod.Invoke(null, [handlers, new ClockEventArgs()]);

        Assert.True(invokedHealthySubscriber);
    }
}
