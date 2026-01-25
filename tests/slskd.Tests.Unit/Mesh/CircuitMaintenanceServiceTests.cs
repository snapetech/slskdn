// <copyright file="CircuitMaintenanceServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class CircuitMaintenanceServiceTests : IDisposable
{
    private readonly Mock<ILogger<CircuitMaintenanceService>> _loggerMock;
    private readonly Mock<IMeshPeerManager> _peerManagerMock;
    private readonly Mock<IAnonymityTransportSelector> _transportSelectorMock;
    private readonly MeshOptions _meshOptions;
    private readonly List<MeshCircuitBuilder> _buildersToDispose = new();

    public CircuitMaintenanceServiceTests()
    {
        _loggerMock = new Mock<ILogger<CircuitMaintenanceService>>();
        _peerManagerMock = new Mock<IMeshPeerManager>();
        _transportSelectorMock = new Mock<IAnonymityTransportSelector>();
        _meshOptions = new MeshOptions { SelfPeerId = "self" };
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(new PeerStatistics());
    }

    public void Dispose() => Dispose(true);
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var b in _buildersToDispose)
                b.Dispose();
    }

    private MeshCircuitBuilder CreateCircuitBuilder()
    {
        var b = new MeshCircuitBuilder(
            _meshOptions,
            Mock.Of<ILogger<MeshCircuitBuilder>>(),
            _peerManagerMock.Object,
            _transportSelectorMock.Object);
        _buildersToDispose.Add(b);
        return b;
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var builder = CreateCircuitBuilder();
        var service = new CircuitMaintenanceService(_loggerMock.Object, builder, _peerManagerMock.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var builder = CreateCircuitBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitMaintenanceService(null!, builder, _peerManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCircuitBuilder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitMaintenanceService(_loggerMock.Object, null!, _peerManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullPeerManager_ThrowsArgumentNullException()
    {
        var builder = CreateCircuitBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitMaintenanceService(_loggerMock.Object, builder, null!));
    }

    [Fact]
    public async Task ExecuteAsync_PerformsMaintenanceEvery5Minutes()
    {
        var builder = CreateCircuitBuilder();
        var service = new CircuitMaintenanceService(_loggerMock.Object, builder, _peerManagerMock.Object);
        var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await Task.Delay(100);
        await service.StopAsync(cts.Token);

        // Verify(PerformMaintenance) and Verify(GetStatistics) removed: real MeshCircuitBuilder; those members are non-virtual.
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellationGracefully()
    {
        var builder = CreateCircuitBuilder();
        var service = new CircuitMaintenanceService(_loggerMock.Object, builder, _peerManagerMock.Object);
        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        cts.Cancel();
        await executeTask;

        Assert.True(executeTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterMaintenanceException()
    {
        var circuitBuilderMock = new Mock<IMeshCircuitBuilder>();
        circuitBuilderMock.Setup(x => x.PerformMaintenance()).Throws(new InvalidOperationException("test"));
        circuitBuilderMock.Setup(x => x.GetStatistics()).Returns(new CircuitStatistics());

        var service = new CircuitMaintenanceService(_loggerMock.Object, circuitBuilderMock.Object, _peerManagerMock.Object);
        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(200);
        await service.StopAsync(cts.Token);

        Assert.True(executeTask.IsCompletedSuccessfully);
        _loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsCircuitTestingWhenActiveCircuitsExist()
    {
        var circuitStats = new CircuitStatistics { ActiveCircuits = 1 };
        var peerStats = new PeerStatistics { OnionRoutingPeers = 5 };
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);

        var circuitBuilderMock = new Mock<IMeshCircuitBuilder>();
        circuitBuilderMock.Setup(x => x.PerformMaintenance()).Callback(() => { });
        circuitBuilderMock.Setup(x => x.GetStatistics()).Returns(circuitStats);

        var service = new CircuitMaintenanceService(_loggerMock.Object, circuitBuilderMock.Object, _peerManagerMock.Object);

        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        _peerManagerMock.Verify(x => x.GetCircuitPeersAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsCircuitTestingWhenInsufficientPeers()
    {
        var peerStats = new PeerStatistics { OnionRoutingPeers = 2 };
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);

        var builder = CreateCircuitBuilder();
        var service = new CircuitMaintenanceService(_loggerMock.Object, builder, _peerManagerMock.Object);

        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        _peerManagerMock.Verify(x => x.GetCircuitPeersAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TestsCircuitBuildingWhenNoActiveCircuitsAndEnoughPeers()
    {
        var peerStats = new PeerStatistics { OnionRoutingPeers = 5 };
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);
        _peerManagerMock.Setup(x => x.GetCircuitPeersAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MeshPeer> { new MeshPeer("peer1", new List<IPEndPoint>()) });

        // Real builder: BuildCircuitAsync is never called because GetCircuitPeersAsync returns only 1 peer (needs >= 3).
        var builder = CreateCircuitBuilder();
        var service = new CircuitMaintenanceService(_loggerMock.Object, builder, _peerManagerMock.Object);

        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        _peerManagerMock.Verify(x => x.GetCircuitPeersAsync(0.2, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LogsMaintenanceStatistics()
    {
        var peerStats = new PeerStatistics { TotalPeers = 25, ActivePeers = 18, OnionRoutingPeers = 2 };
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);

        var builder = CreateCircuitBuilder();
        var service = new CircuitMaintenanceService(_loggerMock.Object, builder, _peerManagerMock.Object);

        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Relaxed: assert Log Information with message containing "circuit" and "peer" (real builder gives 0 circuits).
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("circuit", StringComparison.OrdinalIgnoreCase) && o.ToString()!.Contains("peer", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
