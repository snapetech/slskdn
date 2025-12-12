using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using System;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// Tests for expanded router stats with security metrics.
/// </summary>
public class RouterStatsTests
{
    [Fact]
    public void GetStats_ReturnsBasicMetrics()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MeshServiceRouter>>();
        var optionsMock = new Mock<Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new MeshServiceFabricOptions());
        var router = new MeshServiceRouter(logger, optionsMock.Object);

        // Act
        var stats = router.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.RegisteredServiceCount);
        Assert.Equal(0, stats.TrackedPeerCount);
        Assert.Equal(0, stats.OpenCircuitCount);
        Assert.NotNull(stats.CircuitBreakers);
        Assert.True(stats.WorkBudgetEnabled);
    }

    [Fact]
    public void GetStats_IncludesWorkBudgetMetrics()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MeshServiceRouter>>();
        var optionsMock = new Mock<Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new MeshServiceFabricOptions
        {
            MaxWorkUnitsPerCall = 10,
            MaxWorkUnitsPerPeerPerMinute = 50
        });
        var router = new MeshServiceRouter(logger, optionsMock.Object);

        // Act
        var stats = router.GetStats();

        // Assert
        Assert.NotNull(stats.WorkBudgetMetrics);
        Assert.Equal(0, stats.WorkBudgetMetrics.ActivePeersLastMinute);
        Assert.Equal(0, stats.WorkBudgetMetrics.TotalWorkUnitsConsumedLastMinute);
    }

    [Fact]
    public void GetStats_TracksCircuitBreakerState()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MeshServiceRouter>>();
        var optionsMock = new Mock<Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new MeshServiceFabricOptions());
        var router = new MeshServiceRouter(logger, optionsMock.Object);

        // Register a service that will fail
        var failingService = new FailingTestService();
        router.RegisterService(failingService);

        // Make 5 calls to open the circuit breaker
        for (int i = 0; i < 5; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "failing-test",
                Method = "Fail",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            _ = router.RouteAsync(call, "test-peer").Result;
        }

        // Act
        var stats = router.GetStats();

        // Assert
        Assert.Equal(1, stats.OpenCircuitCount);
        Assert.Single(stats.CircuitBreakers);
        Assert.True(stats.CircuitBreakers[0].IsOpen);
        Assert.Equal("failing-test", stats.CircuitBreakers[0].ServiceName);
        Assert.Equal(5, stats.CircuitBreakers[0].ConsecutiveFailures);
    }

    // Test helper service
    private class FailingTestService : IMeshService
    {
        public string ServiceName => "failing-test";

        public System.Threading.Tasks.Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            System.Threading.CancellationToken cancellationToken)
        {
            throw new Exception("Test failure");
        }

        public System.Threading.Tasks.Task HandleStreamAsync(
            MeshServiceStream stream,
            MeshServiceContext context,
            System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}

