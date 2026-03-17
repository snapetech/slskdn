// <copyright file="IMetadataPortability.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore;

/// <summary>
/// Metadata portability service for exporting/importing metadata between sources.
/// </summary>
public interface IMetadataPortability
{
    /// <summary>
    /// Export metadata for specified ContentIDs.
    /// </summary>
    /// <param name="contentIds">ContentIDs to export metadata for.</param>
    /// <param name="includeLinks">Whether to include IPLD links in export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exported metadata package.</returns>
    Task<MetadataPackage> ExportAsync(
        IEnumerable<string> contentIds,
        bool includeLinks = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Import metadata from a package.
    /// </summary>
    /// <param name="package">Metadata package to import.</param>
    /// <param name="conflictStrategy">Strategy for handling conflicts.</param>
    /// <param name="dryRun">Whether to perform a dry run without making changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import results with conflict resolution details.</returns>
    Task<MetadataImportResult> ImportAsync(
        MetadataPackage package,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Merge,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preview import conflicts without making changes.
    /// </summary>
    /// <param name="package">Metadata package to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conflict analysis results.</returns>
    Task<MetadataConflictAnalysis> AnalyzeConflictsAsync(
        MetadataPackage package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge metadata from multiple sources for a ContentID.
    /// </summary>
    /// <param name="contentId">ContentID to merge metadata for.</param>
    /// <param name="sources">Metadata sources to merge.</param>
    /// <param name="strategy">Merge strategy to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Merged metadata descriptor.</returns>
    Task<ContentDescriptor> MergeMetadataAsync(
        string contentId,
        IEnumerable<MetadataSource> sources,
        MetadataMergeStrategy strategy = MetadataMergeStrategy.PreferNewer,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exported metadata package.
/// </summary>
public record MetadataPackage(
    string Version,
    DateTimeOffset ExportedAt,
    string Source,
    IReadOnlyList<MetadataEntry> Entries,
    IReadOnlyList<IpldLink> Links,
    MetadataPackageMetadata Metadata);

/// <summary>
/// Individual metadata entry in a package.
/// </summary>
public record MetadataEntry(
    string ContentId,
    ContentDescriptor Descriptor,
    MetadataSourceInfo SourceInfo);

/// <summary>
/// Metadata source information.
/// </summary>
public record MetadataSourceInfo(
    string Name,
    DateTimeOffset Timestamp,
    string Version,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// Package metadata.
/// </summary>
public record MetadataPackageMetadata(
    int TotalEntries,
    int TotalLinks,
    IReadOnlyDictionary<string, int> EntriesByDomain,
    string Checksum);

/// <summary>
/// Import operation results.
/// </summary>
public record MetadataImportResult(
    bool Success,
    int EntriesProcessed,
    int EntriesImported,
    int EntriesSkipped,
    int ConflictsResolved,
    IReadOnlyList<MetadataConflict> Conflicts,
    IReadOnlyList<string> Errors,
    TimeSpan Duration);

/// <summary>
/// Conflict analysis results.
/// </summary>
public record MetadataConflictAnalysis(
    int TotalEntries,
    int ConflictingEntries,
    int CleanEntries,
    IReadOnlyList<MetadataConflict> Conflicts,
    IReadOnlyDictionary<ConflictResolutionStrategy, int> RecommendedStrategies);

/// <summary>
/// Metadata conflict details.
/// </summary>
public record MetadataConflict(
    string ContentId,
    string ConflictType,
    string Description,
    IReadOnlyList<MetadataConflictResolution> Resolutions);

/// <summary>
/// Conflict resolution option.
/// </summary>
public record MetadataConflictResolution(
    ConflictResolutionStrategy Strategy,
    string Description,
    ContentDescriptor? ResultDescriptor);

/// <summary>
/// Metadata source for merging.
/// </summary>
public record MetadataSource(
    string Name,
    ContentDescriptor Descriptor,
    DateTimeOffset Timestamp,
    int Priority = 0);

/// <summary>
/// Conflict resolution strategies.
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Skip conflicting entries.
    /// </summary>
    Skip,

    /// <summary>
    /// Overwrite existing data with imported data.
    /// </summary>
    Overwrite,

    /// <summary>
    /// Merge data intelligently.
    /// </summary>
    Merge,

    /// <summary>
    /// Keep existing data, ignore imported data.
    /// </summary>
    KeepExisting,

    /// <summary>
    /// Prompt user for resolution (not implemented for automated imports).
    /// </summary>
    Interactive
}

/// <summary>
/// Metadata merge strategies.
/// </summary>
public enum MetadataMergeStrategy
{
    /// <summary>
    /// Prefer newer metadata based on timestamps.
    /// </summary>
    PreferNewer,

    /// <summary>
    /// Prefer higher priority sources.
    /// </summary>
    PreferHigherPriority,

    /// <summary>
    /// Combine all metadata fields.
    /// </summary>
    CombineAll,

    /// <summary>
    /// Use custom merge logic.
    /// </summary>
    Custom
}
