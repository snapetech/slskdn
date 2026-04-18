// <copyright file="DhtRendezvousServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MonoTorrent;
using MonoTorrent.Dht;
using Moq;
using slskd.DhtRendezvous;
using slskd.Mesh;
using Xunit;

public class DhtRendezvousServiceTests
{
    [Fact]
    public void OnPeersFound_WhenRendezvousPeersAreDiscovered_PopulatesCircuitPeerInventoryImmediately()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector
            .Setup(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()))
            .ReturnsAsync(0);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions { Enabled = true });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.10:50305"));

        var stats = peerManager.GetStatistics();
        var peer = peerManager.GetPeer("203.0.113.10:50305");

        Assert.NotNull(peer);
        Assert.Equal(1, stats.TotalPeers);
        Assert.Equal(1, stats.ActivePeers);
        Assert.Equal(1, stats.OnionRoutingPeers);
        Assert.Equal(IPAddress.Parse("203.0.113.10"), peer!.GetBestAddress().Address);
    }

    [Fact]
    public async Task OnPeersFound_WhenImmediateOverlayConnectFails_PeerStillRemainsAvailableForCircuitBuilding()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector
            .Setup(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()))
            .ReturnsAsync(0);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions { Enabled = true });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.11:50305"));
        await Task.Delay(100);

        var circuitPeers = await peerManager.GetCircuitPeersAsync();

        Assert.Single(circuitPeers);
        Assert.Equal("203.0.113.11:50305", circuitPeers[0].PeerId);
    }

    private static void InvokeOnPeersFound(DhtRendezvousService service, PeersFoundEventArgs eventArgs)
    {
        var method = typeof(DhtRendezvousService).GetMethod("OnPeersFound", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnPeersFound was not found.");

        method.Invoke(service, new object?[] { null, eventArgs });
    }

    private static PeersFoundEventArgs CreatePeersFoundEventArgs(string connectionUri)
    {
        var infoHash = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1")));
        var peers = new List<PeerInfo> { new(new Uri(connectionUri)) };
        return new PeersFoundEventArgs(infoHash, peers);
    }
}
