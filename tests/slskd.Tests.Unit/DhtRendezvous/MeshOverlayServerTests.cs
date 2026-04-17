// <copyright file="MeshOverlayServerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System;
using System.Security.Authentication;
using slskd.DhtRendezvous;
using Xunit;

public class MeshOverlayServerTests
{
    [Fact]
    public void IsExpectedHandshakeNoise_ReturnsTrue_ForCorruptedTlsFrames()
    {
        var exception = new AuthenticationException("Cannot determine the frame size or a corrupted frame was received.");

        Assert.True(MeshOverlayServer.IsExpectedHandshakeNoise(exception));
    }

    [Fact]
    public void IsExpectedHandshakeNoise_ReturnsTrue_ForNestedCorruptedTlsFrames()
    {
        var exception = new InvalidOperationException(
            "Outer wrapper",
            new AuthenticationException("Cannot determine the frame size or a corrupted frame was received."));

        Assert.True(MeshOverlayServer.IsExpectedHandshakeNoise(exception));
    }

    [Fact]
    public void IsExpectedHandshakeNoise_ReturnsFalse_ForUnexpectedFailures()
    {
        var exception = new AuthenticationException("The remote certificate is invalid.");

        Assert.False(MeshOverlayServer.IsExpectedHandshakeNoise(exception));
    }
}
