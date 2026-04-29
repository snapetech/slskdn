// <copyright file="NatEndpointParsingTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using slskd.DhtRendezvous;

namespace slskd.Tests.Unit.Mesh.Nat;

public class NatEndpointParsingTests
{
    [Fact]
    public void NatDetectionService_StunParser_AcceptsBracketedIpv6()
    {
        var parseMethod = typeof(NatDetectionService).GetMethod(
            "TryParseHostAndPort",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var args = new object[] { "[2001:db8::40]:3478", null!, 0 };
        var parsed = (bool)parseMethod!.Invoke(null, args)!;

        Assert.True(parsed);
        Assert.Equal("2001:db8::40", args[1]);
        Assert.Equal(3478, args[2]);
    }
}
