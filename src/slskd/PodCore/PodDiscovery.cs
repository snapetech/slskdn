// <copyright file="PodDiscovery.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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
        if (limit <= 0)
        {
            return Array.Empty<PodMetadata>();
        }

        var results = new List<PodMetadata>();
        var normalizedQuery = searchQuery?.Trim();
        var normalizedFocusContentId = focusContentId?.Trim();
        var normalizedTags = tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        try
        {
            logger.LogDebug("[PodDiscovery] Discovering pods (query: {Query}, tags: {Tags}, content: {Content}, limit: {Limit})",
                normalizedQuery, normalizedTags != null ? string.Join(",", normalizedTags) : "none", normalizedFocusContentId, limit);

            // Get pod index from DHT
            const string PodIndexKey = "pod:index:listed";
            var index = await dht.GetAsync<PodIndex>(PodIndexKey, ct);

            if (index == null || index.PodIds == null || index.PodIds.Count == 0)
            {
                logger.LogDebug("[PodDiscovery] No pods found in index");
                return Array.Empty<PodMetadata>();
            }

            var uniquePodIds = index.PodIds
                .Where(podId => !string.IsNullOrWhiteSpace(podId))
                .Select(podId => podId.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (uniquePodIds.Count == 0)
            {
                logger.LogDebug("[PodDiscovery] Pod index only contained blank entries");
                return Array.Empty<PodMetadata>();
            }

            logger.LogDebug("[PodDiscovery] Found {Count} pods in index", uniquePodIds.Count);

            // Query metadata for each pod ID
            var tasks = uniquePodIds.Select(async podId =>
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

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                var queryLower = normalizedQuery.ToLowerInvariant();
                filtered = filtered.Where(p =>
                    (p.Name != null && p.Name.ToLowerInvariant().Contains(queryLower)) ||
                    (p.PodId != null && p.PodId.ToLowerInvariant().Contains(queryLower)));
            }

            if (normalizedTags != null && normalizedTags.Count > 0)
            {
                filtered = filtered.Where(p => p.Tags != null && normalizedTags.Any(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(normalizedFocusContentId))
            {
                filtered = filtered.Where(p => string.Equals(p.FocusContentId, normalizedFocusContentId, StringComparison.Ordinal));
            }

            results = filtered
                .GroupBy(p => p.PodId, StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(p => p.PublishedAt)
                    .First())
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

        var normalizedPodId = podId.Trim();

        try
        {
            var dhtKey = DeriveDhtKey(normalizedPodId);
            var metadata = await dht.GetAsync<PodMetadata>(dhtKey, ct);

            if (metadata != null)
            {
                logger.LogDebug("[PodDiscovery] Found pod {PodId} in DHT", normalizedPodId);
            }
            else
            {
                logger.LogDebug("[PodDiscovery] Pod {PodId} not found in DHT", normalizedPodId);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodDiscovery] Error discovering pod {PodId}", normalizedPodId);
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

        var normalizedContentId = contentId.Trim();

        try
        {
            logger.LogDebug("[PodDiscovery] Discovering pods for content {ContentId}", normalizedContentId);

            // Query DHT for pods with this content focus
            // In a real implementation, we'd query a content->pod index in DHT
            var allPods = await DiscoverPodsAsync(limit: 1000, ct: ct);
            var matchingPods = allPods
                .Where(p => string.Equals(p.FocusContentId, normalizedContentId, StringComparison.Ordinal))
                .ToList();

            logger.LogInformation("[PodDiscovery] Found {Count} pods for content {ContentId}", matchingPods.Count, normalizedContentId);
            return matchingPods;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PodDiscovery] Error discovering pods for content {ContentId}", normalizedContentId);
            return Array.Empty<PodMetadata>();
        }
    }

    private static string DeriveDhtKey(string podId)
    {
        return $"{PodKeyPrefix}{podId}";
    }
}
