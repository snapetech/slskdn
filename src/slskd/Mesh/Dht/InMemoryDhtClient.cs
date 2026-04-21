// <copyright file="InMemoryDhtClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Linq;
using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.ShadowIndex;

namespace slskd.Mesh.Dht;

/// <summary>
/// In-memory DHT client implementing IDhtClient with Kademlia-style routing table.
/// Provides working PUT/GET operations for single-process/testing scenarios and development.
/// For production multi-node DHT, this would be replaced with a distributed implementation.
/// </summary>
public class InMemoryDhtClient : IDhtClient
{
    private readonly ILogger<InMemoryDhtClient> logger;
    private readonly KademliaRoutingTable routing;
    private readonly ConcurrentDictionary<string, List<DhtValue>> store = new();
    private readonly byte[] selfId;
    private readonly int maxReplicas;
    private readonly MeshStatsCollector? statsCollector;

    public InMemoryDhtClient(ILogger<InMemoryDhtClient> logger, IOptions<MeshOptions> options, MeshStatsCollector? statsCollector = null)
    {
        logger.LogDebug("[InMemoryDhtClient] Constructor called");
        this.logger = logger;
        this.statsCollector = statsCollector;
        logger.LogDebug("[InMemoryDhtClient] Generating random node ID");
        selfId = RandomNodeId();
        logger.LogDebug("[InMemoryDhtClient] Creating KademliaRoutingTable");
        routing = new KademliaRoutingTable(selfId);
        logger.LogDebug("[InMemoryDhtClient] KademliaRoutingTable created");
        maxReplicas = 20; // align with k

        // RpcClient will be set later to avoid circular dependency
        RpcClient = null!;
        logger.LogDebug("[InMemoryDhtClient] Constructor completed");
    }

    /// <summary>
    /// Gets the count of nodes in the routing table (for metrics).
    /// </summary>
    public int GetNodeCount() => routing.Count;

    /// <summary>
    /// Gets the Kademlia RPC client for advanced DHT operations.
    /// </summary>
    public KademliaRpcClient RpcClient { get; private set; }

    /// <summary>
    /// Gets routing table statistics.
    /// </summary>
    public RoutingTableStats GetRoutingTableStats() => routing.GetStats();

    public Task PutAsync(byte[] key, byte[] value, int ttlSeconds, CancellationToken ct = default)
    {
        statsCollector?.RecordDhtOperation();
        statsCollector?.UpdateRoutingTableSize(routing.GetStats().TotalNodes);

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddSeconds(Math.Clamp(ttlSeconds, 60, 3600));
        var keyHex = ToHex(key);
        var list = store.GetOrAdd(keyHex, _ => new List<DhtValue>());

        lock (list)
        {
            // Replace if same value exists; otherwise add until max replicas
            var existing = list.FirstOrDefault(v => v.Data.SequenceEqual(value));
            if (existing != null)
            {
                existing.ExpiresAt = expires;
            }
            else
            {
                list.Add(new DhtValue(value, expires));
                if (list.Count > maxReplicas)
                {
                    list.Sort((a, b) => a.ExpiresAt.CompareTo(b.ExpiresAt));
                    if (list.Count > maxReplicas)
                        list.RemoveRange(0, list.Count - maxReplicas);
                }
            }
        }

        logger.LogDebug("[DHT] PUT key={Key} size={Size} ttl={TTL}s", keyHex, value.Length, ttlSeconds);
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(byte[] key, CancellationToken ct = default)
    {
        statsCollector?.RecordDhtOperation();
        var keyHex = ToHex(key);
        if (!store.TryGetValue(keyHex, out var list))
        {
            return Task.FromResult<byte[]?>(null);
        }

        lock (list)
        {
            var now = DateTimeOffset.UtcNow;
            list.RemoveAll(v => v.ExpiresAt <= now);
            var first = list.FirstOrDefault();
            return Task.FromResult(first?.Data);
        }
    }

    public Task<List<byte[]>> GetMultipleAsync(byte[] key, CancellationToken ct = default)
    {
        var keyHex = ToHex(key);
        if (!store.TryGetValue(keyHex, out var list))
        {
            return Task.FromResult(new List<byte[]>());
        }

        lock (list)
        {
            var now = DateTimeOffset.UtcNow;
            list.RemoveAll(v => v.ExpiresAt <= now);
            return Task.FromResult(list.Select(v => v.Data).ToList());
        }
    }

    /// <summary>
    /// Add a known peer to the routing table (best effort).
    /// </summary>
    public Task AddNodeAsync(byte[] nodeId, string address)
    {
        routing.Touch(nodeId, address);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronous version for backward compatibility.
    /// </summary>
    public void AddNode(byte[] nodeId, string address)
    {
        routing.Touch(nodeId, address);
    }

    /// <summary>
    /// Get closest nodes for a target ID.
    /// </summary>
    public IReadOnlyList<KNode> FindClosest(byte[] target, int k = 20) =>
        routing.GetClosest(target, k);

    /// <summary>
    /// Stats: stored keys and how many are content-peer hints.
    /// </summary>
    public (int TotalKeys, int ContentHintKeys) GetStoreStats()
    {
        var total = 0;
        var content = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var list in store.Values)
        {
            lock (list)
            {
                list.RemoveAll(v => v.ExpiresAt <= now);
                if (list.Count == 0)
                {
                    continue;
                }

                total++;

                if (ContainsContentPeerHints(list))
                {
                    content++;
                }
            }
        }

        return (total, content);
    }

    /// <summary>
    /// Expose values for FindValue-style queries.
    /// </summary>
    public List<byte[]> GetMultiple(byte[] key)
    {
        var keyHex = ToHex(key);
        if (!store.TryGetValue(keyHex, out var list))
        {
            return new List<byte[]>();
        }

        lock (list)
        {
            var now = DateTimeOffset.UtcNow;
            list.RemoveAll(v => v.ExpiresAt <= now);
            return list.Select(v => v.Data).ToList();
        }
    }

    private static string ToHex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();

    private static bool ContainsContentPeerHints(List<DhtValue> values)
    {
        foreach (var value in values)
        {
            try
            {
                var hints = MessagePackSerializer.Deserialize<ContentPeerHints>(value.Data);
                if (hints?.Peers?.Count > 0)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Most DHT values are not content peer hints.
            }
        }

        return false;
    }

    private static byte[] RandomNodeId()
    {
        var buf = new byte[20]; // 160-bit IDs (sha1-length)
        RandomNumberGenerator.Fill(buf);
        return buf;
    }

    private sealed class DhtValue
    {
        public DhtValue(byte[] data, DateTimeOffset expiresAt)
        {
            Data = data;
            ExpiresAt = expiresAt;
        }

        public byte[] Data { get; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
