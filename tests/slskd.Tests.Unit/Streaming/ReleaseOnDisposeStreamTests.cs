// <copyright file="ReleaseOnDisposeStreamTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Streaming;

using System;
using System.IO;
using slskd.Streaming;
using Xunit;

public class ReleaseOnDisposeStreamTests
{
    [Fact]
    public void Constructor_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReleaseOnDisposeStream(null!, () => { }));
    }

    [Fact]
    public void Constructor_NullOnDispose_Throws()
    {
        using var ms = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() =>
            new ReleaseOnDisposeStream(ms, null!));
    }

    [Fact]
    public void Dispose_InvokesOnDispose()
    {
        var invoked = false;
        using (var inner = new MemoryStream(new byte[] { 1, 2, 3 }))
        using (var wrapped = new ReleaseOnDisposeStream(inner, () => invoked = true))
        {
            Assert.False(invoked);
            _ = wrapped.ReadByte();
        }
        Assert.True(invoked);
    }

    [Fact]
    public void Dispose_DisposesInner()
    {
        var inner = new MemoryStream(new byte[] { 1 });
        var wrapped = new ReleaseOnDisposeStream(inner, () => { });
        wrapped.Dispose();
        Assert.Throws<ObjectDisposedException>(() => inner.ReadByte());
    }

    [Fact]
    public void DoubleDispose_InvokesOnDisposeOnce()
    {
        var count = 0;
        var inner = new MemoryStream(new byte[] { 1 });
        var wrapped = new ReleaseOnDisposeStream(inner, () => count++);
        wrapped.Dispose();
        wrapped.Dispose();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Read_DelegatesToInner()
    {
        var buf = new byte[] { 10, 20, 30 };
        using var inner = new MemoryStream(buf);
        using var wrapped = new ReleaseOnDisposeStream(inner, () => { });
        var outBuf = new byte[3];
        var n = wrapped.Read(outBuf, 0, 3);
        Assert.Equal(3, n);
        Assert.Equal(10, outBuf[0]);
        Assert.Equal(20, outBuf[1]);
        Assert.Equal(30, outBuf[2]);
    }
}
