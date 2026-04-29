// <copyright file="MetadataPortability.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace slskd.MediaCore;

/// <summary>
/// Metadata portability service implementation.
/// </summary>
public class MetadataPortability : IMetadataPortability
{
    private readonly IContentIdRegistry _registry;
    private readonly IDescriptorRetriever _descriptorRetriever;
    private readonly IIpldMapper _ipldMapper;
    private readonly ILogger<MetadataPortability> _logger;

    public MetadataPortability(
        IContentIdRegistry registry,
        IDescriptorRetriever descriptorRetriever,
        IIpldMapper ipldMapper,
        ILogger<MetadataPortability> logger)
    {
        _registry = registry;
        _descriptorRetriever = descriptorRetriever;
        _ipldMapper = ipldMapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MetadataPackage> ExportAsync(
        IEnumerable<string> contentIds,
        bool includeLinks = true,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<MetadataEntry>();
        var allLinks = new List<IpldLink>();
        var entriesByDomain = new Dictionary<string, int>();

        foreach (var contentId in contentIds
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Select(contentId => contentId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var retrieval = await _descriptorRetriever.RetrieveAsync(contentId, cancellationToken: cancellationToken);
                if (!retrieval.Found || retrieval.Descriptor == null)
                {
                    _logger.LogWarning(
                        "[MetadataPortability] Skipping export for {ContentId}: descriptor not found",
                        contentId);
                    continue;
                }

                var descriptor = retrieval.Descriptor;
                var sourceInfo = new MetadataSourceInfo(
                    Name: "slskdN",
                    Timestamp: DateTimeOffset.UtcNow,
                    Version: "1.0.0",
                    Properties: new Dictionary<string, string>
                    {
                        ["exported"] = "true",
                        ["from_cache"] = retrieval.FromCache ? "true" : "false"
                    });

                entries.Add(new MetadataEntry(contentId, descriptor, sourceInfo));

                // Track domain statistics
                var parsed = ContentIdParser.Parse(contentId);
                var domain = parsed == null
                    ? "unknown"
                    : ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type);
                entriesByDomain.TryGetValue(domain, out var count);
                entriesByDomain[domain] = count + 1;

                // Collect links if requested
                if (includeLinks)
                {
                    var links = await _ipldMapper.GetGraphAsync(contentId, maxDepth: 1, cancellationToken);
                    allLinks.AddRange(links.Paths.SelectMany(p => p.Links));
                }

                _logger.LogInformation("[MetadataPortability] Exported metadata for {ContentId}", contentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MetadataPortability] Failed to export metadata for {ContentId}", contentId);
            }
        }

        var packageMetadata = new MetadataPackageMetadata(
            TotalEntries: entries.Count,
            TotalLinks: allLinks.Count,
            EntriesByDomain: entriesByDomain,
            Checksum: ComputePackageChecksum(entries, allLinks));

        var package = new MetadataPackage(
            Version: "1.0",
            ExportedAt: DateTimeOffset.UtcNow,
            Source: "slskdN",
            Entries: entries,
            Links: allLinks
                .Where(link => !string.IsNullOrWhiteSpace(link.Target))
                .Distinct()
                .ToArray(),
            Metadata: packageMetadata);

        _logger.LogInformation(
            "[MetadataPortability] Exported {EntryCount} entries with {LinkCount} links",
            entries.Count, allLinks.Count);

        return package;
    }

