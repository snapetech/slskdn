// <copyright file="MeshOverlayConnectorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Security;
using Xunit;

public class MeshOverlayConnectorTests
{
    [Fact]
    public void ClassifyFailure_ReturnsConnectTimeout_ForOperationCanceled()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new OperationCanceledException("connect timed out"));

        Assert.Equal(OverlayConnectionFailureReason.ConnectTimeout, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsNoRoute_ForSocketNoRoute()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new SocketException((int)SocketError.HostUnreachable));

        Assert.Equal(OverlayConnectionFailureReason.NoRoute, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsConnectionRefused_ForSocketRefused()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new SocketException((int)SocketError.ConnectionRefused));

        Assert.Equal(OverlayConnectionFailureReason.ConnectionRefused, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsConnectionReset_ForSocketReset()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new IOException("reset", new SocketException((int)SocketError.ConnectionReset)));

        Assert.Equal(OverlayConnectionFailureReason.ConnectionReset, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsTlsEof_ForUnexpectedTlsEof()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new IOException("Received an unexpected EOF or 0 bytes from the transport stream."));

        Assert.Equal(OverlayConnectionFailureReason.TlsEof, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsTlsHandshake_ForAuthenticationFailure()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new AuthenticationException("Authentication failed"));

        Assert.Equal(OverlayConnectionFailureReason.TlsHandshake, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsProtocolHandshake_ForProtocolViolation()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new ProtocolViolationException("bad hello"));

        Assert.Equal(OverlayConnectionFailureReason.ProtocolHandshake, reason);
    }

    [Fact]
    public void ClassifyFailure_ReturnsUnknown_ForUnexpectedFailure()
    {
        var reason = MeshOverlayConnector.ClassifyFailure(new InvalidOperationException("boom"));

        Assert.Equal(OverlayConnectionFailureReason.Unknown, reason);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 15)]
    [InlineData(8, 15)]
    public void GetFailureCooldown_UsesBoundedBackoff(int consecutiveFailures, int expectedMinutes)
    {
        var cooldown = MeshOverlayConnector.GetFailureCooldown(consecutiveFailures);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), cooldown);
    }

    [Fact]
    public void IsEndpointCoolingDown_ReturnsTrueOnlyBeforeSuppressedUntil()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.True(MeshOverlayConnector.IsEndpointCoolingDown(now, now.AddSeconds(30)));
        Assert.False(MeshOverlayConnector.IsEndpointCoolingDown(now, now));
        Assert.False(MeshOverlayConnector.IsEndpointCoolingDown(now, null));
    }
}
