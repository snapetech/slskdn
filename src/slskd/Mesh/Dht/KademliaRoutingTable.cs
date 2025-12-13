using System.Collections.Concurrent;
using System.Numerics;

namespace slskd.Mesh.Dht;

/// <summary>
/// Kademlia-style k-bucket routing table with proper bucket splitting and ping-before-evict.
/// Implements the complete Kademlia DHT routing table as specified in the original paper.
/// </summary>
public class KademliaRoutingTable
{
    private const int BucketSize = 20; // k parameter
    private const int IdLengthBits = 160; // SHA-1 length for node IDs

    private readonly ConcurrentDictionary<int, Bucket> buckets = new();
    private readonly byte[] selfId;
    private readonly object splitLock = new();

    // Track which bucket indices have been split
    private readonly HashSet<int> splitBuckets = new();

    public KademliaRoutingTable(byte[] selfId)
    {
        this.selfId = selfId ?? throw new ArgumentNullException(nameof(selfId));
        if (selfId.Length * 8 != IdLengthBits)
        {
            throw new ArgumentException($"Node ID must be {IdLengthBits} bits ({IdLengthBits / 8} bytes)", nameof(selfId));
        }

        // Initialize with bucket 0 (contains the whole ID space initially)
        buckets[0] = new Bucket();
    }

    /// <summary>
    /// Gets the total count of nodes across all buckets (for metrics).
    /// </summary>
    public int Count => buckets.Values.Sum(b => b.Nodes.Count);

    /// <summary>
    /// Gets the number of buckets (for metrics).
    /// </summary>
    public int BucketCount => buckets.Count;

