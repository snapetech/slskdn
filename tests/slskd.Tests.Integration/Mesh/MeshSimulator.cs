namespace slskd.Tests.Integration.Mesh;

using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// Mesh simulator for in-process DHT/overlay testing.
/// </summary>
public class MeshSimulator
{
    private readonly ILogger<MeshSimulator> logger;
    private readonly ConcurrentDictionary<string, SimulatedNode> nodes = new();
    private readonly ConcurrentDictionary<string, byte[]> dht = new();
    private bool networkPartitioned;
    private double messageDropRate;

    public MeshSimulator(ILogger<MeshSimulator> logger)
    {
        this.logger = logger;
    }

    public int NodeCount => nodes.Count;
    public bool IsNetworkPartitioned => networkPartitioned;
    public double MessageDropRate => messageDropRate;

    /// <summary>
    /// Add simulated node to mesh.
    /// </summary>
    public SimulatedNode AddNode(string nodeId, Dictionary<string, byte[]>? library = null)
    {
        logger.LogInformation("[MESH-SIM] Adding node {NodeId}", nodeId);

        var node = new SimulatedNode
        {
            NodeId = nodeId,
            PeerId = $"peer:sim:{nodeId}",
            Library = library ?? new Dictionary<string, byte[]>()
        };

        nodes[nodeId] = node;

        return node;
    }

