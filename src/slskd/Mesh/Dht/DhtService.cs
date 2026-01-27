// <copyright file="DhtService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh;
using slskd.Mesh.ServiceFabric;
using slskd.Telemetry;
using slskd.VirtualSoulfind.ShadowIndex;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.Dht;

/// <summary>
/// High-level DHT service coordinating routing table, storage, and RPC operations.
/// Provides the main interface for DHT operations in the mesh network.
/// </summary>
public class DhtService
{
    private readonly ILogger<DhtService> _logger;
    private readonly KademliaRoutingTable _routingTable;
    private readonly IDhtClient _dhtClient;
    private readonly KademliaRpcClient _rpcClient;
    private readonly IMeshMessageSigner _messageSigner;

    public DhtService(
        ILogger<DhtService> logger,
        KademliaRoutingTable routingTable,
        IDhtClient dhtClient,
        KademliaRpcClient rpcClient,
        IMeshMessageSigner messageSigner)
    {
        _logger = logger;
        _routingTable = routingTable;
        _dhtClient = dhtClient;
        _rpcClient = rpcClient;
        _messageSigner = messageSigner;
    }

    /// <summary>
    /// Store a key-value pair in the DHT with signature verification.
    /// </summary>
    public async Task<bool> StoreAsync(byte[] key, byte[] value, int ttlSeconds = 3600, CancellationToken cancellationToken = default)
    {
        using var activity = MeshActivitySource.Source.StartActivity("mesh.dht.store");
        activity?.SetTag("mesh.dht.key", Convert.ToHexString(key));
        activity?.SetTag("mesh.dht.value_size", value.Length);
        activity?.SetTag("mesh.dht.ttl_seconds", ttlSeconds);

        _logger.LogDebug("[DHT] Storing signed key {KeyHex} with TTL {TTL}s", Convert.ToHexString(key), ttlSeconds);

        // Create signed message for the store operation
        var signedMessage = DhtStoreMessage.CreateSigned(key, value, _routingTable.GetSelfId(), ttlSeconds, _messageSigner);

        // Store locally first
        await _dhtClient.PutAsync(key, value, ttlSeconds, cancellationToken);

        // Then store on k closest nodes with signature verification
        var result = await _rpcClient.StoreAsync(signedMessage, cancellationToken);
        activity?.SetTag("mesh.dht.store.success", result);
        return result;
    }

    /// <summary>
    /// Find a value by key in the DHT.
    /// </summary>
    public async Task<DhtLookupResult> FindValueAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        using var activity = MeshActivitySource.Source.StartActivity("mesh.dht.find_value");
        activity?.SetTag("mesh.dht.key", Convert.ToHexString(key));

        _logger.LogDebug("[DHT] Looking up key {KeyHex}", Convert.ToHexString(key));

        var result = await _rpcClient.FindValueAsync(key, cancellationToken);

        if (result.Found)
        {
            activity?.SetTag("mesh.dht.find_value.found", true);
            activity?.SetTag("mesh.dht.find_value.value_size", result.Value?.Length ?? 0);
            // Cache the found value locally for future lookups
            await _dhtClient.PutAsync(key, result.Value!, 3600, cancellationToken);
            _logger.LogDebug("[DHT] Found and cached value for key {KeyHex}", Convert.ToHexString(key));
        }
        else
        {
            activity?.SetTag("mesh.dht.find_value.found", false);
            activity?.SetTag("mesh.dht.find_value.closest_nodes", result.ClosestNodes.Count);
            _logger.LogDebug("[DHT] Value not found for key {KeyHex}, returned {Count} closest nodes",
                Convert.ToHexString(key), result.ClosestNodes.Count);
        }

        return new DhtLookupResult
        {
            Found = result.Found,
            Value = result.Value,
            ClosestNodes = result.ClosestNodes
        };
    }

    /// <summary>
    /// Find the k closest nodes to a target ID.
    /// </summary>
    public async Task<IReadOnlyList<KNode>> FindNodeAsync(byte[] targetId, CancellationToken cancellationToken = default)
    {
        using var activity = MeshActivitySource.Source.StartActivity("mesh.dht.find_node");
        activity?.SetTag("mesh.dht.target_id", Convert.ToHexString(targetId));

        _logger.LogDebug("[DHT] Finding nodes closest to {TargetIdHex}", Convert.ToHexString(targetId));
        var nodes = await _rpcClient.FindNodeAsync(targetId, cancellationToken);
        activity?.SetTag("mesh.dht.find_node.nodes_found", nodes.Count);
        return nodes;
    }

    /// <summary>
    /// Add a known peer to the routing table.
    /// </summary>
    public async Task AddNodeAsync(byte[] nodeId, string address, CancellationToken cancellationToken = default)
    {
        await _routingTable.TouchAsync(nodeId, address);
        _logger.LogDebug("[DHT] Added node {NodeIdHex} at {Address}", Convert.ToHexString(nodeId), address);
    }

    /// <summary>
    /// Get routing table statistics.
    /// </summary>
    public RoutingTableStats GetRoutingTableStats() => _routingTable.GetStats();

    /// <summary>
    /// Get DHT storage statistics.
    /// </summary>
    public (int totalKeys, int contentHintKeys) GetStorageStats()
    {
        if (_dhtClient is InMemoryDhtClient inMemoryClient)
        {
            return inMemoryClient.GetStoreStats();
        }
        return (0, 0);
    }
}

/// <summary>
/// Result of a DHT lookup operation.
/// </summary>
public record DhtLookupResult
{
    public bool Found { get; init; }
    public byte[]? Value { get; init; }
    public IReadOnlyList<KNode> ClosestNodes { get; init; } = Array.Empty<KNode>();
}

