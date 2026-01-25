// <copyright file="MeshDhtClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.Transport;
using slskd.VirtualSoulfind.ShadowIndex;
using System.Security.Cryptography;
using System.Text;

namespace slskd.Mesh.Dht;

/// <summary>
/// Thin DHT client wrapper for MeshCore descriptors, reusing existing IDhtClient.
/// </summary>
public interface IMeshDhtClient
{
    Task PutAsync(string key, object value, int ttlSeconds, CancellationToken ct = default);
    Task<byte[]?> GetRawAsync(string key, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Find closest node IDs for a target key (Kademlia-style).
    /// </summary>
    Task<IReadOnlyList<KNode>> FindNodesAsync(byte[] targetId, int count = 20, CancellationToken ct = default);

    /// <summary>
    /// Find value by key, returning multiple replicas when available.
    /// </summary>
    Task<IReadOnlyList<byte[]>> FindValueAsync(byte[] key, CancellationToken ct = default);
}

public class MeshDhtClient : IMeshDhtClient
{
    private readonly ILogger<MeshDhtClient> logger;
    private readonly IDhtClient inner;
    private readonly Lazy<DhtService?> dhtService;
    private readonly int _maxPayload;

    public MeshDhtClient(ILogger<MeshDhtClient> logger, IDhtClient inner, IServiceProvider? serviceProvider = null, IOptions<MeshOptions>? meshOptions = null)
    {
        this.logger = logger;
        this.inner = inner;
        _maxPayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
        // Use Lazy to break circular dependency: DhtService depends on KademliaRpcClient which depends on IMeshServiceClient
        // which depends on IMeshServiceDirectory which depends on IMeshDhtClient (this) which would depend on DhtService
        this.dhtService = new Lazy<DhtService?>(() => serviceProvider?.GetService<DhtService>());
    }

    public async Task PutAsync(string key, object value, int ttlSeconds, CancellationToken ct = default)
    {
        var payload = value as byte[] ?? MessagePackSerializer.Serialize(value);
        var ttl = Math.Min(Math.Max(ttlSeconds, 60), 3600); // clamp 1m..1h
        await inner.PutAsync(KeyBytes(key), payload, ttl, ct);
        logger.LogDebug("[MeshDHT] Put {Key} ttl={Ttl}s size={Size}", key, ttl, payload.Length);
    }

    public Task<byte[]?> GetRawAsync(string key, CancellationToken ct = default) =>
        inner.GetAsync(KeyBytes(key), ct);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var raw = await inner.GetAsync(KeyBytes(key), ct);
        if (raw == null) return default;
        try
        {
            return SecurityUtils.ParseMessagePackSafely<T>(raw, _maxPayload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDHT] Failed to decode payload for {Key}", key);
            return default;
        }
    }

    public async Task<IReadOnlyList<KNode>> FindNodesAsync(byte[] targetId, int count = 20, CancellationToken ct = default)
    {
        // Use distributed DHT service if available
        if (dhtService.Value != null)
        {
            return await dhtService.Value.FindNodeAsync(targetId, ct);
        }

        // Fallback to local routing table
        if (inner is InMemoryDhtClient mem)
        {
            return mem.FindClosest(targetId, count);
        }

        // Fallback: no routing available
        return Array.Empty<KNode>();
    }

    public async Task<IReadOnlyList<byte[]>> FindValueAsync(byte[] key, CancellationToken ct = default)
    {
        // Use distributed DHT service if available
        if (dhtService.Value != null)
        {
            var result = await dhtService.Value.FindValueAsync(key, ct);
            if (result.Found && result.Value != null)
            {
                return new List<byte[]> { result.Value };
            }
            return Array.Empty<byte[]>();
        }

        // Fallback to local storage
        if (inner is InMemoryDhtClient mem)
        {
            return await mem.GetMultipleAsync(key, ct);
        }

        var val = await inner.GetAsync(key, ct);
        return val == null ? Array.Empty<byte[]>() : new List<byte[]> { val };
    }

    private static byte[] KeyBytes(string key) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(key));
}
