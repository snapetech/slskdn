// <copyright file="PeerResolutionServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh.Dht;
using slskd.PodCore;
using Xunit;

public class PeerResolutionServiceTests
{
    [Theory]
    [InlineData("127.0.0.1:2234", "127.0.0.1", 2234)]
    [InlineData("udp://127.0.0.1:2235", "127.0.0.1", 2235)]
    [InlineData("[2001:db8::5]:2236", "2001:db8::5", 2236)]
    [InlineData("tcp://[2001:db8::6]:2237", "2001:db8::6", 2237)]
    public async Task ResolvePeerIdToEndpointAsync_ParsesIpv4AndIpv6Endpoints(
        string endpointText,
        string expectedAddress,
        int expectedPort)
    {
        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PeerMetadata>("peer:metadata:peer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerMetadata
            {
                PeerId = "peer-1",
                Username = "alice",
                Endpoint = endpointText,
            });

        var service = new PeerResolutionService(dht.Object, NullLogger<PeerResolutionService>.Instance);

        var endpoint = await service.ResolvePeerIdToEndpointAsync("peer-1");

        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.Parse(expectedAddress), endpoint!.Address);
        Assert.Equal(expectedPort, endpoint.Port);
    }

    [Fact]
    public async Task ResolvePeerIdToEndpointAsync_ParsesTrimmedHostnameEndpoint()
    {
        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PeerMetadata>("peer:metadata:peer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerMetadata
            {
                PeerId = "peer-1",
                Username = "alice",
                Endpoint = " tcp://localhost:2238 ",
            });

        var service = new PeerResolutionService(dht.Object, NullLogger<PeerResolutionService>.Instance);

        var endpoint = await service.ResolvePeerIdToEndpointAsync(" peer-1 ");

        Assert.NotNull(endpoint);
        Assert.Equal(2238, endpoint!.Port);
    }

    [Fact]
    public async Task ResolvePeerIdToUsernameAsync_TrimsPeerIdAndUsername()
    {
        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PeerMetadata>("peer:metadata:peer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerMetadata
            {
                PeerId = "peer-1",
                Username = " alice ",
            });

        var service = new PeerResolutionService(dht.Object, NullLogger<PeerResolutionService>.Instance);

        var username = await service.ResolvePeerIdToUsernameAsync(" peer-1 ");

        Assert.Equal("alice", username);
    }

    [Fact]
    public async Task ResolvePeerIdToUsernameAsync_FallbackUsesTrimmedPeerId()
    {
        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PeerMetadata>("peer:metadata:peer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PeerMetadata?)null);

        var service = new PeerResolutionService(dht.Object, NullLogger<PeerResolutionService>.Instance);

        var username = await service.ResolvePeerIdToUsernameAsync(" peer-1 ");

        Assert.Equal("peer-1", username);
    }

    [Fact]
    public async Task ResolvePeerIdToEndpointAsync_ReusesCachedUsernameAliasEndpoint()
    {
        var dht = new Mock<IMeshDhtClient>();
        var service = new PeerResolutionService(dht.Object, NullLogger<PeerResolutionService>.Instance);
        service.RegisterPeerMapping("peer-1", "alice", new IPEndPoint(IPAddress.Loopback, 2238));

        var endpoint = await service.ResolvePeerIdToEndpointAsync(" alice ");

        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.Loopback, endpoint!.Address);
        Assert.Equal(2238, endpoint.Port);
        dht.Verify(x => x.GetAsync<PeerMetadata>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
