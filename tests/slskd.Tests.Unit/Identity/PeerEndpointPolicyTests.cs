// <copyright file="PeerEndpointPolicyTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity;

using slskd.Identity;
using Xunit;

// HARDENING-2026-04-20 H10: GET /api/v0/profile/{peerId} is [AllowAnonymous], so any
// PeerEndpoint.Address that survives into the served PeerProfile is publicly readable.
// PeerEndpointPolicy is the gate that refuses to publish loopback/private/link-local/
// cloud-metadata/malformed entries — this test suite exercises the classification matrix.
public class PeerEndpointPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("relative/path")]
    [InlineData("http://")]
    public void Leaky_NullEmptyOrMalformed(string? address)
    {
        Assert.True(PeerEndpointPolicy.IsLeakyAddress(address));
    }

    [Theory]
    [InlineData("http://localhost:5030")]
    [InlineData("https://LOCALHOST:5030")]
    [InlineData("https://foo.local:5030")]
    [InlineData("https://foo.localhost:5030")]
    [InlineData("https://server.internal:5030")]
    public void Leaky_LocalNames(string address)
    {
        Assert.True(PeerEndpointPolicy.IsLeakyAddress(address));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5030")]
    [InlineData("http://127.1.2.3:5030")]
    [InlineData("http://[::1]:5030")]
    public void Leaky_Loopback(string address)
    {
        Assert.True(PeerEndpointPolicy.IsLeakyAddress(address));
    }

    [Theory]
    [InlineData("https://10.0.0.1:5030")]
    [InlineData("https://10.255.255.1:5030")]
    [InlineData("https://172.16.0.5:5030")]
    [InlineData("https://172.31.255.1:5030")]
    [InlineData("https://192.168.1.100:5030")]
    [InlineData("https://192.168.50.85:5030")]
    public void Leaky_Rfc1918(string address)
    {
        Assert.True(PeerEndpointPolicy.IsLeakyAddress(address));
    }

    [Theory]
    [InlineData("https://169.254.1.2:5030")]
    [InlineData("https://169.254.169.254/")] // AWS/GCP/DO/Azure IMDS
    public void Leaky_LinkLocalAndMetadata(string address)
    {
        Assert.True(PeerEndpointPolicy.IsLeakyAddress(address));
    }

    [Theory]
    [InlineData("https://[fe80::1]:5030")]
    [InlineData("https://[fc00::abcd]:5030")]
    [InlineData("https://[fd12:3456:789a::1]:5030")]
    public void Leaky_Ipv6LinkLocalOrUla(string address)
    {
        Assert.True(PeerEndpointPolicy.IsLeakyAddress(address));
    }

    [Theory]
    [InlineData("https://peer.example.com:5030")]
    [InlineData("https://slskd.keith.snape.tech")]
    [InlineData("https://203.0.113.5:5030")] // TEST-NET-3, classified Public
    [InlineData("https://8.8.8.8")]
    [InlineData("quic://peer.example.com:5030")]
    [InlineData("relay://relayId/peerId")]
    public void Safe_PublicHostOrRoutable(string address)
    {
        Assert.False(PeerEndpointPolicy.IsLeakyAddress(address));
    }
}
