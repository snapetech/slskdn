// <copyright file="StunNatDetectorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Nat;

using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using Xunit;

public sealed class StunNatDetectorTests
{
    [Fact]
    public void TryParseHostAndPort_TrimsEndpoint()
    {
        var method = typeof(StunNatDetector).GetMethod("TryParseHostAndPort", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object[] { "  stun.example.com:3478  ", string.Empty, 0 };
        var parsed = Assert.IsType<bool>(method!.Invoke(null, args)!);

        Assert.True(parsed);
        Assert.Equal("stun.example.com", args[1]);
        Assert.Equal(3478, args[2]);
    }

    [Fact]
    public void ParseMappedAddress_ParsesIpv6MappedAddress()
    {
        var method = typeof(StunNatDetector).GetMethod("ParseMappedAddress", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var txn = new byte[12];
        var packet = new byte[44];
        packet[21] = 0x01;
        packet[23] = 20;
        packet[25] = 0x02;
        packet[26] = 0x13;
        packet[27] = 0x88;
        var address = IPAddress.Parse("2001:db8::1").GetAddressBytes();
        Array.Copy(address, 0, packet, 28, 16);

        var endpoint = method!.Invoke(null, new object[] { packet, txn }) as IPEndPoint;

        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.Parse("2001:db8::1"), endpoint!.Address);
        Assert.Equal(5000, endpoint.Port);
    }

    [Fact]
    public void ParseMappedAddress_SkipsPaddedAttributes()
    {
        var method = typeof(StunNatDetector).GetMethod("ParseMappedAddress", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var txn = new byte[12];
        var packet = new byte[40];

        packet[20] = 0x80;
        packet[21] = 0x22;
        packet[23] = 0x01;
        packet[24] = 0x01;

        packet[28] = 0x00;
        packet[29] = 0x01;
        packet[31] = 0x08;
        packet[33] = 0x01;
        packet[34] = 0x13;
        packet[35] = 0x88;
        packet[36] = 192;
        packet[37] = 0;
        packet[38] = 2;
        packet[39] = 10;

        var endpoint = method!.Invoke(null, new object[] { packet, txn }) as IPEndPoint;

        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), endpoint!.Address);
        Assert.Equal(5000, endpoint.Port);
    }
}
