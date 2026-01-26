// <copyright file="StreamSessionLimiterTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Streaming;

using slskd.Streaming;
using Xunit;

public class StreamSessionLimiterTests
{
    [Fact]
    public void TryAcquire_UnderLimit_ReturnsTrue()
    {
        var limiter = new StreamSessionLimiter();
        Assert.True(limiter.TryAcquire("k1", 2));
        Assert.True(limiter.TryAcquire("k1", 2));
    }

    [Fact]
    public void TryAcquire_AtLimit_ReturnsFalse()
    {
        var limiter = new StreamSessionLimiter();
        Assert.True(limiter.TryAcquire("k1", 2));
        Assert.True(limiter.TryAcquire("k1", 2));
        Assert.False(limiter.TryAcquire("k1", 2));
    }

    [Fact]
    public void Release_DecrementsCount_AllowsNewAcquire()
    {
        var limiter = new StreamSessionLimiter();
        Assert.True(limiter.TryAcquire("k1", 1));
        Assert.False(limiter.TryAcquire("k1", 1));
        limiter.Release("k1");
        Assert.True(limiter.TryAcquire("k1", 1));
    }

    [Fact]
    public void DifferentKeys_CountedIndependently()
    {
        var limiter = new StreamSessionLimiter();
        Assert.True(limiter.TryAcquire("a", 1));
        Assert.True(limiter.TryAcquire("b", 1));
        Assert.False(limiter.TryAcquire("a", 1));
        Assert.False(limiter.TryAcquire("b", 1));
        limiter.Release("a");
        Assert.True(limiter.TryAcquire("a", 1));
        Assert.False(limiter.TryAcquire("b", 1));
    }

    [Fact]
    public void Release_EmptyKey_DoesNotThrow()
    {
        var limiter = new StreamSessionLimiter();
        limiter.Release("");
    }

    [Fact]
    public void Release_OverRelease_DoesNotGoNegative()
    {
        var limiter = new StreamSessionLimiter();
        limiter.Release("k");
        limiter.Release("k");
        Assert.True(limiter.TryAcquire("k", 1));
    }

    [Fact]
    public void TryAcquire_ZeroOrNegativeMax_ReturnsFalse()
    {
        var limiter = new StreamSessionLimiter();
        Assert.False(limiter.TryAcquire("k", 0));
        Assert.False(limiter.TryAcquire("k", -1));
    }

    [Fact]
    public void TryAcquire_EmptyKey_ReturnsFalse()
    {
        var limiter = new StreamSessionLimiter();
        Assert.False(limiter.TryAcquire("", 5));
    }
}