    /// <inheritdoc/>
    public async Task<MetadataImportResult> ImportAsync(
        MetadataPackage package,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Merge,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var entriesProcessed = 0;
        var entriesImported = 0;
        var entriesSkipped = 0;
        var conflictsResolved = 0;
        var conflicts = new List<MetadataConflict>();
        var errors = new List<string>();

        foreach (var entry in package.Entries)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            entriesProcessed++;
            if (!ContentIdParser.IsValid(entry.ContentId))
            {
                entriesSkipped++;
                _logger.LogWarning(
                    "[MetadataPortability] Skipping invalid ContentID during import: {ContentId}",
                    entry.ContentId);
                continue;
            }

            try
            {
                var exists = await _registry.IsContentIdRegisteredAsync(entry.ContentId, cancellationToken);

                if (exists)
                {
                    // Handle conflict
                    var conflict = await AnalyzeConflictAsync(entry.ContentId, entry, cancellationToken);
                    if (conflict != null)
                    {
                        conflicts.Add(conflict);

                        var resolution = conflict.Resolutions
                            .FirstOrDefault(r => r.Strategy == conflictStrategy);

                        if (resolution != null && !dryRun)
                        {
                            // Apply resolution
                            await ApplyResolutionAsync(entry.ContentId, resolution, cancellationToken);
                            conflictsResolved++;
                            entriesImported++;
                        }
                        else
                        {
                            entriesSkipped++;
                        }
                    }
                }
                else
                {
                    // New entry - import directly
                    if (!dryRun)
                    {
                        var imported = await ImportNewEntryAsync(entry, cancellationToken);
                        if (imported)
                        {
                            entriesImported++;
                        }
                        else
                        {
                            entriesSkipped++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var error = $"Failed to import {entry.ContentId}";
                errors.Add(error);
                _logger.LogError(ex, "[MetadataPortability] {Error}", error);
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        var result = new MetadataImportResult(
            Success: errors.Count == 0,
            EntriesProcessed: entriesProcessed,
            EntriesImported: entriesImported,
            EntriesSkipped: entriesSkipped,
            ConflictsResolved: conflictsResolved,
            Conflicts: conflicts,
            Errors: errors,
            Duration: duration);

        _logger.LogInformation(
            "[MetadataPortability] Import completed: {Processed} processed, {Imported} imported, {Skipped} skipped, {Conflicts} conflicts in {Duration}",
            entriesProcessed, entriesImported, entriesSkipped, conflictsResolved, duration);

        return result;
    }

    /// <inheritdoc/>
    public async Task<MetadataConflictAnalysis> AnalyzeConflictsAsync(
        MetadataPackage package,
        CancellationToken cancellationToken = default)
    {
        var conflicts = new List<MetadataConflict>();
        var conflictingEntries = 0;

        foreach (var entry in package.Entries)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var exists = await _registry.IsContentIdRegisteredAsync(entry.ContentId, cancellationToken);
            if (exists)
            {
                var conflict = await AnalyzeConflictAsync(entry.ContentId, entry, cancellationToken);
                if (conflict != null)
                {
                    conflicts.Add(conflict);
                    conflictingEntries++;
                }
            }
        }

        var recommendedStrategies = new Dictionary<ConflictResolutionStrategy, int>
        {
            [ConflictResolutionStrategy.Merge] = conflicts.Count(c => c.Resolutions.Any(r => r.Strategy == ConflictResolutionStrategy.Merge)),
            [ConflictResolutionStrategy.Overwrite] = conflicts.Count(c => c.Resolutions.Any(r => r.Strategy == ConflictResolutionStrategy.Overwrite)),
            [ConflictResolutionStrategy.Skip] = conflicts.Count(c => c.Resolutions.Any(r => r.Strategy == ConflictResolutionStrategy.Skip))
        };

        return new MetadataConflictAnalysis(
            TotalEntries: package.Entries.Count,
            ConflictingEntries: conflictingEntries,
            CleanEntries: package.Entries.Count - conflictingEntries,
            Conflicts: conflicts,
            RecommendedStrategies: recommendedStrategies);
    }

    /// <inheritdoc/>
    public async Task<ContentDescriptor> MergeMetadataAsync(
        string contentId,
        IEnumerable<MetadataSource> sources,
        MetadataMergeStrategy strategy = MetadataMergeStrategy.PreferNewer,
        CancellationToken cancellationToken = default)
    {
        var sourceList = sources.ToList();
        if (!sourceList.Any())
            throw new ArgumentException("At least one metadata source is required", nameof(sources));

        ContentDescriptor result;

        switch (strategy)
        {
            case MetadataMergeStrategy.PreferNewer:
                result = sourceList.OrderByDescending(s => s.Timestamp).First().Descriptor;
                break;

            case MetadataMergeStrategy.PreferHigherPriority:
                result = sourceList.OrderByDescending(s => s.Priority).First().Descriptor;
                break;

            case MetadataMergeStrategy.CombineAll:
                result = await CombineAllMetadataAsync(sourceList, cancellationToken);
                break;

            default:
                result = sourceList.First().Descriptor;
                break;
        }

        _logger.LogInformation(
            "[MetadataPortability] Merged {SourceCount} metadata sources for {ContentId} using {Strategy}",
            sourceList.Count, contentId, strategy);

        return result;
    }

    private async Task<MetadataConflict?> AnalyzeConflictAsync(
        string contentId,
        MetadataEntry newEntry,
        CancellationToken cancellationToken)
    {
        var resolutions = new List<MetadataConflictResolution>
        {
            new MetadataConflictResolution(
                ConflictResolutionStrategy.Skip,
                "Skip importing this entry",
                null),

            new MetadataConflictResolution(
                ConflictResolutionStrategy.Overwrite,
                "Replace existing metadata with imported data",
                newEntry.Descriptor),

            new MetadataConflictResolution(
                ConflictResolutionStrategy.KeepExisting,
                "Keep existing metadata unchanged",
                null)
        };

        var existing = await _descriptorRetriever.RetrieveAsync(contentId, cancellationToken: cancellationToken);
        if (existing.Found && existing.Descriptor != null)
        {
            var mergedDescriptor = await MergeMetadataAsync(contentId, new[]
            {
                new MetadataSource("existing", existing.Descriptor, existing.RetrievedAt, 1),
                new MetadataSource("imported", newEntry.Descriptor, newEntry.SourceInfo.Timestamp, 2),
            }, cancellationToken: cancellationToken);

            resolutions.Insert(2, new MetadataConflictResolution(
                ConflictResolutionStrategy.Merge,
                "Merge existing and imported metadata",
                mergedDescriptor));
        }

        return new MetadataConflict(
            ContentId: contentId,
            ConflictType: "MetadataExists",
            Description: $"Metadata already exists for {contentId}",
            Resolutions: resolutions);
    }

    private async Task ApplyResolutionAsync(
        string contentId,
        MetadataConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would update the registry/database
        // For now, just log the operation
        _logger.LogInformation(
            "[MetadataPortability] Applied {Strategy} resolution for {ContentId}",
            resolution.Strategy, contentId);

        await Task.CompletedTask;
    }

    private async Task<bool> ImportNewEntryAsync(MetadataEntry entry, CancellationToken cancellationToken)
    {
        var parsed = ContentIdParser.Parse(entry.ContentId);
        if (parsed != null)
        {
            var normalizedDomain = ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type);
            var externalId = $"{normalizedDomain}:{parsed.Type}:{parsed.Id}";
            await _registry.RegisterAsync(externalId, entry.ContentId, cancellationToken);

            _logger.LogInformation(
                "[MetadataPortability] Imported new entry for {ContentId}",
                entry.ContentId);
            return true;
        }

        _logger.LogWarning(
            "[MetadataPortability] Skipping new entry with invalid ContentID: {ContentId}",
            entry.ContentId);
        return false;
    }

    private async Task<ContentDescriptor> CombineAllMetadataAsync(
        IEnumerable<MetadataSource> sources,
        CancellationToken cancellationToken)
    {
        // Combine metadata from all sources
        var descriptors = sources.Select(s => s.Descriptor).ToList();
        var combined = new ContentDescriptor
        {
            ContentId = descriptors.First().ContentId,

            // Combine hashes from all sources
            Hashes = descriptors.SelectMany(d => d.Hashes ?? Enumerable.Empty<ContentHash>()).Distinct().ToList(),

            // Combine perceptual hashes
            PerceptualHashes = descriptors.SelectMany(d => d.PerceptualHashes ?? Enumerable.Empty<PerceptualHash>()).Distinct().ToList(),

            // Use the largest size if available
            SizeBytes = descriptors.Max(d => d.SizeBytes),

            // Prefer non-null codec
            Codec = descriptors.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Codec))?.Codec,

            // Average confidence
            Confidence = descriptors.Where(d => d.Confidence.HasValue).Average(d => d.Confidence)
        };

        await Task.CompletedTask;
        return combined;
    }

    private static string ComputePackageChecksum(IEnumerable<MetadataEntry> entries, IEnumerable<IpldLink> links)
    {
        using var sha256 = SHA256.Create();
        var data = JsonSerializer.Serialize(new { entries, links });
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
