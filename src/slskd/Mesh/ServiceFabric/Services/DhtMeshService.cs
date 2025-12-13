// <copyright file="DhtMeshService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.ServiceFabric;
using slskd.VirtualSoulfind.ShadowIndex;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Mesh service for Kademlia DHT operations.
/// Implements FIND_NODE and FIND_VALUE RPCs for decentralized peer discovery and content lookup.
/// </summary>
public class DhtMeshService : IMeshService
{
    private readonly ILogger<DhtMeshService> _logger;
    private readonly KademliaRoutingTable _routingTable;
    private readonly IDhtClient _dhtClient;

    public DhtMeshService(
        ILogger<DhtMeshService> logger,
        KademliaRoutingTable routingTable,
        IDhtClient dhtClient)
    {
        _logger = logger;
        _routingTable = routingTable;
        _dhtClient = dhtClient;
    }

    public string ServiceName => "dht";

    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming not implemented for DHT service");
    }

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "[DHT] Handling call: {Method} from {PeerId}",
                call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "FindNode" => await HandleFindNodeAsync(call, context, cancellationToken),
                "FindValue" => await HandleFindValueAsync(call, context, cancellationToken),
                "Store" => await HandleStoreAsync(call, context, cancellationToken),
                "Ping" => await HandlePingAsync(call, context, cancellationToken),
                _ => new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.MethodNotFound,
                    ErrorMessage = $"Unknown DHT method: {call.Method}",
                    Payload = Array.Empty<byte>()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DHT] Error handling call {Method}", call.Method);
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"Internal error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle FIND_NODE RPC: Return k closest nodes to target ID.
    /// </summary>
    private async Task<ServiceReply> HandleFindNodeAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<FindNodeRequest>(call.Payload);
            if (request?.TargetId == null || request.TargetId.Length != 20)
            {
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.InvalidPayload,
                    ErrorMessage = "Invalid FindNode request: target ID must be 20 bytes",
                    Payload = Array.Empty<byte>()
                };
            }

            // Get k closest nodes from routing table
            var closestNodes = _routingTable.GetClosest(request.TargetId, request.Count ?? 20);

            // Update routing table with the requesting peer
            await _routingTable.TouchAsync(request.RequesterId, context.RemotePeerId);

            var response = new FindNodeResponse
            {
                TargetId = request.TargetId,
                Nodes = closestNodes.Select(n => new DhtNodeInfo
                {
                    NodeId = n.NodeId,
                    Address = n.Address,
                    LastSeen = n.LastSeen
                }).ToArray()
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(response);

            _logger.LogDebug(
                "[DHT] FindNode for {TargetIdHex} returned {Count} nodes",
                Convert.ToHexString(request.TargetId), response.Nodes.Length);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = payload
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DHT] Error in FindNode");
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"FindNode error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle FIND_VALUE RPC: Return value if stored locally, otherwise closest nodes.
    /// </summary>
    private async Task<ServiceReply> HandleFindValueAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<FindValueRequest>(call.Payload);
            if (request?.Key == null)
            {
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.InvalidPayload,
                    ErrorMessage = "Invalid FindValue request: key required",
                    Payload = Array.Empty<byte>()
                };
            }

            // Try to get the value from local storage
            var value = await _dhtClient.GetAsync(request.Key, cancellationToken);
            if (value != null)
            {
                // Found the value locally
                var localResponse = new FindValueResponse
                {
                    Key = request.Key,
                    Value = value,
                    Found = true
                };

                var localPayload = JsonSerializer.SerializeToUtf8Bytes(localResponse);

                _logger.LogDebug(
                    "[DHT] FindValue found value for key {KeyHex} locally",
                    Convert.ToHexString(request.Key));

                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.OK,
                    Payload = localPayload
                };
            }

            // Value not found - return closest nodes instead
            var closestNodes = _routingTable.GetClosest(request.Key, request.Count ?? 20);

            // Update routing table with the requesting peer
            await _routingTable.TouchAsync(request.RequesterId, context.RemotePeerId);

            var response = new FindValueResponse
            {
                Key = request.Key,
                Found = false,
                ClosestNodes = closestNodes.Select(n => new DhtNodeInfo
                {
                    NodeId = n.NodeId,
                    Address = n.Address,
                    LastSeen = n.LastSeen
                }).ToArray()
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(response);

            _logger.LogDebug(
                "[DHT] FindValue for key {KeyHex} returned {Count} closest nodes",
                Convert.ToHexString(request.Key), response.ClosestNodes.Length);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = payload
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DHT] Error in FindValue");
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"FindValue error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle STORE RPC: Cache a key-value pair locally.
    /// </summary>
    private async Task<ServiceReply> HandleStoreAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<StoreRequest>(call.Payload);
            if (request?.Key == null || request.Value == null)
            {
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.InvalidPayload,
                    ErrorMessage = "Invalid Store request: key and value required",
                    Payload = Array.Empty<byte>()
                };
            }

            // Store the key-value pair with TTL
            var ttlSeconds = request.TtlSeconds ?? 3600; // Default 1 hour
            await _dhtClient.PutAsync(request.Key, request.Value, ttlSeconds, cancellationToken);

            // Update routing table with the storing peer
            await _routingTable.TouchAsync(request.RequesterId, context.RemotePeerId);

            var response = new StoreResponse
            {
                Key = request.Key,
                Stored = true,
                TtlSeconds = ttlSeconds
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(response);

            _logger.LogDebug(
                "[DHT] Stored value for key {KeyHex} with TTL {TTL}s from peer {PeerId}",
                Convert.ToHexString(request.Key), ttlSeconds, context.RemotePeerId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = payload
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DHT] Error in Store");
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"Store error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle PING RPC: Simple liveness check.
    /// </summary>
    private Task<ServiceReply> HandlePingAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        // Update routing table with the pinging peer
        _ = Task.Run(() => _routingTable.TouchAsync(
            JsonSerializer.Deserialize<PingRequest>(call.Payload)?.RequesterId ?? Array.Empty<byte>(),
            context.RemotePeerId));

        var response = new PingResponse
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(response);

        _logger.LogDebug("[DHT] Ping responded to peer {PeerId}", context.RemotePeerId);

        return Task.FromResult(new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = payload
        });
    }
}

