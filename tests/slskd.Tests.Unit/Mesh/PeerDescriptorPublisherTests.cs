// <copyright file="PeerDescriptorPublisherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh;

using System.Reflection;
using slskd.Mesh.Dht;
using Xunit;

public class PeerDescriptorPublisherTests
{
    [Theory]
    [InlineData("203.0.113.10:2234", "203.0.113.10", 2234)]
    [InlineData("example.com:443", "example.com", 443)]
    [InlineData("2001:db8::42:2235", "2001:db8::42", 2235)]
    [InlineData("[2001:db8::42]:2235", "2001:db8::42", 2235)]
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
