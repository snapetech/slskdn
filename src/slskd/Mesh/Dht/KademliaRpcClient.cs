// <copyright file="KademliaRpcClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.VirtualSoulfind.ShadowIndex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.Dht;

/// <summary>
/// Client for performing Kademlia RPC operations (FIND_NODE, FIND_VALUE, PING).
/// Implements the iterative lookup algorithm with alpha=3 parallel requests.
/// </summary>
public class KademliaRpcClient
{
    private const int Alpha = 3; // Number of parallel requests
    private const int K = 20; // Bucket size
    private const int MaxIterations = 20; // Prevent infinite loops

    private readonly ILogger<KademliaRpcClient> _logger;
    private readonly IMeshServiceClient _meshClient;
    private readonly KademliaRoutingTable _routingTable;
    private readonly IDhtClient _dhtClient;

    public KademliaRpcClient(
        ILogger<KademliaRpcClient> logger,
        IMeshServiceClient meshClient,
        KademliaRoutingTable routingTable,
        IDhtClient dhtClient)
    {
        _logger = logger;
        _meshClient = meshClient;
        _routingTable = routingTable;
        _dhtClient = dhtClient;
    }

    /// <summary>
    /// Perform iterative FIND_NODE lookup for the given target ID.
    /// Returns up to k closest nodes to the target.
    /// </summary>
    public async Task<IReadOnlyList<KNode>> FindNodeAsync(
        byte[] targetId,
        CancellationToken cancellationToken = default)
    {
        if (targetId.Length != 20)
            throw new ArgumentException("Target ID must be 20 bytes", nameof(targetId));

        var visited = new HashSet<string>(); // Track contacted peers
        var candidates = new SortedSet<NodeDistance>(
            _routingTable.GetClosest(targetId, K).Select(n => new NodeDistance(n, targetId))
        );
        var closestFound = new SortedSet<NodeDistance>();

        for (int iteration = 0; iteration < MaxIterations && candidates.Any(); iteration++)
        {
            // Take alpha closest unvisited nodes
            var toContact = candidates
                .Where(c => !visited.Contains(c.Node.Address))
                .Take(Alpha)
                .ToList();

            if (!toContact.Any())
                break; // No more nodes to contact

            // Mark as visited
            foreach (var node in toContact)
            {
                visited.Add(node.Node.Address);
            }

            // Contact nodes in parallel
            var tasks = toContact.Select(node => QueryFindNodeAsync(node.Node, targetId, cancellationToken));
            var results = await Task.WhenAll(tasks);

            // Process results
            foreach (var result in results.Where(r => r != null))
            {
                foreach (var returnedNode in result!)
                {
                    var distance = new NodeDistance(returnedNode, targetId);
                    candidates.Add(distance);
                    closestFound.Add(distance);
                }
            }

            // Keep only the k closest candidates
            candidates = new SortedSet<NodeDistance>(candidates.Take(K));
        }

        _logger.LogDebug(
            "[Kademlia] FIND_NODE for {TargetIdHex} completed after {Iterations} iterations, found {Nodes} nodes",
            Convert.ToHexString(targetId), visited.Count, closestFound.Count);

        return closestFound.Select(n => n.Node).ToList();
    }

    /// <summary>
    /// Perform FIND_VALUE lookup - first tries local storage, then iterative node lookup.
    /// </summary>
    public async Task<FindValueResult> FindValueAsync(
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        // First try to find the value locally
        var localValues = await _dhtClient.GetMultipleAsync(key, cancellationToken);
        if (localValues.Any())
        {
            return new FindValueResult
            {
                Found = true,
                Value = localValues.First(),
                ClosestNodes = Array.Empty<KNode>()
            };
        }

        // Not found locally - perform node lookup
        var closestNodes = await FindNodeAsync(key, cancellationToken);

        return new FindValueResult
        {
            Found = false,
            Value = null,
            ClosestNodes = closestNodes
        };
    }

    /// <summary>
    /// Send PING to a specific node to check if it's alive.
    /// </summary>
    public async Task<bool> PingAsync(KNode node, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PingRequest
            {
                RequesterId = _routingTable.GetSelfId()
            };

            var call = new ServiceCall
            {
                ServiceName = "dht",
                Method = "Ping",
                Payload = JsonSerializer.SerializeToUtf8Bytes(request)
            };

            var reply = await _meshClient.CallAsync(node.Address, call, cancellationToken);

            if (reply.IsSuccess)
            {
                var response = JsonSerializer.Deserialize<PingResponse>(reply.Payload);
                _logger.LogDebug("[Kademlia] PING to {Address} successful", node.Address);
                return true;
            }
            else
            {
                _logger.LogDebug("[Kademlia] PING to {Address} failed: {Error}", node.Address, reply.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Kademlia] PING to {Address} threw exception", node.Address);
            return false;
        }
    }

    private async Task<KNode[]?> QueryFindNodeAsync(
        KNode node,
        byte[] targetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new FindNodeRequest
            {
                TargetId = targetId,
                RequesterId = _routingTable.GetSelfId(),
                Count = K
            };

            var call = new ServiceCall
            {
                ServiceName = "dht",
                Method = "FindNode",
                Payload = JsonSerializer.SerializeToUtf8Bytes(request)
            };

            var reply = await _meshClient.CallAsync(node.Address, call, cancellationToken);

            if (reply.IsSuccess)
            {
                var response = JsonSerializer.Deserialize<FindNodeResponse>(reply.Payload);
                if (response?.Nodes != null)
                {
                    // Convert DhtNodeInfo back to KNode
                    return response.Nodes.Select(n => new KNode(
                        n.NodeId,
                        n.Address,
                        DateTimeOffset.FromUnixTimeMilliseconds(n.LastSeen.ToUnixTimeMilliseconds())
                    )).ToArray();
                }
            }
            else
            {
                _logger.LogDebug(
                    "[Kademlia] FIND_NODE query to {Address} failed: {Error}",
                    node.Address, reply.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Kademlia] FIND_NODE query to {Address} threw exception", node.Address);
        }

        return null;
    }

    /// <summary>
    /// Get the local node's ID from the routing table.
    /// </summary>
    private byte[] GetSelfId() => _routingTable.GetSelfId();
}

/// <summary>
/// Result of a FIND_VALUE operation.
/// </summary>
public record FindValueResult
{
    public bool Found { get; init; }
    public byte[]? Value { get; init; }
    public IReadOnlyList<KNode> ClosestNodes { get; init; } = Array.Empty<KNode>();
}

/// <summary>
/// Node with XOR distance for sorting.
/// </summary>
internal class NodeDistance : IComparable<NodeDistance>
{
    public KNode Node { get; }
    public BigInteger Distance { get; }

    public NodeDistance(KNode node, byte[] targetId)
    {
        Node = node;
        Distance = XorDistance(targetId, node.NodeId);
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

    public int CompareTo(NodeDistance? other)
    {
        if (other == null) return 1;
        return Distance.CompareTo(other.Distance);
    }
}
