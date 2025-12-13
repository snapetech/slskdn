// <copyright file="ContentIdRegistry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore;

/// <summary>
/// In-memory ContentID registry implementation.
/// Thread-safe for concurrent access.
/// </summary>
public class ContentIdRegistry : IContentIdRegistry
{
    // externalId -> contentId mapping
    private readonly ConcurrentDictionary<string, string> _externalToContent = new();

    // contentId -> List<externalId> reverse mapping
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _contentToExternal = new();

    /// <inheritdoc/>
    public async Task RegisterAsync(string externalId, string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External ID cannot be empty", nameof(externalId));

        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("Content ID cannot be empty", nameof(contentId));

        // Register external -> content mapping
        _externalToContent[externalId] = contentId;

        // Register reverse mapping
        var externalIds = _contentToExternal.GetOrAdd(contentId, _ => new ConcurrentBag<string>());
        if (!externalIds.Contains(externalId))
        {
            externalIds.Add(externalId);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string?> ResolveAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        _externalToContent.TryGetValue(externalId, out var contentId);
        return await Task.FromResult(contentId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetExternalIdsAsync(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return Array.Empty<string>();

        if (_contentToExternal.TryGetValue(contentId, out var externalIds))
        {
            return await Task.FromResult(externalIds.ToArray());
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc/>
    public async Task<bool> IsRegisteredAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return await Task.FromResult(false);

        return await Task.FromResult(_externalToContent.ContainsKey(externalId));
    }

    /// <inheritdoc/>
    public async Task<ContentIdRegistryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalMappings = _externalToContent.Count;

        // Count mappings by domain (extract domain from external ID format)
        var mappingsByDomain = new Dictionary<string, int>();
        foreach (var externalId in _externalToContent.Keys)
        {
            var domain = ExtractDomain(externalId);
            mappingsByDomain.TryGetValue(domain, out var count);
            mappingsByDomain[domain] = count + 1;
        }

        var totalDomains = mappingsByDomain.Count;

        return await Task.FromResult(new ContentIdRegistryStats(
            TotalMappings: totalMappings,
            TotalDomains: totalDomains,
            MappingsByDomain: mappingsByDomain));
    }

    /// <summary>
    /// Extract domain from external ID (e.g., "mb:recording:123" -> "mb").
    /// </summary>
    private static string ExtractDomain(string externalId)
    {
        var colonIndex = externalId.IndexOf(':');
        return colonIndex > 0 ? externalId.Substring(0, colonIndex) : "unknown";
    }

    /// <summary>
    /// Clear all registry data (for testing).
    /// </summary>
    public void Clear()
    {
        _externalToContent.Clear();
        _contentToExternal.Clear();
    }
}