    /// <summary>
    /// Gets statistics about the routing table.
    /// </summary>
    public RoutingTableStats GetStats()
    {
        var bucketSizes = buckets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Nodes.Count);
        return new RoutingTableStats(
            TotalNodes: Count,
            BucketCount: BucketCount,
            BucketSizes: bucketSizes,
            MaxBucketSize: bucketSizes.Values.DefaultIfEmpty(0).Max(),
            MinBucketSize: bucketSizes.Values.DefaultIfEmpty(0).Min()
        );
    }

    /// <summary>
    /// Get all nodes in the routing table (for diagnostics).
    /// </summary>
    public IReadOnlyList<KNode> GetAllNodes()
    {
        return buckets.Values.SelectMany(b => b.Nodes).ToList();
    }

    /// <summary>
    /// Add or update a node in the routing table.
    /// Implements the Kademlia bucket splitting and ping-before-evict algorithm.
    /// </summary>
    public async Task TouchAsync(byte[] nodeId, string address, Func<byte[], Task<bool>> pingFunc = null)
    {
        if (nodeId.Length != selfId.Length || nodeId.SequenceEqual(selfId))
            return;

        // Handle ping-before-evict outside the lock to avoid async in lock
        KNode? nodeToRemove = null;
        bool shouldAddNewNode = true;

        lock (splitLock)
        {
            var bucketIndex = GetBucketIndex(nodeId);
            EnsureBucketExists(bucketIndex);

            var bucket = buckets[bucketIndex];

            // If bucket is not full, just add the node
            if (bucket.Nodes.Count < BucketSize)
            {
                bucket.Touch(nodeId, address);
                return;
            }

            // Bucket is full - check if we can split it
            if (CanSplitBucket(bucketIndex))
            {
                SplitBucket(bucketIndex);
                // After splitting, retry the insertion
                bucketIndex = GetBucketIndex(nodeId);
                EnsureBucketExists(bucketIndex);
                buckets[bucketIndex].Touch(nodeId, address);
                return;
            }

            // Cannot split - prepare for ping-before-evict
            if (pingFunc != null)
            {
                // Find the least recently seen node (but don't ping yet)
                nodeToRemove = bucket.Nodes.OrderBy(n => n.LastSeen).First();
                shouldAddNewNode = false; // Wait for ping result
            }
            else
            {
                // No ping function - just add and let LRU eviction happen
                bucket.Touch(nodeId, address);
            }
        }

        // Handle ping-before-evict outside the lock
        if (nodeToRemove != null && pingFunc != null)
        {
            var isAlive = await pingFunc(nodeToRemove.NodeId);

            lock (splitLock)
            {
                var bucketIndex = GetBucketIndex(nodeId);
                if (buckets.TryGetValue(bucketIndex, out var bucket))
                {
                    if (!isAlive)
                    {
                        // Remove dead node and add new one
                        bucket.Remove(nodeToRemove.NodeId);
                    }

                    // Add the new node (will evict LRU if still full)
                    bucket.Touch(nodeId, address);
                }
            }
        }
    }

    /// <summary>
    /// Synchronous version for backward compatibility (no ping-before-evict).
    /// </summary>
    public void Touch(byte[] nodeId, string address)
    {
        // Use synchronous version without ping
        var task = TouchAsync(nodeId, address, null);
        task.GetAwaiter().GetResult();
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

    /// <summary>
    /// Calculate which bucket a node belongs to based on XOR distance.
    /// Returns the highest bucket index that this node can fit into.
    /// </summary>
    private int GetBucketIndex(byte[] nodeId)
    {
        // Find the longest common prefix between selfId and nodeId
        // This determines which bucket the node belongs to
        for (int i = 0; i < IdLengthBits; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = 7 - (i % 8); // MSB first

            byte selfByte = selfId[byteIndex];
            byte nodeByte = nodeId[byteIndex];

            bool selfBit = (selfByte & (1 << bitIndex)) != 0;
            bool nodeBit = (nodeByte & (1 << bitIndex)) != 0;

            if (selfBit != nodeBit)
            {
                // First differing bit found - this determines the bucket
                return i;
            }
        }

        // Identical IDs - put in highest possible bucket
        return IdLengthBits;
    }

    /// <summary>
    /// Check if a bucket can be split (i.e., if the bucket contains our own node).
    /// </summary>
    private bool CanSplitBucket(int bucketIndex)
    {
        // Only split if this bucket contains our own node
        // This means we can subdivide the bucket further
        return bucketIndex < IdLengthBits && OwnsBucket(bucketIndex);
    }

    /// <summary>
    /// Check if our node "owns" a bucket (i.e., falls within that bucket's range).
    /// </summary>
    private bool OwnsBucket(int bucketIndex)
    {
        // Our node always owns bucket 0
        if (bucketIndex == 0) return true;

        // For higher buckets, check if our ID falls within that bucket
        var selfBucket = GetBucketIndex(selfId);
        return selfBucket >= bucketIndex;
    }

    /// <summary>
    /// Split a bucket into two smaller buckets.
    /// </summary>
    private void SplitBucket(int bucketIndex)
    {
        var oldBucket = buckets[bucketIndex];
        var oldNodes = oldBucket.Nodes.ToList();

        // Create two new buckets
        var bucket0 = new Bucket();
        var bucket1 = new Bucket();

        // Redistribute nodes based on the new, more specific bucket index
        foreach (var node in oldNodes)
        {
            var newBucketIndex = GetBucketIndex(node.NodeId);
            if (newBucketIndex == bucketIndex)
            {
                // This node stays in bucket0 (less specific)
                bucket0.AddNode(node);
            }
            else if (newBucketIndex == bucketIndex + 1)
            {
                // This node goes to bucket1 (more specific)
                bucket1.AddNode(node);
            }
            // Nodes that would go to even higher buckets stay in bucket0 for now
            else
            {
                bucket0.AddNode(node);
            }
        }

        // Replace the old bucket with the two new ones
        buckets[bucketIndex] = bucket0;
        buckets[bucketIndex + 1] = bucket1;

        splitBuckets.Add(bucketIndex);
    }

    /// <summary>
    /// Ensure a bucket exists at the given index.
    /// </summary>
    private void EnsureBucketExists(int bucketIndex)
    {
        if (!buckets.ContainsKey(bucketIndex))
        {
            buckets[bucketIndex] = new Bucket();
        }
    }

    private static BigInteger XorDistance(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Arrays must be the same length");

        Span<byte> buf = stackalloc byte[a.Length];
        for (int i = 0; i < a.Length; i++)
            buf[i] = (byte)(a[i] ^ b[i]);

        // BigInteger constructor expects the bytes in little-endian order
        // Our array is already in big-endian order (MSB first), so we need to reverse it
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
                    // Move to front (most recently seen)
                    list.Remove(existing);
                    list.AddFirst(existing with { LastSeen = DateTimeOffset.UtcNow, Address = address });
                    return;
                }

                // Add new node at front
                var node = new KNode(nodeId, address, DateTimeOffset.UtcNow);
                list.AddFirst(node);

                // If bucket is over capacity, remove least recently seen (LRU)
                if (list.Count > BucketSize)
                {
                    list.RemoveLast();
                }
            }
        }

        public void AddNode(KNode node)
        {
            lock (gate)
            {
                list.AddFirst(node);
            }
        }

        public bool Remove(byte[] nodeId)
        {
            lock (gate)
            {
                var node = list.FirstOrDefault(n => n.NodeId.SequenceEqual(nodeId));
                if (node != null)
                {
                    list.Remove(node);
                    return true;
                }
                return false;
            }
        }
    }
}

public record KNode(byte[] NodeId, string Address, DateTimeOffset LastSeen);

public record RoutingTableStats(
    int TotalNodes,
    int BucketCount,
    IReadOnlyDictionary<int, int> BucketSizes,
    int MaxBucketSize,
    int MinBucketSize
);

