// <copyright file="ShardPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.HashDb;

namespace slskd.VirtualSoulfind.ShadowIndex;

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
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
    private readonly IHashDbService hashDb;

    public ShardPublisher(
        ILogger<ShardPublisher> logger,
        IShadowIndexBuilder builder,
        IDhtClient dht,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        IHashDbService hashDb = null)
    {
        this.logger = logger;
        this.builder = builder;
        this.dht = dht;
        this.optionsMonitor = optionsMonitor;
        this.hashDb = hashDb;
    }

    public async Task StartPublishingAsync(CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.ShadowIndex?.Enabled != true)
        {
            logger.LogInformation("[VSF-PUBLISH] Shadow index publishing disabled");
            return;
        }

        var intervalMinutes = options.VirtualSoulfind.ShadowIndex.PublishIntervalMinutes > 0
            ? options.VirtualSoulfind.ShadowIndex.PublishIntervalMinutes
            : 15;
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
        logger.LogDebug("[VSF-PUBLISH] Starting shard publishing cycle");

        // Get list of MBIDs we have observations/variants for
        List<string> recordingIds;

        if (hashDb != null)
        {
            try
            {
                recordingIds = await hashDb.GetRecordingIdsWithVariantsAsync(ct);
                logger.LogDebug("[VSF-PUBLISH] Found {Count} recording IDs with variants", recordingIds.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-PUBLISH] Failed to get recording IDs from HashDb");
                recordingIds = new List<string>();
            }
        }
        else
        {
            logger.LogWarning("[VSF-PUBLISH] HashDb not available, cannot get recording IDs");
            recordingIds = new List<string>();
        }

        if (recordingIds.Count == 0)
        {
            logger.LogDebug("[VSF-PUBLISH] No recording IDs to publish");
            return;
        }

        // Limit to reasonable number per cycle to avoid overwhelming DHT
        const int maxShardsPerCycle = 50; // Publish max 50 shards per cycle
        var idsToPublish = recordingIds.Take(maxShardsPerCycle).ToList();

        logger.LogInformation(
            "[VSF-PUBLISH] Publishing {Count} shards (out of {Total} available)",
            idsToPublish.Count,
            recordingIds.Count);

        var publishedCount = 0;
        var failedCount = 0;

        // Publish shards in parallel (with limit)
        var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent publishes
        var publishTasks = idsToPublish.Select(async mbid =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await PublishShardForMbidAsync(mbid, ct);
                Interlocked.Increment(ref publishedCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-PUBLISH] Failed to publish shard for {MBID}", mbid);
                Interlocked.Increment(ref failedCount);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(publishTasks);

        logger.LogInformation("[VSF-PUBLISH] Publishing cycle complete: {Published} published, {Failed} failed",
            publishedCount, failedCount);
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
