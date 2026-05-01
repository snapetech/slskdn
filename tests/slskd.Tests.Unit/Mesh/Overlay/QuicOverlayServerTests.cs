// <copyright file="QuicOverlayServerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.Overlay;

using System.Net;
using slskd.Mesh.Overlay;
using Xunit;

public class QuicOverlayServerTests
{
    [Fact]
    public void GetListenEndPoint_WhenSharingDhtPort_UsesLoopbackBackendPort()
    {
        var endpoint = QuicOverlayServer.GetListenEndPoint(new OverlayOptions
        {
            ShareQuicWithDhtPort = true,
            QuicListenPort = 50305,
            QuicBackendListenPort = 55305,
        });

        Assert.Equal(IPAddress.Loopback, endpoint.Address);
        Assert.Equal(55305, endpoint.Port);
    }

    [Fact]
    public void GetListenEndPoint_WhenNotSharingDhtPort_UsesPublicQuicPort()
    {
        var endpoint = QuicOverlayServer.GetListenEndPoint(new OverlayOptions
        {
            ShareQuicWithDhtPort = false,
            QuicListenPort = 50402,
            QuicBackendListenPort = 50403,
        });

        Assert.Equal(IPAddress.Any, endpoint.Address);
        Assert.Equal(50402, endpoint.Port);
    }
}
