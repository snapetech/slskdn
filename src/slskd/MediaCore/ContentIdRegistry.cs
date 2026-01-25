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

    // contentId -> set of externalId (values ignored) for reverse lookup; supports remove on overwrite
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _contentToExternal = new();

    /// <inheritdoc/>
    public async Task RegisterAsync(string externalId, string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External ID cannot be empty", nameof(externalId));

        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("Content ID cannot be empty", nameof(contentId));

        // If overwriting a mapping to a different contentId, remove externalId from the old contentId's set
        if (_externalToContent.TryGetValue(externalId, out var oldContentId) && oldContentId != contentId &&
            _contentToExternal.TryGetValue(oldContentId, out var oldSet))
        {
            oldSet.TryRemove(externalId, out _);
        }

        // Register external -> content mapping
        _externalToContent[externalId] = contentId;

        // Register reverse mapping
        var externalIds = _contentToExternal.GetOrAdd(contentId, _ => new ConcurrentDictionary<string, byte>());
        externalIds.TryAdd(externalId, 0);

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
            return await Task.FromResult(externalIds.Keys.ToArray());
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

        // Count mappings by domain (extract domain from contentId, e.g. content:audio:track:id -> "audio")
        var mappingsByDomain = new Dictionary<string, int>();
        foreach (var contentId in _externalToContent.Values)
        {
            var domain = ContentIdParser.GetDomain(contentId) ?? "unknown";
            mappingsByDomain.TryGetValue(domain, out var count);
            mappingsByDomain[domain] = count + 1;
        }

        var totalDomains = mappingsByDomain.Count;

        return await Task.FromResult(new ContentIdRegistryStats(
            TotalMappings: totalMappings,
            TotalDomains: totalDomains,
            MappingsByDomain: mappingsByDomain));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Array.Empty<string>();

        var results = new List<string>();
        var normalizedDomain = domain.ToLowerInvariant();

        foreach (var (externalId, contentId) in _externalToContent)
        {
            var parsedContentId = ContentIdParser.Parse(contentId);
            if (parsedContentId != null && parsedContentId.Domain.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(contentId);
            }
        }

        return await Task.FromResult(results.Distinct().ToArray());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindByDomainAndTypeAsync(string domain, string type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(type))
            return Array.Empty<string>();

        var results = new List<string>();
        var normalizedDomain = domain.ToLowerInvariant();
        var normalizedType = type.ToLowerInvariant();

        foreach (var (externalId, contentId) in _externalToContent)
        {
            var parsedContentId = ContentIdParser.Parse(contentId);
            if (parsedContentId != null &&
                parsedContentId.Domain.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase) &&
                parsedContentId.Type.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(contentId);
            }
        }

        return await Task.FromResult(results.Distinct().ToArray());
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
