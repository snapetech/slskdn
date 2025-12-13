// <copyright file="DhtService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using slskd.VirtualSoulfind.ShadowIndex;
using System;
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

    public DhtService(
        ILogger<DhtService> logger,
        KademliaRoutingTable routingTable,
        IDhtClient dhtClient,
        KademliaRpcClient rpcClient)
    {
        _logger = logger;
        _routingTable = routingTable;
        _dhtClient = dhtClient;
        _rpcClient = rpcClient;
    }

    /// <summary>
    /// Store a key-value pair in the DHT.
    /// </summary>
    public async Task<bool> StoreAsync(byte[] key, byte[] value, int ttlSeconds = 3600, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[DHT] Storing key {KeyHex} with TTL {TTL}s", Convert.ToHexString(key), ttlSeconds);

        // Store locally first
        await _dhtClient.PutAsync(key, value, ttlSeconds, cancellationToken);

        // Then store on k closest nodes
        return await _rpcClient.StoreAsync(key, value, ttlSeconds, cancellationToken);
    }

    /// <summary>
    /// Find a value by key in the DHT.
    /// </summary>
    public async Task<DhtLookupResult> FindValueAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[DHT] Looking up key {KeyHex}", Convert.ToHexString(key));

        var result = await _rpcClient.FindValueAsync(key, cancellationToken);

        if (result.Found)
        {
            // Cache the found value locally for future lookups
            await _dhtClient.PutAsync(key, result.Value!, 3600, cancellationToken);
            _logger.LogDebug("[DHT] Found and cached value for key {KeyHex}", Convert.ToHexString(key));
        }
        else
        {
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
        _logger.LogDebug("[DHT] Finding nodes closest to {TargetIdHex}", Convert.ToHexString(targetId));
        return await _rpcClient.FindNodeAsync(targetId, cancellationToken);
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
