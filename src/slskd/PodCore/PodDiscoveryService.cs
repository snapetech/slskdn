// <copyright file="PodDiscoveryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// Service for pod discovery via DHT keys.
/// </summary>
public class PodDiscoveryService : IPodDiscoveryService
{
    private readonly ILogger<PodDiscoveryService> _logger;
    private readonly IMeshDhtClient _dhtClient;
    private readonly IPodDhtPublisher _podPublisher;

    // Tracking registered pods and their discovery keys
    private readonly ConcurrentDictionary<string, PodRegistration> _registeredPods = new();
    private readonly ConcurrentDictionary<string, int> _registrationsByTag = new();
    private readonly ConcurrentDictionary<string, int> _searchesByType = new();

    private int _totalRegistrations;
    private int _activeEntries;
    private int _expiredEntries;
    private DateTimeOffset _lastOperation = DateTimeOffset.MinValue;
    private long _totalSearchTimeMs;

    public PodDiscoveryService(
        ILogger<PodDiscoveryService> logger,
        IMeshDhtClient dhtClient,
        IPodDhtPublisher podPublisher)
    {
        _logger = logger;
        _dhtClient = dhtClient;
        _podPublisher = podPublisher;
    }

    /// <inheritdoc/>
    public async Task<PodRegistrationResult> RegisterPodAsync(Pod pod, CancellationToken cancellationToken = default)
    {
        if (pod.Visibility != PodVisibility.Listed)
        {
            return new PodRegistrationResult(
                Success: false,
                PodId: pod.PodId,
                DiscoveryKeys: Array.Empty<string>(),
                RegisteredAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.MinValue,
                ErrorMessage: "Pod is not marked as listed for discovery");
        }

        var startTime = DateTimeOffset.UtcNow;
        var discoveryKeys = GenerateDiscoveryKeys(pod);

        try
        {
            _logger.LogInformation("[PodDiscovery] Registering pod {PodId} for discovery with {Count} keys", pod.PodId, discoveryKeys.Count);

            // Publish pod metadata to each discovery key
            var publishTasks = discoveryKeys.Select(key =>
                PublishToDiscoveryKeyAsync(key, pod, cancellationToken));

            await Task.WhenAll(publishTasks);

            var registeredAt = DateTimeOffset.UtcNow;
            var expiresAt = registeredAt.AddHours(24); // 24 hour TTL

            // Track registration
            var registration = new PodRegistration(
                PodId: pod.PodId,
                DiscoveryKeys: discoveryKeys,
                RegisteredAt: registeredAt,
                ExpiresAt: expiresAt);

            _registeredPods[pod.PodId] = registration;

            // Update statistics
            Interlocked.Increment(ref _totalRegistrations);
            Interlocked.Add(ref _activeEntries, discoveryKeys.Count);

            // Update tag statistics
            foreach (var tag in pod.Tags ?? Enumerable.Empty<string>())
            {
                _registrationsByTag.AddOrUpdate(tag, 1, (_, count) => count + 1);
            }

            _lastOperation = registeredAt;

            _logger.LogInformation(
                "[PodDiscovery] Successfully registered pod {PodId} with {Count} discovery keys, expires: {ExpiresAt}",
                pod.PodId, discoveryKeys.Count, expiresAt);

            return new PodRegistrationResult(
                Success: true,
                PodId: pod.PodId,
                DiscoveryKeys: discoveryKeys,
                RegisteredAt: registeredAt,
                ExpiresAt: expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error registering pod {PodId} for discovery", pod.PodId);
            return new PodRegistrationResult(
                Success: false,
                PodId: pod.PodId,
                DiscoveryKeys: discoveryKeys,
                RegisteredAt: startTime,
                ExpiresAt: startTime,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodUnregistrationResult> UnregisterPodAsync(string podId, CancellationToken cancellationToken = default)
    {
        if (!_registeredPods.TryGetValue(podId, out var registration))
        {
            return new PodUnregistrationResult(
                Success: false,
                PodId: podId,
                RemovedKeys: Array.Empty<string>(),
                ErrorMessage: "Pod not found in discovery registry");
        }

        try
        {
            _logger.LogInformation("[PodDiscovery] Unregistering pod {PodId} from discovery", podId);

            // Remove from DHT (publish null values to clear entries)
            var removalTasks = registration.DiscoveryKeys.Select(key =>
                _dhtClient.PutAsync(key, null, ttlSeconds: 300, cancellationToken));

            await Task.WhenAll(removalTasks);

            // Remove from local tracking
            _registeredPods.TryRemove(podId, out _);

            // Update statistics
            Interlocked.Add(ref _expiredEntries, registration.DiscoveryKeys.Count);
            Interlocked.Add(ref _activeEntries, -registration.DiscoveryKeys.Count);

            // Update tag statistics (simplified - would need to recount in real implementation)
            // For now, just decrement counters
            var pod = await GetPodFromPublisherAsync(podId, cancellationToken);
            if (pod != null)
            {
                foreach (var tag in pod.Tags ?? Enumerable.Empty<string>())
                {
                    _registrationsByTag.AddOrUpdate(tag, 0, (_, count) => Math.Max(0, count - 1));
                }
            }

            _lastOperation = DateTimeOffset.UtcNow;

            _logger.LogInformation("[PodDiscovery] Unregistered pod {PodId} from {Count} discovery keys", podId, registration.DiscoveryKeys.Count);

            return new PodUnregistrationResult(
                Success: true,
                PodId: podId,
                RemovedKeys: registration.DiscoveryKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error unregistering pod {PodId} from discovery", podId);
            return new PodUnregistrationResult(
                Success: false,
                PodId: podId,
                RemovedKeys: Array.Empty<string>(),
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodRegistrationResult> UpdatePodAsync(Pod pod, CancellationToken cancellationToken = default)
    {
        // Unregister old version and register new one
        await UnregisterPodAsync(pod.PodId, cancellationToken);
        return await RegisterPodAsync(pod, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PodDiscoveryResult> DiscoverPodsByNameAsync(string nameSlug, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var discoveryKey = $"pod:discover:name:{nameSlug.ToLowerInvariant()}";

        try
        {
            _logger.LogDebug("[PodDiscovery] Discovering pods by name slug: {Slug}", nameSlug);

            var podIds = await DiscoverPodIdsFromKeyAsync(discoveryKey, cancellationToken);
            var pods = await GetPodMetadataBatchAsync(podIds, cancellationToken);

            TrackSearch("name", startTime);

            return new PodDiscoveryResult(
                Pods: pods,
                SearchType: "name",
                SearchTerm: nameSlug,
                TotalFound: pods.Count,
                SearchedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by name: {Slug}", nameSlug);
            return new PodDiscoveryResult(
                Pods: Array.Empty<PodMetadata>(),
                SearchType: "name",
                SearchTerm: nameSlug,
                TotalFound: 0,
                SearchedAt: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodDiscoveryResult> DiscoverPodsByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var discoveryKey = $"pod:discover:tag:{tag.ToLowerInvariant()}";

        try
        {
            _logger.LogDebug("[PodDiscovery] Discovering pods by tag: {Tag}", tag);

            var podIds = await DiscoverPodIdsFromKeyAsync(discoveryKey, cancellationToken);
            var pods = await GetPodMetadataBatchAsync(podIds, cancellationToken);

            TrackSearch("tag", startTime);

            return new PodDiscoveryResult(
                Pods: pods,
                SearchType: "tag",
                SearchTerm: tag,
                TotalFound: pods.Count,
                SearchedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by tag: {Tag}", tag);
            return new PodDiscoveryResult(
                Pods: Array.Empty<PodMetadata>(),
                SearchType: "tag",
                SearchTerm: tag,
                TotalFound: 0,
                SearchedAt: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodDiscoveryResult> DiscoverPodsByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var tagList = tags.ToList();

        try
        {
            _logger.LogDebug("[PodDiscovery] Discovering pods by tags: {Tags}", string.Join(", ", tagList));

            // Get pod IDs for each tag
            var podIdSets = new List<HashSet<string>>();
            foreach (var tag in tagList)
            {
                var discoveryKey = $"pod:discover:tag:{tag.ToLowerInvariant()}";
                var podIds = await DiscoverPodIdsFromKeyAsync(discoveryKey, cancellationToken);
                podIdSets.Add(new HashSet<string>(podIds));
            }

            // Find intersection of all tag sets (pods that have ALL tags)
            var commonPodIds = podIdSets.FirstOrDefault() ?? new HashSet<string>();
            foreach (var podIdSet in podIdSets.Skip(1))
            {
                commonPodIds.IntersectWith(podIdSet);
            }

            var pods = await GetPodMetadataBatchAsync(commonPodIds, cancellationToken);

            TrackSearch("tags", startTime);

            return new PodDiscoveryResult(
                Pods: pods,
                SearchType: "tags",
                SearchTerm: string.Join(",", tagList),
                TotalFound: pods.Count,
                SearchedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by tags: {Tags}", string.Join(", ", tagList));
            return new PodDiscoveryResult(
                Pods: Array.Empty<PodMetadata>(),
                SearchType: "tags",
                SearchTerm: string.Join(",", tagList),
                TotalFound: 0,
                SearchedAt: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodDiscoveryResult> DiscoverAllPodsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var discoveryKey = "pod:discover:all";

        try
        {
            _logger.LogDebug("[PodDiscovery] Discovering all pods (limit: {Limit})", limit);

            var podIds = await DiscoverPodIdsFromKeyAsync(discoveryKey, cancellationToken);
            var limitedPodIds = podIds.Take(limit).ToList();
            var pods = await GetPodMetadataBatchAsync(limitedPodIds, cancellationToken);

            TrackSearch("all", startTime);

            return new PodDiscoveryResult(
                Pods: pods,
                SearchType: "all",
                SearchTerm: limit.ToString(),
                TotalFound: pods.Count,
                SearchedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering all pods");
            return new PodDiscoveryResult(
                Pods: Array.Empty<PodMetadata>(),
                SearchType: "all",
                SearchTerm: limit.ToString(),
                TotalFound: 0,
                SearchedAt: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodDiscoveryResult> DiscoverPodsByContentAsync(string contentId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var discoveryKey = $"pod:discover:content:{contentId.ToLowerInvariant()}";

        try
        {
            _logger.LogDebug("[PodDiscovery] Discovering pods by content: {ContentId}", contentId);

            var podIds = await DiscoverPodIdsFromKeyAsync(discoveryKey, cancellationToken);
            var pods = await GetPodMetadataBatchAsync(podIds, cancellationToken);

            TrackSearch("content", startTime);

            return new PodDiscoveryResult(
                Pods: pods,
                SearchType: "content",
                SearchTerm: contentId,
                TotalFound: pods.Count,
                SearchedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by content: {ContentId}", contentId);
            return new PodDiscoveryResult(
                Pods: Array.Empty<PodMetadata>(),
                SearchType: "content",
                SearchTerm: contentId,
                TotalFound: 0,
                SearchedAt: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodDiscoveryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        // Clean up expired entries
        var expired = _registeredPods.Where(kvp => kvp.Value.ExpiresAt < DateTimeOffset.UtcNow)
                                   .Select(kvp => kvp.Key)
                                   .ToList();

        foreach (var podId in expired)
        {
            if (_registeredPods.TryRemove(podId, out var registration))
            {
                Interlocked.Add(ref _expiredEntries, registration.DiscoveryKeys.Count);
                Interlocked.Add(ref _activeEntries, -registration.DiscoveryKeys.Count);
            }
        }

        var averageSearchTime = _searchesByType.Values.Sum() > 0
            ? TimeSpan.FromMilliseconds(_totalSearchTimeMs / _searchesByType.Values.Sum())
            : TimeSpan.Zero;

        return new PodDiscoveryStats(
            TotalRegisteredPods: _registeredPods.Count,
            ActiveDiscoveryEntries: _activeEntries,
            ExpiredEntries: _expiredEntries,
            RegistrationsByTag: _registrationsByTag.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            SearchesByType: _searchesByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LastDiscoveryOperation: _lastOperation,
            AverageDiscoveryTime: averageSearchTime);
    }

    /// <inheritdoc/>
    public async Task<PodRefreshResult> RefreshDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        var refreshed = 0;
        var expired = 0;
        var errors = 0;

        foreach (var (podId, registration) in _registeredPods)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (registration.ExpiresAt < now.AddHours(6)) // Refresh if < 6 hours left
                {
                    // Get current pod info and refresh registration
                    var pod = await GetPodFromPublisherAsync(podId, cancellationToken);
                    if (pod != null)
                    {
                        await UpdatePodAsync(pod, cancellationToken);
                        refreshed++;
                    }
                    else
                    {
                        // Pod no longer exists, remove from discovery
                        await UnregisterPodAsync(podId, cancellationToken);
                        expired++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PodDiscovery] Error refreshing discovery for pod {PodId}", podId);
                errors++;
            }
        }

        _logger.LogInformation(
            "[PodDiscovery] Refresh completed: {Refreshed} refreshed, {Expired} expired, {Errors} errors",
            refreshed, expired, errors);

        return new PodRefreshResult(
            Success: errors == 0,
            PodId: "all", // Refresh operation applies to all pods
            WasRepublished: refreshed > 0,
            NextRefresh: DateTimeOffset.UtcNow.AddHours(1), // Next refresh in 1 hour
            ErrorMessage: errors > 0 ? $"{errors} errors occurred during refresh" : null);
    }

    // Helper methods
    private List<string> GenerateDiscoveryKeys(Pod pod)
    {
        var keys = new List<string>();

        // Add to general discovery
        keys.Add("pod:discover:all");

        // Add by name slug (simplified - use pod name as slug)
        var nameSlug = pod.Name?.ToLowerInvariant().Replace(" ", "-") ?? "unnamed";
        keys.Add($"pod:discover:name:{nameSlug}");

        // Add by tags
        foreach (var tag in pod.Tags ?? Enumerable.Empty<string>())
        {
            keys.Add($"pod:discover:tag:{tag.ToLowerInvariant()}");
        }

        // Add by focus content (if specified)
        if (!string.IsNullOrEmpty(pod.FocusContentId))
        {
            keys.Add($"pod:discover:content:{pod.FocusContentId.ToLowerInvariant()}");
        }

        return keys;
    }

    private async Task PublishToDiscoveryKeyAsync(string discoveryKey, Pod pod, CancellationToken cancellationToken)
    {
        // For discovery, we store pod IDs under discovery keys
        // The actual pod metadata is stored under the main pod key
        var podIds = await DiscoverPodIdsFromKeyAsync(discoveryKey, cancellationToken);
        var updatedPodIds = new HashSet<string>(podIds) { pod.PodId };

        await _dhtClient.PutAsync(discoveryKey, updatedPodIds.ToList(), ttlSeconds: 24 * 60 * 60, cancellationToken);
    }

    private async Task<List<string>> DiscoverPodIdsFromKeyAsync(string discoveryKey, CancellationToken cancellationToken)
    {
        var result = await _dhtClient.GetAsync<List<string>>(discoveryKey, cancellationToken);
        return result ?? new List<string>();
    }

    private async Task<List<PodMetadata>> GetPodMetadataBatchAsync(IEnumerable<string> podIds, CancellationToken cancellationToken)
    {
        var pods = new List<PodMetadata>();

        foreach (var podId in podIds)
        {
            try
            {
                var result = await _podPublisher.GetPublishedMetadataAsync(podId, cancellationToken);
                if (result.Found && result.PublishedPod != null)
                {
                    // Convert Pod to PodMetadata for discovery results
                    var pod = result.PublishedPod;
                    var metadata = new PodMetadata
                    {
                        PodId = pod.PodId,
                        Name = pod.Name ?? "Unknown",
                        Visibility = pod.Visibility,
                        FocusContentId = pod.FocusContentId,
                        Tags = pod.Tags ?? new List<string>(),
                        ChannelCount = pod.Channels?.Count ?? 0,
                        PublishedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    pods.Add(metadata);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PodDiscovery] Error getting metadata for pod {PodId}", podId);
            }
        }

        return pods;
    }

    private async Task<Pod?> GetPodFromPublisherAsync(string podId, CancellationToken cancellationToken)
    {
        // This would need to be implemented - perhaps through a pod storage service
        // For now, return null as placeholder
        _logger.LogWarning("[PodDiscovery] GetPodFromPublisherAsync not implemented - using placeholder");
        return null;
    }

    private void TrackSearch(string searchType, DateTimeOffset startTime)
    {
        var duration = DateTimeOffset.UtcNow - startTime;
        Interlocked.Add(ref _totalSearchTimeMs, (long)duration.TotalMilliseconds);
        _searchesByType.AddOrUpdate(searchType, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Information about a pod registered for discovery.
    /// </summary>
    private record PodRegistration(
        string PodId,
        IReadOnlyList<string> DiscoveryKeys,
        DateTimeOffset RegisteredAt,
        DateTimeOffset ExpiresAt);
}
