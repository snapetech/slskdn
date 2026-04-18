// <copyright file="DhtRendezvousServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Collections.Concurrent;
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
    public void OnPeersFound_WhenRendezvousPeersAreDiscovered_TracksUnverifiedCandidateWithoutClaimingCircuitCapability()
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
        Assert.Equal(0, stats.OnionRoutingPeers);
        Assert.Equal("dht-discovered", peer!.Version);
        Assert.False(peer.SupportsOnionRouting);
        Assert.Equal(IPAddress.Parse("203.0.113.10"), peer.GetBestAddress().Address);
    }

    [Fact]
    public async Task OnPeersFound_WhenImmediateOverlayConnectFails_PeerRemainsTrackedButNotCircuitCapable()
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
        var peer = peerManager.GetPeer("203.0.113.11:50305");

        Assert.Empty(circuitPeers);
        Assert.NotNull(peer);
        Assert.Equal("dht-discovered", peer!.Version);
        Assert.False(peer.SupportsOnionRouting);
    }

    private static void InvokeOnPeersFound(DhtRendezvousService service, PeersFoundEventArgs eventArgs)
    {
        var method = typeof(DhtRendezvousService).GetMethod("OnPeersFound", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnPeersFound was not found.");

        method.Invoke(service, new object?[] { null, eventArgs });
    }

    [Fact]
    public async Task OnPeersFound_WhenOverlayConnectSucceeds_PeerBecomesCircuitCapable()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector
            .Setup(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()))
            .ReturnsAsync(1);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions { Enabled = true });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.12:50305"));
        await Task.Delay(100);

        var circuitPeers = await peerManager.GetCircuitPeersAsync();
        var peer = Assert.Single(circuitPeers);
        Assert.Equal("203.0.113.12:50305", peer.PeerId);
        Assert.True(peer.SupportsOnionRouting);
        Assert.Equal("overlay-verified", peer.Version);
    }


    [Fact]
    public async Task OnPeersFound_WhenPeerIsRediscoveredAfterBackoff_FailedCandidatesAreRetried()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector
            .SetupSequence(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()))
            .ReturnsAsync(0)
            .ReturnsAsync(1);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions { Enabled = true });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.13:50305"));
        await Task.Delay(100);

        SetLastAttempt(service, "203.0.113.13:50305", DateTimeOffset.UtcNow.AddMinutes(-6));
        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.13:50305"));
        await Task.Delay(100);

        connector.Verify(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()), Times.Exactly(2));
        var peer = peerManager.GetPeer("203.0.113.13:50305");
        Assert.NotNull(peer);
        Assert.True(peer!.SupportsOnionRouting);
        Assert.Equal("overlay-verified", peer.Version);
    }

    [Fact]
    public async Task OnPeersFound_WhenPeerIsRediscoveredImmediately_DoesNotHammerFailedCandidate()
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

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.14:50305"));
        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.14:50305"));
        await Task.Delay(100);

        connector.Verify(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()), Times.Once);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void ShouldRetryPeerConnection_RespectsVerificationConnectionAndPendingState(
        bool isConnected,
        bool isVerified,
        bool isPending,
        bool hasRecentAttempt,
        bool expected)
    {
        var lastAttempt = hasRecentAttempt ? DateTimeOffset.UtcNow.AddMinutes(-1) : (DateTimeOffset?)null;

        var result = DhtRendezvousService.ShouldRetryPeerConnection(
            DateTimeOffset.UtcNow,
            lastAttempt,
            isConnected,
            isVerified,
            isPending,
            TimeSpan.FromMinutes(5));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldRetryPeerConnection_WhenBackoffElapsed_ReturnsTrue()
    {
        var result = DhtRendezvousService.ShouldRetryPeerConnection(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-6),
            isConnected: false,
            isVerified: false,
            isPending: false,
            reconnectInterval: TimeSpan.FromMinutes(5));

        Assert.True(result);
    }

    private static void SetLastAttempt(DhtRendezvousService service, string peerId, DateTimeOffset lastAttempt)
    {
        var field = typeof(DhtRendezvousService).GetField("_peerConnectionAttemptedAt", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_peerConnectionAttemptedAt was not found.");
        var attempts = (ConcurrentDictionary<string, DateTimeOffset>)field.GetValue(service)!;
        attempts[peerId] = lastAttempt;
    }

    private static PeersFoundEventArgs CreatePeersFoundEventArgs(string connectionUri)
    {
        var infoHash = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1")));
        var peers = new List<PeerInfo> { new(new Uri(connectionUri)) };
        return new PeersFoundEventArgs(infoHash, peers);
    }
}