    /// <summary>
    /// Remove node from mesh.
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        logger.LogInformation("[MESH-SIM] Removing node {NodeId}", nodeId);
        nodes.TryRemove(nodeId, out _);
    }

    /// <summary>
    /// Connect two nodes in the simulated mesh.
    /// </summary>
    public void ConnectNodes(string nodeId1, string nodeId2)
    {
        if (!nodes.ContainsKey(nodeId1))
            throw new ArgumentException($"Node {nodeId1} not found");
        if (!nodes.ContainsKey(nodeId2))
            throw new ArgumentException($"Node {nodeId2} not found");

        logger.LogDebug("[MESH-SIM] Connected {Node1} <-> {Node2}", nodeId1, nodeId2);
        // In this simple simulator, connection is implicit through the nodes dictionary
    }

    /// <summary>
    /// Simulate DHT put operation.
    /// </summary>
    public async Task DhtPutAsync(string key, byte[] value, CancellationToken ct = default)
    {
        if (ShouldDropMessage())
        {
            logger.LogDebug("[MESH-SIM] DHT PUT dropped (message drop simulation): {Key}", key);
            return;
        }

        logger.LogDebug("[MESH-SIM] DHT PUT: {Key} ({Size} bytes)", key, value.Length);
        
        await Task.Delay(SimulateNetworkLatency(), ct);
        dht[key] = value;
    }

    /// <summary>
    /// Simulate DHT get operation.
    /// </summary>
    public async Task<byte[]?> DhtGetAsync(string key, CancellationToken ct = default)
    {
        if (ShouldDropMessage())
        {
            logger.LogDebug("[MESH-SIM] DHT GET dropped (message drop simulation): {Key}", key);
            return null;
        }

        logger.LogDebug("[MESH-SIM] DHT GET: {Key}", key);
        
        await Task.Delay(SimulateNetworkLatency(), ct);
        dht.TryGetValue(key, out var value);
        
        return value;
    }

    /// <summary>
    /// Simulate overlay transfer.
    /// </summary>
    public async Task<byte[]?> OverlayTransferAsync(string fromNodeId, string toNodeId, string fileHash, CancellationToken ct = default)
    {
        if (networkPartitioned)
        {
            logger.LogWarning("[MESH-SIM] Overlay transfer blocked (network partition): {From} → {To}", fromNodeId, toNodeId);
            return null;
        }

        if (ShouldDropMessage())
        {
            logger.LogDebug("[MESH-SIM] Overlay transfer dropped: {From} → {To}", fromNodeId, toNodeId);
            return null;
        }

        logger.LogInformation("[MESH-SIM] Overlay transfer: {From} → {To} ({Hash})", fromNodeId, toNodeId, fileHash);

        if (!nodes.TryGetValue(fromNodeId, out var fromNode))
        {
            return null;
        }

        // Find file by hash
        var file = fromNode.Library.FirstOrDefault(kvp => ComputeHash(kvp.Value) == fileHash);
        if (file.Value == null)
        {
            return null;
        }

        // Simulate transfer time based on file size
        var transferTimeMs = (int)(file.Value.Length / 1024.0 * 10); // 10ms per KB
        await Task.Delay(transferTimeMs, ct);

        return file.Value;
    }

    /// <summary>
    /// Simulate network partition.
    /// </summary>
    public void SimulateNetworkPartition(bool enabled)
    {
        logger.LogWarning("[MESH-SIM] Network partition: {Status}", enabled ? "ENABLED" : "DISABLED");
        networkPartitioned = enabled;
    }

    /// <summary>
    /// Set message drop rate (0.0 to 1.0).
    /// </summary>
    public void SetMessageDropRate(double rate)
    {
        logger.LogInformation("[MESH-SIM] Message drop rate: {Rate:P}", rate);
        messageDropRate = Math.Clamp(rate, 0.0, 1.0);
    }

    /// <summary>
    /// Get simulated node by ID.
    /// </summary>
    public SimulatedNode? GetNode(string nodeId)
    {
        nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>
    /// Get all nodes.
    /// </summary>
    public IEnumerable<SimulatedNode> GetAllNodes()
    {
        return nodes.Values;
    }

    /// <summary>
    /// Clear all DHT data.
    /// </summary>
    public void ClearDht()
    {
        logger.LogInformation("[MESH-SIM] Clearing DHT");
        dht.Clear();
    }

    private bool ShouldDropMessage()
    {
        return Random.Shared.NextDouble() < messageDropRate;
    }

    private int SimulateNetworkLatency()
    {
        // Random latency 10-100ms
        return Random.Shared.Next(10, 100);
    }

    /// <summary>
    /// Store content in the simulated DHT.
    /// </summary>
    public async Task StoreAsync(string nodeId, string key, byte[] value)
    {
        if (!nodes.ContainsKey(nodeId))
            throw new ArgumentException($"Node {nodeId} not found");

        // Origin node always retains its own data
        nodes[nodeId].Library[key] = value;

        if (networkPartitioned)
        {
            await Task.CompletedTask;
            return;
        }

        // Simulate DHT replication to k closest nodes
        var keyHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        var closestNodes = FindClosestNodes(nodeId, keyHash, 3);

        foreach (var closeNode in closestNodes)
        {
            if (Random.Shared.NextDouble() > messageDropRate)
            {
                nodes[closeNode.NodeId].Library[key] = value;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Retrieve content from the simulated DHT.
    /// </summary>
    public async Task<byte[]?> RetrieveAsync(string nodeId, string key)
    {
        if (!nodes.ContainsKey(nodeId))
            throw new ArgumentException($"Node {nodeId} not found");

        if (networkPartitioned)
        {
            // During partition, only check local node
            return nodes[nodeId].Library.TryGetValue(key, out var value) ? value : null;
        }

        // Check local node first
        if (nodes[nodeId].Library.TryGetValue(key, out var localValue))
        {
            return localValue;
        }

        // Simulate DHT lookup to closest nodes
        var keyHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        var closestNodes = FindClosestNodes(nodeId, keyHash, 3);

        foreach (var closeNode in closestNodes)
        {
            if (Random.Shared.NextDouble() > messageDropRate &&
                nodes[closeNode.NodeId].Library.TryGetValue(key, out var remoteValue))
            {
                return remoteValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Publish content availability for discovery.
    /// </summary>
    public async Task PublishContentAsync(string nodeId, string contentId)
    {
        if (!nodes.ContainsKey(nodeId))
            throw new ArgumentException($"Node {nodeId} not found");

        var contentPeersKey = $"mesh:content-peers:{contentId}";
        var peerHint = new
        {
            PeerId = nodes[nodeId].PeerId,
            Endpoints = new[] { $"udp://simulated:{nodeId}:5000" },
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Store peer hints in DHT
        await StoreAsync(nodeId, contentPeersKey, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(peerHint));

        // Also maintain reverse index
        var peerContentKey = $"mesh:peer-content:{nodes[nodeId].PeerId}";
        var existingContent = await RetrieveAsync(nodeId, peerContentKey);
        var contentList = existingContent != null
            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingContent).Append(contentId).Distinct().ToList()
            : new List<string> { contentId };

        await StoreAsync(nodeId, peerContentKey, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(contentList));
    }

    /// <summary>
    /// Find peers that have specific content.
    /// </summary>
    public async Task<List<SimulatedNode>> FindContentPeersAsync(string nodeId, string contentId)
    {
        var contentPeersKey = $"mesh:content-peers:{contentId}";
        var hintsData = await RetrieveAsync(nodeId, contentPeersKey);

        if (hintsData == null)
            return new List<SimulatedNode>();

        // Simplified: return all online nodes (in real impl, would parse peer hints)
        return nodes.Values.Where(n => n.IsOnline).ToList();
    }

    /// <summary>
    /// Find closest nodes in the simulated network.
    /// </summary>
    private List<SimulatedNode> FindClosestNodes(string sourceNodeId, string targetHash, int count)
    {
        // Simple distance calculation based on node IDs
        return nodes.Values
            .Where(n => n.NodeId != sourceNodeId && n.IsOnline)
            .OrderBy(n => ComputeDistance(n.NodeId, targetHash))
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Find closest nodes for iterative lookup.
    /// </summary>
    public async Task<List<SimulatedNode>> FindClosestNodesAsync(string sourceNodeId, string targetNodeId, int count)
    {
        var targetHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(targetNodeId));
        var closest = FindClosestNodes(sourceNodeId, targetHash, count);

        await Task.CompletedTask; // Simulate async operation
        return closest;
    }

    /// <summary>
    /// Bootstrap a node using bootstrap peers.
    /// </summary>
    public async Task BootstrapNodeAsync(string nodeId, string[] bootstrapPeerIds)
    {
        if (!nodes.ContainsKey(nodeId))
            throw new ArgumentException($"Node {nodeId} not found");

        foreach (var bootstrapPeerId in bootstrapPeerIds)
        {
            // Find bootstrap node
            var bootstrapNode = nodes.Values.FirstOrDefault(n => n.PeerId == bootstrapPeerId);
            if (bootstrapNode != null)
            {
                // Connect to bootstrap node
                ConnectNodes(nodeId, bootstrapNode.NodeId);

                // Copy some known content from bootstrap node
                foreach (var kvp in bootstrapNode.Library.Where(kvp => kvp.Key.StartsWith("mesh:")))
                {
                    if (Random.Shared.NextDouble() > messageDropRate)
                    {
                        nodes[nodeId].Library[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Simulate network partition.
    /// </summary>
    public void PartitionNetwork()
    {
        networkPartitioned = true;
        logger.LogInformation("[MESH-SIM] Network partitioned");
    }

    /// <summary>
    /// Heal network partition.
    /// </summary>
    public void HealNetwork()
    {
        networkPartitioned = false;
        logger.LogInformation("[MESH-SIM] Network partition healed");
    }

    private int ComputeDistance(string nodeId, string targetHash)
    {
        // Simple XOR distance simulation
        var nodeHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(nodeId));
        return Math.Abs(nodeHash.GetHashCode() - targetHash.GetHashCode());
    }

    private string ComputeHash(byte[] data)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(data);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Simulated mesh node.
/// </summary>
public class SimulatedNode
{
    public string NodeId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public Dictionary<string, byte[]> Library { get; set; } = new();
    public bool IsOnline { get; set; } = true;
    public int RequestCount { get; set; }
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Add file to node's library.
    /// </summary>
    public void AddFile(string filename, byte[] content)
    {
        Library[filename] = content;
    }

    /// <summary>
    /// Remove file from library.
    /// </summary>
    public void RemoveFile(string filename)
    {
        Library.Remove(filename);
    }

    /// <summary>
    /// Get file by hash.
    /// </summary>
    public byte[]? GetFileByHash(string hash)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        
        foreach (var (filename, content) in Library)
        {
            var fileHash = Convert.ToHexString(sha.ComputeHash(content));
            if (fileHash.Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }
        }

        return null;
    }
}
