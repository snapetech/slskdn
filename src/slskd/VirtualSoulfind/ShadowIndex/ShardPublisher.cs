// <copyright file="ShardPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.HashDb;

namespace slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Interface for DHT operations.
/// Phase 6B: Real implementation uses Mesh.Dht.InMemoryDhtClient (registered in Program.cs).
/// </summary>
public interface IDhtClient
{
    Task PutAsync(byte[] key, byte[] value, int ttlSeconds, CancellationToken ct = default);
    Task<byte[]?> GetAsync(byte[] key, CancellationToken ct = default);
    Task<List<byte[]>> GetMultipleAsync(byte[] key, CancellationToken ct = default);
}

/// <summary>
/// Publishes shadow index shards to DHT periodically.
/// Phase 6B: T-808 - Background service implementation.
/// </summary>
public interface IShardPublisher
{
    Task StartPublishingAsync(CancellationToken ct = default);
}

/// <summary>
/// Background service that publishes shards to DHT periodically.
/// Phase 6B: T-808 - Real implementation as BackgroundService.
/// </summary>
public class ShardPublisher : BackgroundService, IShardPublisher
{
    private readonly ILogger<ShardPublisher> logger;
    private readonly IShadowIndexBuilder builder;
    private readonly IDhtClient dht;
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
    private readonly IHashDbService? hashDb;
    private readonly IDhtRateLimiter? rateLimiter;

    public ShardPublisher(
        ILogger<ShardPublisher> logger,
        IShadowIndexBuilder builder,
        IDhtClient dht,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        IHashDbService? hashDb = null,
        IDhtRateLimiter? rateLimiter = null)
    {
        this.logger = logger;
        this.builder = builder;
        this.dht = dht;
        this.optionsMonitor = optionsMonitor;
        this.hashDb = hashDb;
        this.rateLimiter = rateLimiter;
    }

    public Task StartPublishingAsync(CancellationToken ct = default)
    {
        // This method is kept for interface compatibility but ExecuteAsync is the real implementation
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

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

        // Wait a bit before first publish to let system stabilize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishShardsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VSF-PUBLISH] Failed to publish shards");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }

        logger.LogInformation("[VSF-PUBLISH] Shard publisher stopped");
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
                logger.LogWarning(ex, "[VSF-PUBLISH] Failed to get recording IDs from HashDb: {Message}", ex.Message);
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

        var options = optionsMonitor.CurrentValue;
        // Limit to reasonable number per cycle to avoid overwhelming DHT
        var maxShardsPerCycle = options.VirtualSoulfind?.ShadowIndex?.MaxShardsPerPublish > 0
            ? options.VirtualSoulfind.ShadowIndex.MaxShardsPerPublish
            : 50; // Default: 50 shards per cycle
        var idsToPublish = recordingIds.Take(maxShardsPerCycle).ToList();

        logger.LogInformation(
            "[VSF-PUBLISH] Publishing {Count} shards (out of {Total} available)",
            idsToPublish.Count,
            recordingIds.Count);

        var publishedCount = 0;
        var failedCount = 0;

        // Publish shards in parallel (with limit and rate limiting)
        var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent publishes
        var publishTasks = idsToPublish.Select(async mbid =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                // Apply rate limiting (T-812)
                if (rateLimiter != null)
                {
                    var acquired = await rateLimiter.TryAcquireAsync(ct);
                    if (!acquired)
                    {
                        logger.LogWarning("[VSF-PUBLISH] Rate limit exceeded, skipping {MBID}", mbid);
                        Interlocked.Increment(ref failedCount);
                        return;
                    }
                }

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
            logger.LogDebug("[VSF-PUBLISH] No shard data for {MBID}", mbid);
            return;
        }

        // Apply eviction policy (trim if needed)
        if (ShardEvictionPolicy.ExceedsSizeLimit(shard))
        {
            logger.LogDebug("[VSF-PUBLISH] Shard for {MBID} exceeds size limit, trimming", mbid);
            shard = ShardEvictionPolicy.TrimShard(shard);
        }

        // Use TTL from options if configured
        var options = optionsMonitor.CurrentValue;
        var ttlSeconds = options.VirtualSoulfind?.ShadowIndex?.ShardTTLHours > 0
            ? options.VirtualSoulfind.ShadowIndex.ShardTTLHours * 3600
            : shard.TTLSeconds;

        var key = DhtKeyDerivation.DeriveRecordingKey(mbid);
        var value = ShardSerializer.Serialize(shard);

        // Apply rate limiting (T-812)
        // Note: Rate limiter would be injected if needed, but for now we rely on semaphore in PublishShardsAsync
        await dht.PutAsync(key, value, ttlSeconds, ct);

        logger.LogInformation("[VSF-PUBLISH] Published shard for {MBID}: {PeerCount} peers, {VariantCount} variants, TTL={TTL}s",
            mbid, shard.ApproximatePeerCount, shard.CanonicalVariants.Count, ttlSeconds);
    }
}
