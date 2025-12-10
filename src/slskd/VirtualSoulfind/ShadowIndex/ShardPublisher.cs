namespace slskd.VirtualSoulfind.ShadowIndex;

using Microsoft.Extensions.Options;

/// <summary>
/// Interface for DHT operations (stub for Phase 6B).
/// </summary>
public interface IDhtClient
{
    Task PutAsync(byte[] key, byte[] value, int ttlSeconds, CancellationToken ct = default);
    Task<byte[]?> GetAsync(byte[] key, CancellationToken ct = default);
    Task<List<byte[]>> GetMultipleAsync(byte[] key, CancellationToken ct = default);
}

/// <summary>
/// Stub DHT client (Phase 6B will implement real DHT).
/// </summary>
public class DhtClientStub : IDhtClient
{
    private readonly ILogger<DhtClientStub> logger;

    public DhtClientStub(ILogger<DhtClientStub> logger)
    {
        this.logger = logger;
    }

    public Task PutAsync(byte[] key, byte[] value, int ttlSeconds, CancellationToken ct)
    {
        logger.LogDebug("[DHT-STUB] PUT key={KeyHex} size={Size}b ttl={TTL}s",
            DhtKeyDerivation.ToHexString(key), value.Length, ttlSeconds);
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(byte[] key, CancellationToken ct)
    {
        logger.LogDebug("[DHT-STUB] GET key={KeyHex}",
            DhtKeyDerivation.ToHexString(key));
        return Task.FromResult<byte[]?>(null);
    }

    public Task<List<byte[]>> GetMultipleAsync(byte[] key, CancellationToken ct)
    {
        logger.LogDebug("[DHT-STUB] GET_MULTIPLE key={KeyHex}",
            DhtKeyDerivation.ToHexString(key));
        return Task.FromResult(new List<byte[]>());
    }
}

/// <summary>
/// Publishes shadow index shards to DHT periodically.
/// </summary>
public interface IShardPublisher
{
    Task StartPublishingAsync(CancellationToken ct = default);
}

/// <summary>
/// Background service that publishes shards to DHT.
/// </summary>
public class ShardPublisher : IShardPublisher
{
    private readonly ILogger<ShardPublisher> logger;
    private readonly IShadowIndexBuilder builder;
    private readonly IDhtClient dht;
    private readonly IOptionsMonitor<Options> optionsMonitor;

    public ShardPublisher(
        ILogger<ShardPublisher> logger,
        IShadowIndexBuilder builder,
        IDhtClient dht,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.builder = builder;
        this.dht = dht;
        this.optionsMonitor = optionsMonitor;
    }

    public async Task StartPublishingAsync(CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.ShadowIndex?.Enabled != true)
        {
            logger.LogInformation("[VSF-PUBLISH] Shadow index publishing disabled");
            return;
        }

        var intervalMinutes = options.VirtualSoulfind.ShadowIndex.PublishIntervalMinutes ?? 15;
        logger.LogInformation("[VSF-PUBLISH] Starting shard publisher (interval: {Interval}m)", intervalMinutes);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PublishShardsAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VSF-PUBLISH] Failed to publish shards");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), ct);
        }
    }

    private async Task PublishShardsAsync(CancellationToken ct)
    {
        // TODO: Get list of MBIDs we have observations for
        // For now, this is a placeholder that would be called per MBID
        logger.LogDebug("[VSF-PUBLISH] Publishing shards (stub)");
    }

    private async Task PublishShardForMbidAsync(string mbid, CancellationToken ct)
    {
        var shard = await builder.BuildShardAsync(mbid, ct);
        if (shard == null)
        {
            return;
        }

        var key = DhtKeyDerivation.DeriveRecordingKey(mbid);
        var value = ShardSerializer.Serialize(shard);

        await dht.PutAsync(key, value, shard.TTLSeconds, ct);

        logger.LogInformation("[VSF-PUBLISH] Published shard for {MBID}: {PeerCount} peers, {VariantCount} variants",
            mbid, shard.ApproximatePeerCount, shard.CanonicalVariants.Count);
    }
}
