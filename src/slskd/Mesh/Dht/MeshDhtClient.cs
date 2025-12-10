using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.ShadowIndex;

namespace slskd.Mesh.Dht;

/// <summary>
/// Thin DHT client wrapper for MeshCore descriptors, reusing existing IDhtClient.
/// </summary>
public interface IMeshDhtClient
{
    Task PutAsync(string key, object value, int ttlSeconds, CancellationToken ct = default);
    Task<byte[]?> GetRawAsync(string key, CancellationToken ct = default);
}

public class MeshDhtClient : IMeshDhtClient
{
    private readonly ILogger<MeshDhtClient> logger;
    private readonly IDhtClient inner;

    public MeshDhtClient(ILogger<MeshDhtClient> logger, IDhtClient inner)
    {
        this.logger = logger;
        this.inner = inner;
    }

    public async Task PutAsync(string key, object value, int ttlSeconds, CancellationToken ct = default)
    {
        var payload = MessagePackSerializer.Serialize(value);
        var ttl = Math.Min(Math.Max(ttlSeconds, 60), 3600); // clamp 1m..1h
        await inner.PutAsync(key, payload, ttl, ct);
        logger.LogDebug("[MeshDHT] Put {Key} ttl={Ttl}s size={Size}", key, ttl, payload.Length);
    }

    public Task<byte[]?> GetRawAsync(string key, CancellationToken ct = default) =>
        inner.GetAsync(key, ct);
}
