// <copyright file="IPodDiscoveryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for pod discovery via DHT.
/// </summary>
public interface IPodDiscoveryService
{
    /// <summary>
    /// Registers a pod for discovery by publishing its metadata to DHT discovery keys.
    /// </summary>
    /// <param name="pod">The pod to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration result.</returns>
    Task<PodRegistrationResult> RegisterPodAsync(Pod pod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a pod from discovery by removing its metadata from DHT.
    /// </summary>
    /// <param name="podId">The pod ID to unregister.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unregistration result.</returns>
    Task<PodUnregistrationResult> UnregisterPodAsync(string podId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates pod discovery metadata when pod information changes.
    /// </summary>
    /// <param name="pod">The updated pod.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    Task<PodRegistrationResult> UpdatePodAsync(Pod pod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers pods by name slug.
    /// </summary>
    /// <param name="nameSlug">The pod name slug to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching pods.</returns>
    Task<PodDiscoveryResult> DiscoverPodsByNameAsync(string nameSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers pods by tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pods with the specified tag.</returns>
    Task<PodDiscoveryResult> DiscoverPodsByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers pods by multiple tags (AND logic).
    /// </summary>
    /// <param name="tags">The tags to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pods matching all specified tags.</returns>
    Task<PodDiscoveryResult> DiscoverPodsByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a general list of discoverable pods.
    /// </summary>
    /// <param name="limit">Maximum number of pods to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A sample of discoverable pods.</returns>
    Task<PodDiscoveryResult> DiscoverAllPodsAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for pods by content ID association.
    /// </summary>
    /// <param name="contentId">The content ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pods associated with the content.</returns>
    Task<PodDiscoveryResult> DiscoverPodsByContentAsync(string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets discovery statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery statistics.</returns>
    Task<PodDiscoveryStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes discovery entries for registered pods.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    Task<PodRefreshResult> RefreshDiscoveryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pod registration for discovery.
/// </summary>
public record PodRegistrationResult(
    bool Success,
    string PodId,
    IReadOnlyList<string> DiscoveryKeys,
    DateTimeOffset RegisteredAt,
    DateTimeOffset ExpiresAt,
    string? ErrorMessage = null);

/// <summary>
/// Result of pod unregistration from discovery.
/// </summary>
public record PodUnregistrationResult(
    bool Success,
    string PodId,
    IReadOnlyList<string> RemovedKeys,
    string? ErrorMessage = null);

