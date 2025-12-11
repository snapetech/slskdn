namespace slskd.PodCore;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// Publishes pod metadata to DHT for discovery.
/// </summary>
public interface IPodPublisher
{
    /// <summary>
    /// Publishes a pod's metadata to the DHT.
    /// </summary>
    Task PublishPodAsync(Pod pod, CancellationToken ct = default);

    /// <summary>
    /// Removes a pod's metadata from the DHT (unpublish).
    /// </summary>
    Task UnpublishPodAsync(string podId, CancellationToken ct = default);

    /// <summary>
    /// Refreshes pod metadata in DHT (updates TTL).
    /// </summary>
    Task RefreshPodAsync(string podId, CancellationToken ct = default);
}

/// <summary>
/// Implements pod metadata publishing to DHT.
/// </summary>
public class PodPublisher : IPodPublisher
{
    private readonly IMeshDhtClient dht;
    private readonly IPodService podService;
    private readonly ILogger<PodPublisher> logger;
    private const int DefaultTTLSeconds = 3600; // 1 hour
    private const string PodKeyPrefix = "pod:metadata:";

    public PodPublisher(
        IMeshDhtClient dht,
        IPodService podService,
        ILogger<PodPublisher> logger)
    {
        this.dht = dht;
        this.podService = podService;
        this.logger = logger;
    }

    public async Task PublishPodAsync(Pod pod, CancellationToken ct = default)
    {
        if (pod == null || string.IsNullOrWhiteSpace(pod.PodId))
        {
            logger.LogWarning("[PodPublisher] Cannot publish pod - invalid pod data");
            return;
        }

        // Only publish listed pods
        if (pod.Visibility != PodVisibility.Listed)
        {
            logger.LogDebug("[PodPublisher] Skipping publish for unlisted pod {PodId}", pod.PodId);
            return;
        }

        try
        {
            var dhtKey = DeriveDhtKey(pod.PodId);
            
            // Create pod metadata for DHT (exclude sensitive data)
            var metadata = new PodMetadata
            {
                PodId = pod.PodId,
                Name = pod.Name,
                Visibility = pod.Visibility,
                FocusContentId = pod.FocusContentId,
                Tags = pod.Tags ?? new List<string>(),
                ChannelCount = pod.Channels?.Count ?? 0,
                PublishedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            // Publish to DHT with TTL
            await dht.PutAsync(dhtKey, metadata, DefaultTTLSeconds, ct);

            // Update pod index (list of all listed pod IDs)
            await UpdatePodIndexAsync(pod.PodId, add: true, ct);

            logger.LogInformation("[PodPublisher] Published pod {PodId} ({Name}) to DHT with TTL {TTL}s",
                pod.PodId, pod.Name, DefaultTTLSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodPublisher] Failed to publish pod {PodId} to DHT", pod.PodId);
        }
    }

    public async Task UnpublishPodAsync(string podId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return;
        }

        try
        {
            logger.LogInformation("[PodPublisher] Unpublishing pod {PodId} from DHT", podId);
            
            // Remove from index
            await UpdatePodIndexAsync(podId, add: false, ct);
            
            // Note: DHT doesn't support deletion, metadata entry will expire naturally
            // We could publish a tombstone with short TTL if needed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodPublisher] Failed to unpublish pod {PodId} from DHT", podId);
        }
    }

    public async Task RefreshPodAsync(string podId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return;
        }

        try
        {
            var pod = await podService.GetPodAsync(podId, ct);
            if (pod != null)
            {
                await PublishPodAsync(pod, ct);
            }
            else
            {
                logger.LogWarning("[PodPublisher] Cannot refresh pod {PodId} - pod not found", podId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodPublisher] Failed to refresh pod {PodId} in DHT", podId);
        }
    }

    private async Task UpdatePodIndexAsync(string podId, bool add, CancellationToken ct)
    {
        const string indexKey = "pod:index:listed";
        try
        {
            // Get current index
            var index = await dht.GetAsync<PodIndex>(indexKey, ct) ?? new PodIndex { PodIds = new List<string>() };

            if (add)
            {
                if (!index.PodIds.Contains(podId))
                {
                    index.PodIds.Add(podId);
                    index.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await dht.PutAsync(indexKey, index, DefaultTTLSeconds, ct);
                    logger.LogDebug("[PodPublisher] Added pod {PodId} to index", podId);
                }
            }
            else
            {
                if (index.PodIds.Remove(podId))
                {
                    index.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await dht.PutAsync(indexKey, index, DefaultTTLSeconds, ct);
                    logger.LogDebug("[PodPublisher] Removed pod {PodId} from index", podId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PodPublisher] Failed to update pod index");
        }
    }

    private static string DeriveDhtKey(string podId)
    {
        return $"{PodKeyPrefix}{podId}";
    }
}

/// <summary>
/// Pod index stored in DHT (list of all listed pod IDs).
/// </summary>
public class PodIndex
{
    public List<string> PodIds { get; set; } = new();
    public long UpdatedAt { get; set; } // Unix timestamp in milliseconds
}

/// <summary>
/// Background service that periodically refreshes pod metadata in DHT.
/// </summary>
public class PodPublisherBackgroundService : BackgroundService
{
    private readonly IPodService podService;
    private readonly IPodPublisher podPublisher;
    private readonly ILogger<PodPublisherBackgroundService> logger;
    private const int RefreshIntervalMinutes = 30; // Refresh every 30 minutes

    public PodPublisherBackgroundService(
        IPodService podService,
        IPodPublisher podPublisher,
        ILogger<PodPublisherBackgroundService> logger)
    {
        this.podService = podService;
        this.podPublisher = podPublisher;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[PodPublisher] Starting background refresh service (interval: {Interval} minutes)", RefreshIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(RefreshIntervalMinutes), stoppingToken);

                // Refresh all listed pods
                var pods = await podService.ListAsync(stoppingToken);
                var listedPods = pods.Where(p => p.Visibility == PodVisibility.Listed).ToList();

                logger.LogDebug("[PodPublisher] Refreshing {Count} listed pods in DHT", listedPods.Count);

                foreach (var pod in listedPods)
                {
                    try
                    {
                        await podPublisher.RefreshPodAsync(pod.PodId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[PodPublisher] Failed to refresh pod {PodId} in DHT", pod.PodId);
                    }
                }

                logger.LogInformation("[PodPublisher] Refreshed {Count} pods in DHT", listedPods.Count);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PodPublisher] Error in background refresh cycle");
                // Continue running despite errors
            }
        }

        logger.LogInformation("[PodPublisher] Background refresh service stopped");
    }
}

/// <summary>
/// Pod metadata published to DHT (public information only).
/// </summary>
public class PodMetadata
{
    public string PodId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PodVisibility Visibility { get; set; }
    public string? FocusContentId { get; set; }
    public List<string> Tags { get; set; } = new();
    public int ChannelCount { get; set; }
    public long PublishedAt { get; set; } // Unix timestamp in milliseconds
}

