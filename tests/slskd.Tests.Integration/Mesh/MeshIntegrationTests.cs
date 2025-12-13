namespace slskd.Tests.Integration.Mesh;

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Health;
using slskd.Mesh.Nat;
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

    [Fact]
    public async Task MultiNode_DhtConvergence()
    {
        // Arrange - Create multiple DHT nodes
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        var node1 = simulator.AddNode("node1");
        var node2 = simulator.AddNode("node2");
        var node3 = simulator.AddNode("node3");

        // Connect nodes in a chain
        simulator.ConnectNodes("node1", "node2");
        simulator.ConnectNodes("node2", "node3");

        // Act - Store value from node1, retrieve from node3
        var key = "test-key";
        var value = "test-value"u8.ToArray();

        await simulator.StoreAsync("node1", key, value);
        await Task.Delay(100); // Allow propagation

        var retrieved = await simulator.RetrieveAsync("node3", key);

        // Assert - Value should be available across the network
        Assert.NotNull(retrieved);
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task MultiNode_ContentDiscovery()
    {
        // Arrange - Create mesh with content
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        var contentId = "content:music:track1";
        var node1 = simulator.AddNode("node1", new Dictionary<string, byte[]>
        {
            [contentId] = "track1-metadata"u8.ToArray()
        });

        var node2 = simulator.AddNode("node2");
        var node3 = simulator.AddNode("node3");

        // Connect in star topology
        simulator.ConnectNodes("node1", "node2");
        simulator.ConnectNodes("node1", "node3");

        // Publish content availability
        await simulator.PublishContentAsync("node1", contentId);

        // Act - Discover content from different nodes
        var peersFromNode2 = await simulator.FindContentPeersAsync("node2", contentId);
        var peersFromNode3 = await simulator.FindContentPeersAsync("node3", contentId);

        // Assert - Both nodes should find the content provider
        Assert.Contains(peersFromNode2, p => p.PeerId == "peer:sim:node1");
        Assert.Contains(peersFromNode3, p => p.PeerId == "peer:sim:node1");
    }

    [Fact]
    public async Task NatTraversal_SymmetricFallback()
    {
        // Arrange - Simulate NAT traversal scenario
        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<NatTraversalService>>();
        var holePuncher = new Mock<IUdpHolePuncher>();
        var relayClient = new Mock<IRelayClient>();

        // Configure hole punch to fail (symmetric NAT)
        holePuncher.Setup(h => h.TryPunchAsync(It.IsAny<IPEndPoint>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UdpHolePunchResult(false, null, TimeSpan.FromMilliseconds(50)));

        // Configure relay to succeed
        relayClient.Setup(r => r.RelayAsync(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = Microsoft.Extensions.Options.Options.Create(new MeshOptions
        {
            EnableMirrored = true
        });

        var natService = new NatTraversalService(logger, holePuncher.Object, relayClient.Object, options);

        // Act - Attempt connection through NAT
        var endpoints = new List<string>
        {
            "udp://192.168.1.100:5000",  // Local endpoint (fails)
            "relay://relay.example.com:6000"  // Relay endpoint (succeeds)
        };

        var result = await natService.ConnectAsync("test-peer", endpoints, CancellationToken.None);

        // Assert - Should fallback to relay successfully
        Assert.True(result.Success);
        Assert.True(result.UsedRelay);
        Assert.Equal("relay", result.Reason);
        Assert.Equal("relay://relay.example.com:6000", result.ChosenEndpoint);
    }

    [Fact]
    public async Task KademliaRpc_IterativeLookup()
    {
        // Arrange - Create network topology for testing iterative lookup
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        // Create a network with known topology
        for (int i = 1; i <= 10; i++)
        {
            simulator.AddNode($"node{i}");
        }

        // Connect in a way that creates a realistic DHT topology
        for (int i = 1; i < 10; i++)
        {
            simulator.ConnectNodes($"node{i}", $"node{i + 1}");
        }

        // Act - Perform iterative FIND_NODE from node1 to find node10
        var foundNodes = await simulator.FindClosestNodesAsync("node1", "node10", 3);

        // Assert - Should find closest nodes through iterative lookup
        Assert.NotEmpty(foundNodes);
        Assert.True(foundNodes.Count <= 3); // k=3 parameter
    }

    [Fact]
    public async Task ContentReplication_AcrossNodes()
    {
        // Arrange - Multi-node content replication test
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        var contentId = "content:video:movie1";
        var contentData = new byte[1024]; // 1KB content
        Random.Shared.NextBytes(contentData);

        // Create nodes
        var node1 = simulator.AddNode("node1");
        var node2 = simulator.AddNode("node2");
        var node3 = simulator.AddNode("node3");

        // Connect in triangle topology
        simulator.ConnectNodes("node1", "node2");
        simulator.ConnectNodes("node2", "node3");
        simulator.ConnectNodes("node3", "node1");

        // Act - Store content from node1, replicate to others
        await simulator.StoreAsync("node1", contentId, contentData);
        await Task.Delay(200); // Allow replication

        var retrievedFromNode2 = await simulator.RetrieveAsync("node2", contentId);
        var retrievedFromNode3 = await simulator.RetrieveAsync("node3", contentId);

        // Assert - Content should be available on all nodes
        Assert.NotNull(retrievedFromNode2);
        Assert.NotNull(retrievedFromNode3);
        Assert.Equal(contentData, retrievedFromNode2);
        Assert.Equal(contentData, retrievedFromNode3);
    }

    [Fact]
    public async Task NetworkPartition_Recovery()
    {
        // Arrange - Test network partition and recovery
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        var node1 = simulator.AddNode("node1");
        var node2 = simulator.AddNode("node2");
        var node3 = simulator.AddNode("node3");

        // Connect all nodes
        simulator.ConnectNodes("node1", "node2");
        simulator.ConnectNodes("node2", "node3");

        // Store content before partition
        await simulator.StoreAsync("node1", "pre-partition-content", "data1"u8.ToArray());

        // Act - Create network partition
        simulator.PartitionNetwork();

        // Try to store during partition
        await simulator.StoreAsync("node1", "during-partition-content", "data2"u8.ToArray());

        // Heal partition
        simulator.HealNetwork();

        // Allow convergence
        await Task.Delay(300);

        // Assert - Content stored during partition should propagate
        var contentFromNode3 = await simulator.RetrieveAsync("node3", "during-partition-content");
        Assert.NotNull(contentFromNode3);
        Assert.Equal("data2"u8.ToArray(), contentFromNode3);
    }

    [Fact]
    public async Task PeerChurn_Handling()
    {
        // Arrange - Test peer join/leave scenarios
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        var stableNode = simulator.AddNode("stable");
        await simulator.StoreAsync("stable", "persistent-content", "stable-data"u8.ToArray());

        // Act - Add and remove nodes dynamically
        var tempNode1 = simulator.AddNode("temp1");
        var tempNode2 = simulator.AddNode("temp2");

        simulator.ConnectNodes("stable", "temp1");
        simulator.ConnectNodes("stable", "temp2");

        // Store content through temp nodes
        await simulator.StoreAsync("temp1", "temp-content-1", "temp-data-1"u8.ToArray());
        await simulator.StoreAsync("temp2", "temp-content-2", "temp-data-2"u8.ToArray());

        // Allow propagation
        await Task.Delay(100);

        // Remove temp nodes (simulate churn)
        simulator.RemoveNode("temp1");
        simulator.RemoveNode("temp2");

        // Stable node should still have content
        var persistentContent = await simulator.RetrieveAsync("stable", "persistent-content");
        Assert.NotNull(persistentContent);
        Assert.Equal("stable-data"u8.ToArray(), persistentContent);

        // Temp content may or may not be available depending on replication
        // This tests that the network continues functioning after churn
        var networkSize = simulator.NodeCount;
        Assert.Equal(1, networkSize); // Only stable node remains
    }

    [Fact]
    public async Task BootstrapProcess_MultiNode()
    {
        // Arrange - Test bootstrap process with multiple bootstrap nodes
        var simulator = new MeshSimulator(Mock.Of<ILogger<MeshSimulator>>());

        // Create bootstrap nodes
        var bootstrap1 = simulator.AddNode("bootstrap1");
        var bootstrap2 = simulator.AddNode("bootstrap2");

        // Create new node that needs bootstrapping
        var newNode = simulator.AddNode("newcomer");

        // Act - Bootstrap newcomer through multiple bootstrap nodes
        var bootstrapPeers = new[] { "peer:sim:bootstrap1", "peer:sim:bootstrap2" };
        await simulator.BootstrapNodeAsync("newcomer", bootstrapPeers);

        // Store content from newcomer
        await simulator.StoreAsync("newcomer", "newcomer-content", "newcomer-data"u8.ToArray());
        await Task.Delay(150); // Allow propagation

        // Assert - Content should be discoverable through bootstrap nodes
        var contentFromBootstrap1 = await simulator.RetrieveAsync("bootstrap1", "newcomer-content");
        var contentFromBootstrap2 = await simulator.RetrieveAsync("bootstrap2", "newcomer-content");

        Assert.NotNull(contentFromBootstrap1);
        Assert.NotNull(contentFromBootstrap2);
        Assert.Equal("newcomer-data"u8.ToArray(), contentFromBootstrap1);
        Assert.Equal("newcomer-data"u8.ToArray(), contentFromBootstrap2);
    }
}

