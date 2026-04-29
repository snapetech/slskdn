// <copyright file="OverlayLogSanitizerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using slskd.DhtRendezvous;
using Xunit;

public class OverlayLogSanitizerTests
{
    [Fact]
    public void Username_RedactsRawSoulseekName()
    {
        var result = OverlayLogSanitizer.Username("keef_shape");

        Assert.Equal("k***e (10 chars)", result);
        Assert.DoesNotContain("keef_shape", result);
    }

    [Fact]
    public void PeerId_RedactsUsernameBackedPeerId()
    {
        var result = OverlayLogSanitizer.PeerId("spynn56");

        Assert.Equal("s***6 (7 chars)", result);
        Assert.DoesNotContain("spynn56", result);
    }

    [Fact]
    public void Endpoint_RedactsPublicIpButKeepsPortForTriage()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("24.109.206.134"), 34160);

        var result = OverlayLogSanitizer.Endpoint(endpoint);

        Assert.Equal("xxx.xxx.xxx.134:34160", result);
        Assert.DoesNotContain("24.109.206", result);
    }
}
