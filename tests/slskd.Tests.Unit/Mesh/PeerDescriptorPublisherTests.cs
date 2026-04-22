// <copyright file="PeerDescriptorPublisherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh;

using System.Net;
using System.Reflection;
using slskd.Mesh.Dht;
using Xunit;

public class PeerDescriptorPublisherTests
{
    [Theory]
    [InlineData("203.0.113.10:50400", "203.0.113.10", 50400)]
    [InlineData("example.com:443", "example.com", 443)]
    [InlineData("2001:db8::42:50400", "2001:db8::42", 50400)]
    [InlineData("[2001:db8::42]:50400", "2001:db8::42", 50400)]
    public void TryParseAdvertisedEndpoint_ParsesIpv4HostnamesAndIpv6(
        string endpoint,
        string expectedHost,
        int expectedPort)
    {
        var result = InvokeTryParseAdvertisedEndpoint(endpoint);

        Assert.True(result.Success);
        Assert.Equal(expectedHost, result.Host);
        Assert.Equal(expectedPort, result.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[2001:db8::42]")]
    [InlineData("example.com:not-a-port")]
    public void TryParseAdvertisedEndpoint_RejectsMalformedEndpoints(string endpoint)
    {
        var result = InvokeTryParseAdvertisedEndpoint(endpoint);

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("203.0.113.10", 50400, "udp://203.0.113.10:50400")]
    [InlineData("2001:db8::42", 50400, "udp://[2001:db8::42]:50400")]
    public void FormatUdpLegacyEndpoint_UsesExplicitUdpScheme(string host, int port, string expected)
    {
        Assert.Equal(expected, PeerDescriptorPublisher.FormatUdpLegacyEndpoint(host, port));
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    public void ShouldAdvertiseDirectTransport_RequiresSupportedDirectRuntime(
        bool enableDirect,
        bool privacyModeNoClearnetAdvertise,
        bool quicIsSupported,
        bool expected)
    {
        Assert.Equal(expected, PeerDescriptorPublisher.ShouldAdvertiseDirectTransport(enableDirect, privacyModeNoClearnetAdvertise, quicIsSupported));
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2001:4860:4860::8888", true)]
    [InlineData("10.0.0.10", false)]
    [InlineData("172.16.0.10", false)]
    [InlineData("192.168.1.10", false)]
    [InlineData("169.254.1.10", false)]
    [InlineData("100.64.1.10", false)]
    [InlineData("203.0.113.10", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    public void IsPubliclyRoutableAddress_RejectsLocalAndSpecialRanges(string address, bool expected)
    {
        Assert.Equal(expected, PeerDescriptorPublisher.IsPubliclyRoutableAddress(IPAddress.Parse(address)));
    }

    private static (bool Success, string Host, int Port) InvokeTryParseAdvertisedEndpoint(string endpoint)
    {
        var method = typeof(PeerDescriptorPublisher).GetMethod(
            "TryParseAdvertisedEndpoint",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        object[] args = { endpoint, string.Empty, 0 };
        var success = (bool)method!.Invoke(null, args)!;
        return (success, (string)args[1], (int)args[2]);
    }
}
