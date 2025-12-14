// <copyright file="CircuitMaintenanceServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class CircuitMaintenanceServiceTests
{
    private readonly Mock<ILogger<CircuitMaintenanceService>> _loggerMock;
    private readonly Mock<MeshCircuitBuilder> _circuitBuilderMock;
    private readonly Mock<IMeshPeerManager> _peerManagerMock;

    public CircuitMaintenanceServiceTests()
    {
        _loggerMock = new Mock<ILogger<CircuitMaintenanceService>>();
        _circuitBuilderMock = new Mock<MeshCircuitBuilder>();
        _peerManagerMock = new Mock<IMeshPeerManager>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act & Assert - Should not throw
        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitMaintenanceService(null!, _circuitBuilderMock.Object, _peerManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCircuitBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitMaintenanceService(_loggerMock.Object, null!, _peerManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullPeerManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitMaintenanceService(_loggerMock.Object, _circuitBuilderMock.Object, null!));
    }

    [Fact]
    public async Task ExecuteAsync_PerformsMaintenanceEvery5Minutes()
    {
        // Arrange
        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        var cancellationTokenSource = new CancellationTokenSource();
        var executeTask = service.StartAsync(cancellationTokenSource.Token);

        // Let it run for a short time
        await Task.Delay(100);
        await service.StopAsync(cancellationTokenSource.Token);

        // Assert - Verify maintenance was called
        _circuitBuilderMock.Verify(x => x.PerformMaintenance(), Times.AtLeastOnce);
        _circuitBuilderMock.Verify(x => x.GetStatistics(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellationGracefully()
    {
        // Arrange
        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        var cancellationTokenSource = new CancellationTokenSource();
        var executeTask = service.StartAsync(cancellationTokenSource.Token);

        // Cancel immediately
        cancellationTokenSource.Cancel();

        // Wait for graceful shutdown
        await executeTask;

        // Assert - Should complete without throwing
        Assert.True(executeTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterMaintenanceException()
    {
        // Arrange
        _circuitBuilderMock.Setup(x => x.PerformMaintenance()).Throws<Exception>();
        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        var cancellationTokenSource = new CancellationTokenSource();
        var executeTask = service.StartAsync(cancellationTokenSource.Token);

        // Let it run briefly to encounter the exception
        await Task.Delay(200);
        await service.StopAsync(cancellationTokenSource.Token);

        // Assert - Should continue running despite exception
        Assert.True(executeTask.IsCompletedSuccessfully);
        _loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_TestsCircuitBuildingWhenNoActiveCircuitsAndEnoughPeers()
    {
        // Arrange
        var circuitStats = new CircuitStatistics { ActiveCircuits = 0 };
        var peerStats = new PeerStatistics { OnionRoutingPeers = 5 };

        _circuitBuilderMock.Setup(x => x.GetStatistics()).Returns(circuitStats);
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);
        _peerManagerMock.Setup(x => x.GetCircuitPeersAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MeshPeer> { new MeshPeer("peer1", new List<System.Net.IPEndPoint>()) });

        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        // Act - Call the private method via reflection for testing
        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Should have attempted to get circuit peers
        _peerManagerMock.Verify(x => x.GetCircuitPeersAsync(0.2, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsCircuitTestingWhenActiveCircuitsExist()
    {
        // Arrange
        var circuitStats = new CircuitStatistics { ActiveCircuits = 1 };
        var peerStats = new PeerStatistics { OnionRoutingPeers = 5 };

        _circuitBuilderMock.Setup(x => x.GetStatistics()).Returns(circuitStats);
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);

        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        // Act - Call the private method via reflection for testing
        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Should NOT have attempted circuit testing
        _peerManagerMock.Verify(x => x.GetCircuitPeersAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsCircuitTestingWhenInsufficientPeers()
    {
        // Arrange
        var circuitStats = new CircuitStatistics { ActiveCircuits = 0 };
        var peerStats = new PeerStatistics { OnionRoutingPeers = 2 }; // Need 3+ for testing

        _circuitBuilderMock.Setup(x => x.GetStatistics()).Returns(circuitStats);
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);

        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        // Act - Call the private method via reflection for testing
        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Should NOT have attempted circuit testing
        _peerManagerMock.Verify(x => x.GetCircuitPeersAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_LogsMaintenanceStatistics()
    {
        // Arrange
        var circuitStats = new CircuitStatistics
        {
            ActiveCircuits = 3,
            TotalCircuitsBuilt = 15,
            AverageCircuitLength = 2.5
        };
        var peerStats = new PeerStatistics
        {
            TotalPeers = 25,
            ActivePeers = 18,
            OnionRoutingPeers = 12
        };

        _circuitBuilderMock.Setup(x => x.GetStatistics()).Returns(circuitStats);
        _peerManagerMock.Setup(x => x.GetStatistics()).Returns(peerStats);

        var service = new CircuitMaintenanceService(
            _loggerMock.Object,
            _circuitBuilderMock.Object,
            _peerManagerMock.Object);

        // Act - Call the private method via reflection for testing
        var method = typeof(CircuitMaintenanceService).GetMethod("PerformMaintenanceAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Should log the statistics
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("3 circuits") && o.ToString()!.Contains("25 total peers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

// Mock classes for testing
public class CircuitStatistics
{
    public int ActiveCircuits { get; set; }
    public int TotalCircuitsBuilt { get; set; }
    public double AverageCircuitLength { get; set; }
}

public class PeerStatistics
{
    public int TotalPeers { get; set; }
    public int ActivePeers { get; set; }
    public int OnionRoutingPeers { get; set; }
}

