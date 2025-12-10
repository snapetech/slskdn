namespace slskd.Tests.Integration.Mesh;

using System.Collections.Concurrent;

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
