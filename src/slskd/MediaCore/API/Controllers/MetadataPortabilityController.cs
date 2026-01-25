// <copyright file="MetadataPortabilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Metadata portability API controller.
/// </summary>
[Route("api/v0/mediacore/portability")]
[ApiController]
[AllowAnonymous] // PR-02: intended-public
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class MetadataPortabilityController : ControllerBase
{
    private readonly ILogger<MetadataPortabilityController> _logger;
    private readonly IMetadataPortability _portability;

    public MetadataPortabilityController(
        ILogger<MetadataPortabilityController> logger,
        IMetadataPortability portability)
    {
        _logger = logger;
        _portability = portability;
    }

    /// <summary>
    /// Export metadata for specified ContentIDs.
    /// </summary>
    /// <param name="request">Export request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata package for download.</returns>
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] MetadataExportRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.ContentIds == null || !request.ContentIds.Any())
        {
            return BadRequest("At least one ContentID is required for export");
        }

        try
        {
            var package = await _portability.ExportAsync(
                request.ContentIds,
                request.IncludeLinks ?? true,
                cancellationToken);

            _logger.LogInformation(
                "[MetadataPortability] Exported package with {EntryCount} entries",
                package.Entries.Count);

            // Return as JSON for now (in production, could return file download)
            return Ok(package);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MetadataPortability] Failed to export metadata");
            return StatusCode(500, new { error = "Failed to export metadata" });
        }
    }

    /// <summary>
    /// Import metadata from a package.
    /// </summary>
    /// <param name="request">Import request with package data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import operation results.</returns>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] MetadataImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Package == null)
        {
            return BadRequest("Metadata package is required");
        }

        try
        {
            var result = await _portability.ImportAsync(
                request.Package,
                request.ConflictStrategy ?? ConflictResolutionStrategy.Merge,
                request.DryRun ?? false,
                cancellationToken);

            _logger.LogInformation(
                "[MetadataPortability] Import completed: {Processed} processed, {Imported} imported, {Skipped} skipped",
                result.EntriesProcessed, result.EntriesImported, result.EntriesSkipped);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MetadataPortability] Failed to import metadata");
            return StatusCode(500, new { error = "Failed to import metadata" });
        }
    }

    /// <summary>
    /// Analyze potential conflicts in a metadata package.
    /// </summary>
    /// <param name="request">Conflict analysis request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conflict analysis results.</returns>
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeConflicts([FromBody] MetadataAnalyzeRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Package == null)
        {
            return BadRequest("Metadata package is required");
        }

        try
        {
            var analysis = await _portability.AnalyzeConflictsAsync(request.Package, cancellationToken);

            _logger.LogInformation(
                "[MetadataPortability] Analyzed {TotalEntries} entries: {ConflictingEntries} conflicts, {CleanEntries} clean",
                analysis.TotalEntries, analysis.ConflictingEntries, analysis.CleanEntries);

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MetadataPortability] Failed to analyze conflicts");
            return StatusCode(500, new { error = "Failed to analyze conflicts" });
        }
    }

    /// <summary>
    /// Get supported conflict resolution strategies.
    /// </summary>
    /// <returns>List of available strategies with descriptions.</returns>
    [HttpGet("strategies")]
    public IActionResult GetStrategies()
    {
        var strategies = new[]
        {
            new
            {
                strategy = ConflictResolutionStrategy.Skip,
                name = "Skip",
                description = "Skip conflicting entries without importing them"
            },
            new
            {
                strategy = ConflictResolutionStrategy.Overwrite,
                name = "Overwrite",
                description = "Replace existing metadata with imported data"
            },
            new
            {
                strategy = ConflictResolutionStrategy.Merge,
                name = "Merge",
                description = "Intelligently merge existing and imported metadata"
            },
            new
            {
                strategy = ConflictResolutionStrategy.KeepExisting,
                name = "Keep Existing",
                description = "Keep existing metadata and ignore imported data"
            }
        };

        return Ok(new { strategies });
    }

    /// <summary>
    /// Get supported merge strategies.
    /// </summary>
    /// <returns>List of available merge strategies with descriptions.</returns>
    [HttpGet("merge-strategies")]
    public IActionResult GetMergeStrategies()
    {
        var strategies = new[]
        {
            new
            {
                strategy = MetadataMergeStrategy.PreferNewer,
                name = "Prefer Newer",
                description = "Prefer metadata with newer timestamps"
            },
            new
            {
                strategy = MetadataMergeStrategy.PreferHigherPriority,
                name = "Prefer Higher Priority",
                description = "Prefer metadata from higher priority sources"
            },
            new
            {
                strategy = MetadataMergeStrategy.CombineAll,
                name = "Combine All",
                description = "Combine metadata fields from all sources"
            }
        };

        return Ok(new { strategies });
    }
}

/// <summary>
/// Metadata export request.
/// </summary>
public record MetadataExportRequest(
    IReadOnlyList<string> ContentIds,
    bool? IncludeLinks = true);

/// <summary>
/// Metadata import request.
/// </summary>
public record MetadataImportRequest(
    MetadataPackage Package,
    ConflictResolutionStrategy? ConflictStrategy = null,
    bool? DryRun = null);

/// <summary>
/// Metadata conflict analysis request.
/// </summary>
public record MetadataAnalyzeRequest(MetadataPackage Package);

