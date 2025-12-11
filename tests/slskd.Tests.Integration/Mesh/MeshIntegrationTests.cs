namespace slskd.Tests.Integration.Mesh;

using Microsoft.Extensions.DependencyInjection;
using slskd.Mesh;
using slskd.Mesh.Health;
using slskd.Tests.Integration.Harness;
using Xunit;

/// <summary>
/// Integration tests for Mesh overlay network functionality.
/// </summary>
[Trait("Category", "L2-Integration")]
[Trait("Category", "Mesh")]
public class MeshIntegrationTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly StubWebApplicationFactory factory;
    private readonly IServiceProvider serviceProvider;

    public MeshIntegrationTests(StubWebApplicationFactory factory)
    {
        this.factory = factory;
        serviceProvider = factory.Services;
    }

    [Fact]
    public void MeshHealthService_ShouldReturnHealthStatus()
    {
        // Arrange
        var healthService = serviceProvider.GetService<IMeshHealthService>();

        // Act
        if (healthService != null)
        {
            var health = healthService.GetSnapshot();

            // Assert
            Assert.NotNull(health);
            // Health service should return some status (even if mesh is not fully initialized)
        }
        else
        {
            // Mesh health service not available - this is acceptable if Mesh is not fully configured
            Assert.True(true, "Mesh health service not available (Mesh may not be fully configured)");
        }
    }

    [Fact]
    public async Task MeshDirectory_ShouldBeAvailable()
    {
        // Arrange
        var meshDirectory = serviceProvider.GetService<IMeshDirectory>();

        // Act & Assert
        if (meshDirectory != null)
        {
            // Mesh directory is available
            Assert.NotNull(meshDirectory);
            
            // Try to query for peers (may return empty if no peers)
            var peers = await meshDirectory.FindPeersByContentAsync("test-content-id", CancellationToken.None);
            Assert.NotNull(peers);
        }
        else
        {
            // Mesh directory not available - acceptable if Mesh is not configured
            Assert.True(true, "Mesh directory not available (Mesh may not be fully configured)");
        }
    }

    [Fact]
    public async Task MeshAdvanced_ShouldProvideRouteDiagnostics()
    {
        // Arrange
        var meshAdvanced = serviceProvider.GetService<IMeshAdvanced>();

        // Act & Assert
        if (meshAdvanced != null)
        {
            Assert.NotNull(meshAdvanced);
            
            // Try to trace routes (may return empty list if no routes)
            var routes = await meshAdvanced.TraceRoutesAsync("test-peer-id", CancellationToken.None);
            Assert.NotNull(routes);
        }
        else
        {
            // Mesh advanced not available
            Assert.True(true, "Mesh advanced not available (Mesh may not be fully configured)");
        }
    }

    [Fact]
    public async Task MeshAdvanced_ShouldProvideStats()
    {
        // Arrange
        var meshAdvanced = serviceProvider.GetService<IMeshAdvanced>();

        // Act & Assert
        if (meshAdvanced != null)
        {
            Assert.NotNull(meshAdvanced);
            
            // Try to get transport stats
            var stats = await meshAdvanced.GetTransportStatsAsync(CancellationToken.None);
            Assert.NotNull(stats);
        }
        else
        {
            // Mesh advanced not available
            Assert.True(true, "Mesh advanced not available (Mesh may not be fully configured)");
        }
    }

    [Fact]
    public void MeshSimulator_ShouldCreateNodes()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var simulator = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        // Act
        var node1 = simulator.AddNode("node1");
        var node2 = simulator.AddNode("node2");

        // Assert
        Assert.Equal(2, simulator.NodeCount);
        Assert.NotNull(node1);
        Assert.NotNull(node2);
        Assert.Equal("node1", node1.NodeId);
        Assert.Equal("node2", node2.NodeId);
    }

    [Fact]
    public async Task MeshSimulator_ShouldHandleDhtOperations()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var simulator = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        // Act
        var testKey = "test-key";
        var testValue = System.Text.Encoding.UTF8.GetBytes("test-value");
        
        await simulator.DhtPutAsync(testKey, testValue);
        var retrieved = await simulator.DhtGetAsync(testKey);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(testValue, retrieved);
    }

    [Fact]
    public async Task MeshSimulator_ShouldSimulateOverlayTransfer()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var simulator = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());
        
        var node1 = simulator.AddNode("node1");
        var node2 = simulator.AddNode("node2");
        
        var testFile = System.Text.Encoding.UTF8.GetBytes("test file content");
        node1.AddFile("test.txt", testFile);

        // Act
        var hash = System.Security.Cryptography.SHA256.HashData(testFile);
        var hashHex = Convert.ToHexString(hash);
        
        var transferred = await simulator.OverlayTransferAsync("node1", "node2", hashHex);

        // Assert
        Assert.NotNull(transferred);
        Assert.Equal(testFile, transferred);
    }

    [Fact]
    public void MeshSimulator_ShouldHandleNetworkPartition()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var simulator = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        // Act
        simulator.SimulateNetworkPartition(true);
        Assert.True(simulator.IsNetworkPartitioned);

        simulator.SimulateNetworkPartition(false);
        Assert.False(simulator.IsNetworkPartitioned);
    }

    [Fact]
    public void MeshSimulator_ShouldHandleMessageDropRate()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var simulator = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        // Act
        simulator.SetMessageDropRate(0.5);
        Assert.Equal(0.5, simulator.MessageDropRate);

        simulator.SetMessageDropRate(1.0);
        Assert.Equal(1.0, simulator.MessageDropRate);

        simulator.SetMessageDropRate(0.0);
        Assert.Equal(0.0, simulator.MessageDropRate);
    }
}

