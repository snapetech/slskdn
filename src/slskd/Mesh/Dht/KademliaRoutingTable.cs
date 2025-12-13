using System.Collections.Concurrent;
using System.Numerics;

namespace slskd.Mesh.Dht;

/// <summary>
/// In-memory k-bucket routing table for Kademlia-style lookups.
/// Provides functional DHT routing for single-node/dev scenarios and integration tests.
/// </summary>
public class KademliaRoutingTable
{
    private const int BucketSize = 20; // k
    private readonly ConcurrentDictionary<int, Bucket> buckets = new();
    private readonly byte[] selfId;

    public KademliaRoutingTable(byte[] selfId)
    {
        this.selfId = selfId;
    }

    /// <summary>
    /// Gets the total count of nodes across all buckets (for metrics).
    /// </summary>
    public int Count => buckets.Values.Sum(b => b.Nodes.Count);

    public void Touch(byte[] nodeId, string address)
    {
        if (nodeId.Length != selfId.Length) return;
        var bucketIndex = BucketIndex(nodeId);
        var bucket = buckets.GetOrAdd(bucketIndex, _ => new Bucket());
        bucket.Touch(nodeId, address);
    }

    public IReadOnlyList<KNode> GetClosest(byte[] target, int count)
    {
        var all = buckets.Values
            .SelectMany(b => b.Nodes)
            .OrderBy(n => XorDistance(target, n.NodeId))
            .Take(count)
            .ToList();
        return all;
    }

    private int BucketIndex(byte[] nodeId)
    {
        // Find first differing bit between selfId and nodeId
        for (int i = 0; i < selfId.Length; i++)
        {
            var diff = selfId[i] ^ nodeId[i];
            if (diff == 0) continue;
            return (selfId.Length - i - 1) * 8 + (int)Math.Log2(diff);
        }
        // Identical -> place in last bucket
        return selfId.Length * 8;
    }

    private static BigInteger XorDistance(byte[] a, byte[] b)
    {
        Span<byte> buf = stackalloc byte[a.Length];
        for (int i = 0; i < a.Length; i++) buf[i] = (byte)(a[i] ^ b[i]);
        // BigInteger expects little-endian; reverse to preserve ordering
        var arr = buf.ToArray();
        Array.Reverse(arr);
        return new BigInteger(arr, isUnsigned: true, isBigEndian: false);
    }

    private sealed class Bucket
    {
        private readonly LinkedList<KNode> list = new();
        private readonly object gate = new();

        public IReadOnlyList<KNode> Nodes
        {
            get
            {
                lock (gate) return list.ToList();
            }
        }

        public void Touch(byte[] nodeId, string address)
        {
            lock (gate)
            {
                var existing = list.FirstOrDefault(n => n.NodeId.SequenceEqual(nodeId));
                if (existing != null)
                {
                    list.Remove(existing);
                    list.AddFirst(existing with { LastSeen = DateTimeOffset.UtcNow, Address = address });
                    return;
                }

                var node = new KNode(nodeId, address, DateTimeOffset.UtcNow);
                list.AddFirst(node);
                if (list.Count > BucketSize)
                {
                    list.RemoveLast();
                }
            }
        }
    }
}

public record KNode(byte[] NodeId, string Address, DateTimeOffset LastSeen);
















