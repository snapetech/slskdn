// <copyright file="DhtRendezvousServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

    [Fact]
    public async Task OnPeersFound_WhenCandidateUsesConfiguredDhtPort_IgnoresEndpoint()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions
            {
                Enabled = true,
                OverlayPort = 50305,
                DhtPort = 50306,
            });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.15:50306"));
        await Task.Delay(100);

        connector.Verify(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()), Times.Never);
        Assert.Equal(0, service.DiscoveredPeerCount);
        Assert.Equal(0, peerManager.GetStatistics().TotalPeers);
        Assert.Equal(1, service.GetStats().TotalCandidatesSkippedDhtPort);
    }

    [Fact]
    public async Task OnPeersFound_WhenOverlayAndDhtPortsMatch_DoesNotFilterCandidate()
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
            new DhtRendezvousOptions
            {
                Enabled = true,
                OverlayPort = 50305,
                DhtPort = 50305,
            });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.16:50305"));
        await Task.Delay(100);

        connector.Verify(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()), Times.Once);
        Assert.Equal(1, service.DiscoveredPeerCount);
        Assert.Equal(1, service.GetStats().TotalCandidatesAccepted);
    }

    [Fact]
    public void GetBootstrapTimeoutSeconds_UsesAdaptiveWarmColdAndLanOnlyWindows()
    {
        var options = new DhtRendezvousOptions
        {
            BootstrapTimeoutSeconds = 120,
            ColdBootstrapTimeoutSeconds = 180,
            LanOnlyBootstrapTimeoutSeconds = 30,
        };

        Assert.Equal(120, DhtRendezvousService.GetBootstrapTimeoutSeconds(options, savedNodeTableBytes: 256));
        Assert.Equal(180, DhtRendezvousService.GetBootstrapTimeoutSeconds(options, savedNodeTableBytes: 0));

        options.LanOnly = true;

        Assert.Equal(30, DhtRendezvousService.GetBootstrapTimeoutSeconds(options, savedNodeTableBytes: 0));
        Assert.Equal(30, DhtRendezvousService.GetBootstrapTimeoutSeconds(options, savedNodeTableBytes: 256));
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
        Assert.Equal(1, service.GetStats().TotalCandidatesSkippedReconnectBackoff);
    }

    [Fact]
    public async Task OnPeersFound_WhenPeerFailsRepeatedly_UsesProgressiveReconnectBackoff()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector
            .SetupSequence(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()))
            .ReturnsAsync(0)
            .ReturnsAsync(0)
            .ReturnsAsync(1);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions { Enabled = true });

        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.17:50305"));
        await Task.Delay(100);

        SetLastAttempt(service, "203.0.113.17:50305", DateTimeOffset.UtcNow.AddMinutes(-6));
        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.17:50305"));
        await Task.Delay(100);

        SetLastAttempt(service, "203.0.113.17:50305", DateTimeOffset.UtcNow.AddMinutes(-6));
        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.17:50305"));
        await Task.Delay(100);

        connector.Verify(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()), Times.Exactly(2));
        Assert.Equal(1, service.GetStats().TotalCandidatesSkippedReconnectBackoff);

        SetLastAttempt(service, "203.0.113.17:50305", DateTimeOffset.UtcNow.AddMinutes(-16));
        InvokeOnPeersFound(service, CreatePeersFoundEventArgs("ipv4://203.0.113.17:50305"));
        await Task.Delay(100);

        connector.Verify(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()), Times.Exactly(3));
        var peer = peerManager.GetPeer("203.0.113.17:50305");
        Assert.NotNull(peer);
        Assert.True(peer!.SupportsOnionRouting);
    }

    [Fact]
    public async Task OnPeersFound_WhenConnectorCapacityIsFull_DefersExtraCandidatesWithoutCountingAttempts()
    {
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        var pendingConnection = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        connector
            .Setup(x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()))
            .Returns(pendingConnection.Task);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            Mock.Of<IMeshOverlayServer>(),
            connector.Object,
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            peerManager,
            new DhtRendezvousOptions { Enabled = true });

        InvokeOnPeersFound(
            service,
            CreatePeersFoundEventArgs(
                "ipv4://203.0.113.20:50305",
                "ipv4://203.0.113.21:50305",
                "ipv4://203.0.113.22:50305",
                "ipv4://203.0.113.23:50305"));

        await WaitForAsync(
            () => service.GetStats().TotalConnectionsAttempted == DhtRendezvousService.MaxConcurrentPeerConnectionAttempts,
            TimeSpan.FromSeconds(1));

        connector.Verify(
            x => x.ConnectToCandidatesAsync(It.IsAny<IEnumerable<IPEndPoint>>()),
            Times.Exactly(DhtRendezvousService.MaxConcurrentPeerConnectionAttempts));
        Assert.Equal(DhtRendezvousService.MaxConcurrentPeerConnectionAttempts, service.GetStats().TotalConnectionsAttempted);
        Assert.Equal(1, service.GetStats().TotalCandidatesDeferredConnectorCapacity);

        pendingConnection.SetResult(0);
    }

    [Fact]
    public async Task StartOverlayServerIfPossible_WhenPortIsTemporarilyInUse_RetriesAndStartsOverlay()
    {
        var overlayServer = new Mock<IMeshOverlayServer>();
        overlayServer
            .SetupSequence(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException((int)SocketError.AddressAlreadyInUse))
            .ThrowsAsync(new SocketException((int)SocketError.AddressAlreadyInUse))
            .Returns(Task.CompletedTask);

        var service = new DhtRendezvousService(
            NullLogger<DhtRendezvousService>.Instance,
            overlayServer.Object,
            Mock.Of<IMeshOverlayConnector>(),
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            new MeshPeerManager(NullLogger<MeshPeerManager>.Instance),
            new DhtRendezvousOptions { Enabled = true, OverlayPort = 50305 });

        var result = await InvokeStartOverlayServerIfPossibleAsync(service);

        Assert.True(result);
        overlayServer.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
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

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(9, 60)]
    public void GetPeerReconnectInterval_ProgressivelyDeprioritizesRepeatedFailures(
        int consecutiveFailures,
        int expectedMinutes)
    {
        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            DhtRendezvousService.GetPeerReconnectInterval(consecutiveFailures));
    }

    private static void SetLastAttempt(DhtRendezvousService service, string peerId, DateTimeOffset lastAttempt)
    {
        var field = typeof(DhtRendezvousService).GetField("_peerConnectionAttemptedAt", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_peerConnectionAttemptedAt was not found.");
        var attempts = (ConcurrentDictionary<string, DateTimeOffset>)field.GetValue(service)!;
        attempts[peerId] = lastAttempt;
    }

    private static async Task<bool> InvokeStartOverlayServerIfPossibleAsync(DhtRendezvousService service)
    {
        var method = typeof(DhtRendezvousService).GetMethod("StartOverlayServerIfPossibleAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StartOverlayServerIfPossibleAsync was not found.");

        var task = (Task<bool>)method.Invoke(service, new object[] { CancellationToken.None })!;
        return await task;
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var startedAt = DateTimeOffset.UtcNow;
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow - startedAt > timeout)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }

    private static PeersFoundEventArgs CreatePeersFoundEventArgs(params string[] connectionUris)
    {
        var infoHash = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1")));
        var peers = connectionUris.Select(uri => new PeerInfo(new Uri(uri))).ToList();
        return new PeersFoundEventArgs(infoHash, peers);
    }
}
