// <copyright file="TransportAddressParsingTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.Overlay;

namespace slskd.Tests.Unit.Mesh.Transport;

public class TransportAddressParsingTests
{
    [Fact]
    public void TorSocksTransport_ParseSocksAddress_AcceptsBracketedIpv6()
    {
        var parseMethod = typeof(TorSocksTransport).GetMethod(
            "ParseSocksAddress",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = ((string Host, int Port))parseMethod!.Invoke(null, new object[] { "[::1]:9050" })!;

        Assert.Equal("::1", result.Host);
        Assert.Equal(9050, result.Port);
    }

    [Fact]
    public void I2PTransport_ParseSamAddress_AcceptsBracketedIpv6()
    {
        var parseMethod = typeof(I2PTransport).GetMethod(
            "ParseSamAddress",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = ((string Host, int Port))parseMethod!.Invoke(null, new object[] { "[::1]:7656" })!;

        Assert.Equal("::1", result.Host);
        Assert.Equal(7656, result.Port);
    }

    [Fact]
    public async Task RelayOnlyTransport_ParseEndpointAsync_AcceptsBracketedIpv6()
    {
        var parseMethod = typeof(RelayOnlyTransport).GetMethod(
            "ParseEndpointAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var parseTask = (Task<IPEndPoint?>)parseMethod!.Invoke(null, new object[] { "[::1]:4040", CancellationToken.None })!;
        var endpoint = await parseTask;

        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.IPv6Loopback, endpoint.Address);
        Assert.Equal(4040, endpoint.Port);
    }

    [Fact]
    public async Task RelayOnlyTransport_TrustedRelayPeerWithBracketedIpv6_IsAvailable()
    {
        var transport = new RelayOnlyTransport(
            new RelayOnlyOptions
            {
                TrustedRelayPeers = new List<string> { "[::1]:4040" },
            },
            Mock.Of<IOverlayDataPlane>(),
            Mock.Of<ILogger<RelayOnlyTransport>>());

        var result = await transport.IsAvailableAsync();

        Assert.True(result);
    }
}
