using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Linq;
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

    public InMemoryDhtClient(ILogger<InMemoryDhtClient> logger, MeshOptions options)
    {
        this.logger = logger;
        selfId = RandomNodeId();
        routing = new KademliaRoutingTable(selfId);
        maxReplicas = 20; // align with k
    }

    /// <summary>
    /// Gets the count of nodes in the routing table (for metrics).
    /// </summary>
    public int GetNodeCount() => routing.Count;

    public Task PutAsync(byte[] key, byte[] value, int ttlSeconds, CancellationToken ct = default)
    {
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
    public (int totalKeys, int contentHintKeys) GetStoreStats()
    {
        var keys = store.Keys.ToList();
        var total = keys.Count;
        var content = keys.Count(k => k.StartsWith("mesh:content-peers:", StringComparison.Ordinal));
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