/// <summary>
/// Request DTO for FIND_NODE RPC.
/// </summary>
public record FindNodeRequest
{
    public required byte[] TargetId { get; init; }
    public required byte[] RequesterId { get; init; }
    public int? Count { get; init; }
}

/// <summary>
/// Response DTO for FIND_NODE RPC.
/// </summary>
public record FindNodeResponse
{
    public required byte[] TargetId { get; init; }
    public required DhtNodeInfo[] Nodes { get; init; }
}

/// <summary>
/// Request DTO for FIND_VALUE RPC.
/// </summary>
public record FindValueRequest
{
    public required byte[] Key { get; init; }
    public required byte[] RequesterId { get; init; }
    public int? Count { get; init; }
}

/// <summary>
/// Response DTO for FIND_VALUE RPC.
/// </summary>
public record FindValueResponse
{
    public required byte[] Key { get; init; }
    public bool Found { get; init; }
    public byte[]? Value { get; init; }
    public DhtNodeInfo[]? ClosestNodes { get; init; }
}

/// <summary>
/// Request DTO for STORE RPC.
/// </summary>
public record StoreRequest
{
    public required byte[] Key { get; init; }
    public required byte[] Value { get; init; }
    public required byte[] RequesterId { get; init; }
    public int? TtlSeconds { get; init; }
}

/// <summary>
/// Response DTO for STORE RPC.
/// </summary>
public record StoreResponse
{
    public required byte[] Key { get; init; }
    public bool Stored { get; init; }
    public int TtlSeconds { get; init; }
}

/// <summary>
/// Request DTO for PING RPC.
/// </summary>
public record PingRequest
{
    public required byte[] RequesterId { get; init; }
}

/// <summary>
/// Response DTO for PING RPC.
/// </summary>
public record PingResponse
{
    public long Timestamp { get; init; }
}

/// <summary>
/// Information about a DHT node.
/// </summary>
public record DhtNodeInfo
{
    public required byte[] NodeId { get; init; }
    public required string Address { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}
