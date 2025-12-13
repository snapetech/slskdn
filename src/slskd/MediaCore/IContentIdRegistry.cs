// <copyright file="IContentIdRegistry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore;

/// <summary>
/// ContentID registry for mapping external identifiers to internal ContentIDs.
/// Supports multiple external ID domains (MBID, IMDB, etc.) and resolution.
/// </summary>
public interface IContentIdRegistry
{
    /// <summary>
    /// Register a mapping from an external ID to an internal ContentID.
    /// </summary>
    /// <param name="externalId">The external identifier (e.g., "mb:recording:12345").</param>
    /// <param name="contentId">The internal ContentID (e.g., "content:mb:recording:12345").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RegisterAsync(string externalId, string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve an external ID to its internal ContentID.
    /// </summary>
    /// <param name="externalId">The external identifier to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The internal ContentID if found, null otherwise.</returns>
    Task<string?> ResolveAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all external IDs mapped to a specific ContentID.
    /// </summary>
    /// <param name="contentId">The internal ContentID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of external IDs mapped to the ContentID.</returns>
    Task<IReadOnlyList<string>> GetExternalIdsAsync(string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an external ID is registered.
    /// </summary>
    /// <param name="externalId">The external identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the external ID is registered, false otherwise.</returns>
    Task<bool> IsRegisteredAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get registry statistics.
    /// </summary>
    /// <returns>Registry statistics including total mappings and domains.</returns>
    Task<ContentIdRegistryStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all ContentIDs for a specific domain.
    /// </summary>
    /// <param name="domain">The domain to search for (e.g., "audio", "video").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of ContentIDs in the specified domain.</returns>
    Task<IReadOnlyList<string>> FindByDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all ContentIDs for a specific domain and type.
    /// </summary>
    /// <param name="domain">The domain to search for.</param>
    /// <param name="type">The type within the domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of ContentIDs matching the domain and type.</returns>
    Task<IReadOnlyList<string>> FindByDomainAndTypeAsync(string domain, string type, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for the ContentID registry.
/// </summary>
public record ContentIdRegistryStats(
    int TotalMappings,
    int TotalDomains,
    IReadOnlyDictionary<string, int> MappingsByDomain);
