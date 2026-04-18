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
}
