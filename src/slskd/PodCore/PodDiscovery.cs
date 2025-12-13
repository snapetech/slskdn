namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// Discovers pods via DHT queries.
/// </summary>
public interface IPodDiscovery
{
    /// <summary>
    /// Discovers pods by querying DHT.
    /// </summary>
    Task<IReadOnlyList<PodMetadata>> DiscoverPodsAsync(
        string? searchQuery = null,
        List<string>? tags = null,
        string? focusContentId = null,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Discovers a specific pod by ID.
    /// </summary>
    Task<PodMetadata?> DiscoverPodAsync(string podId, CancellationToken ct = default);

    /// <summary>
    /// Discovers pods by content focus (content-linked pods).
    /// </summary>
    Task<IReadOnlyList<PodMetadata>> DiscoverPodsByContentAsync(
        string contentId,
        CancellationToken ct = default);
}

/// <summary>
/// Implements pod discovery via DHT.
/// </summary>
public class PodDiscovery : IPodDiscovery
{
    private readonly IMeshDhtClient dht;
    private readonly ILogger<PodDiscovery> logger;
    private const string PodKeyPrefix = "pod:metadata:";

    public PodDiscovery(
        IMeshDhtClient dht,
        ILogger<PodDiscovery> logger)
    {
        this.dht = dht;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<PodMetadata>> DiscoverPodsAsync(
        string? searchQuery = null,
        List<string>? tags = null,
        string? focusContentId = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var results = new List<PodMetadata>();

        try
        {
            logger.LogDebug("[PodDiscovery] Discovering pods (query: {Query}, tags: {Tags}, content: {Content}, limit: {Limit})",
                searchQuery, tags != null ? string.Join(",", tags) : "none", focusContentId, limit);

            // Get pod index from DHT
            const string PodIndexKey = "pod:index:listed";
            var index = await dht.GetAsync<PodIndex>(PodIndexKey, ct);
            
            if (index == null || index.PodIds == null || index.PodIds.Count == 0)
            {
                logger.LogDebug("[PodDiscovery] No pods found in index");
                return Array.Empty<PodMetadata>();
            }

            logger.LogDebug("[PodDiscovery] Found {Count} pods in index", index.PodIds.Count);

            // Query metadata for each pod ID
            var tasks = index.PodIds.Select(async podId =>
            {
                try
                {
                    var dhtKey = DeriveDhtKey(podId);
                    var metadata = await dht.GetAsync<PodMetadata>(dhtKey, ct);
                    return metadata;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[PodDiscovery] Failed to get metadata for pod {PodId}", podId);
                    return null;
                }
            });

            var allMetadata = await Task.WhenAll(tasks);
            var validMetadata = allMetadata.Where(m => m != null).Cast<PodMetadata>().ToList();

            // Apply filters
            var filtered = validMetadata.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var queryLower = searchQuery.ToLowerInvariant();
                filtered = filtered.Where(p => 
                    (p.Name != null && p.Name.ToLowerInvariant().Contains(queryLower)) ||
                    (p.PodId != null && p.PodId.ToLowerInvariant().Contains(queryLower)));
            }

            if (tags != null && tags.Count > 0)
            {
                filtered = filtered.Where(p => p.Tags != null && tags.Any(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(focusContentId))
            {
                filtered = filtered.Where(p => p.FocusContentId == focusContentId);
            }

            results = filtered
                .OrderByDescending(p => p.PublishedAt)
                .Take(limit)
                .ToList();

            logger.LogInformation("[PodDiscovery] Discovered {Count} pods (filtered from {Total} total)", 
                results.Count, validMetadata.Count);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodDiscovery] Error discovering pods");
            return Array.Empty<PodMetadata>();
        }
    }

    public async Task<PodMetadata?> DiscoverPodAsync(string podId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return null;
        }

        try
        {
            var dhtKey = DeriveDhtKey(podId);
            var metadata = await dht.GetAsync<PodMetadata>(dhtKey, ct);

            if (metadata != null)
            {
                logger.LogDebug("[PodDiscovery] Found pod {PodId} in DHT", podId);
            }
            else
            {
                logger.LogDebug("[PodDiscovery] Pod {PodId} not found in DHT", podId);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodDiscovery] Error discovering pod {PodId}", podId);
            return null;
        }
    }

    public async Task<IReadOnlyList<PodMetadata>> DiscoverPodsByContentAsync(
        string contentId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return Array.Empty<PodMetadata>();
        }

        try
        {
            logger.LogDebug("[PodDiscovery] Discovering pods for content {ContentId}", contentId);

            // Query DHT for pods with this content focus
            // In a real implementation, we'd query a content->pod index in DHT
            var allPods = await DiscoverPodsAsync(limit: 1000, ct: ct);
            var matchingPods = allPods
                .Where(p => p.FocusContentId == contentId)
                .ToList();

            logger.LogInformation("[PodDiscovery] Found {Count} pods for content {ContentId}", matchingPods.Count, contentId);
            return matchingPods;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodDiscovery] Error discovering pods for content {ContentId}", contentId);
            return Array.Empty<PodMetadata>();
        }
    }

    private static string DeriveDhtKey(string podId)
    {
        return $"{PodKeyPrefix}{podId}";
    }
}















