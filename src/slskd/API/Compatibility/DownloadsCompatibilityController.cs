// <copyright file="DownloadsCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using slskd.Core.Security;

using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd;
using slskd.Transfers;
using slskd.Transfers.Downloads;
using Soulseek;

/// <summary>
/// Provides slskd-compatible downloads API.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class DownloadsCompatibilityController : ControllerBase
{
    private readonly IDownloadService downloadService;
    private readonly ILogger<DownloadsCompatibilityController> logger;
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;

    public DownloadsCompatibilityController(
        IDownloadService downloadService,
        ILogger<DownloadsCompatibilityController> logger,
        IOptionsMonitor<slskd.Options> optionsMonitor)
    {
        this.downloadService = downloadService;
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
    }

    /// <summary>
    /// Create downloads (slskd compatibility).
    /// </summary>
    [HttpPost("downloads")]
    [Authorize]
    public async Task<IActionResult> CreateDownloads(
        [FromBody] DownloadRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Compatibility download: {ItemCount} items", request.Items?.Count ?? 0);

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { error = "Items are required" });
        }

        var downloadIds = new List<string>();
        var enqueued = new List<slskd.Transfers.Transfer>();
        var failed = new List<string>();

        foreach (var item in request.Items)
        {
            try
            {
                // Enqueue download using IDownloadService
                var files = new List<(string Filename, long Size)>
                {
                    (item.RemotePath, 0) // Size unknown, will be determined during transfer
                };

                var (enqueuedTransfers, failedFiles) = await downloadService.EnqueueAsync(
                    item.User,
                    files,
                    cancellationToken);

                if (enqueuedTransfers.Count > 0)
                {
                    enqueued.AddRange(enqueuedTransfers);
                    downloadIds.Add(enqueuedTransfers[0].Id.ToString("N"));
                }

                if (failedFiles.Count > 0)
                {
                    failed.AddRange(failedFiles);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue download for {User}/{Path}", item.User, item.RemotePath);
                failed.Add($"{item.User}/{item.RemotePath}: {ex.Message}");
            }
        }

        return Ok(new
        {
            DownloadIds = downloadIds,
            Enqueued = enqueued.Count,
            Failed = failed.Count,
            Errors = failed.Count > 0 ? failed : null
        });
    }

    /// <summary>
    /// Get all downloads (slskd compatibility).
    /// </summary>
    [HttpGet("downloads")]
    [Authorize]
    public async Task<IActionResult> GetDownloads(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting downloads list with status filter: {Status}", status);

        // Get all downloads from IDownloadService
        var allDownloads = downloadService.List(includeRemoved: false);

        // Filter by status if provided
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusLower = status.ToLowerInvariant();
            allDownloads = allDownloads.Where(d =>
            {
                var mappedStatus = MapStatus(d.State);
                return mappedStatus.Equals(statusLower, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        // Convert to compatibility format
        var options = optionsMonitor.CurrentValue;
        var downloads = allDownloads.Select(d =>
        {
            // Determine base directory based on transfer state
            var baseDirectory = d.State.HasFlag(TransferStates.Completed) && d.State.HasFlag(TransferStates.Succeeded)
                ? options.Directories.Downloads
                : options.Directories.Incomplete;
            var localPath = d.Filename.ToLocalFilename(baseDirectory);
            return new
            {
                Id = d.Id.ToString("N"),
                User = d.Username,
                RemotePath = d.Filename,
                LocalPath = localPath,
                Status = MapStatus(d.State),
                Progress = d.PercentComplete / 100.0,
                Size = d.Size,
                Remaining = d.Size - d.BytesTransferred
            };
        }).ToList();

        await Task.CompletedTask;
        return Ok(new { Downloads = downloads });
    }

    /// <summary>
    /// Get a single download (slskd compatibility).
    /// </summary>
    [HttpGet("downloads/{id}")]
    [Authorize]
    public async Task<IActionResult> GetDownload(
        string id,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting download: {Id}", id);

        if (!Guid.TryParse(id, out var downloadGuid))
        {
            return BadRequest(new { error = "Invalid download ID format" });
        }

        // Find download by ID
        var download = downloadService.Find(d => d.Id == downloadGuid);

        if (download == null)
        {
            return NotFound(new { error = "Download not found" });
        }

        // Determine base directory based on transfer state
        var options = optionsMonitor.CurrentValue;
        var baseDirectory = download.State.HasFlag(TransferStates.Completed) && download.State.HasFlag(TransferStates.Succeeded)
            ? options.Directories.Downloads
            : options.Directories.Incomplete;
        var localPath = download.Filename.ToLocalFilename(baseDirectory);

        await Task.CompletedTask;
        return Ok(new
        {
            Id = download.Id.ToString("N"),
            User = download.Username,
            RemotePath = download.Filename,
            LocalPath = localPath,
            Status = MapStatus(download.State),
            Progress = download.PercentComplete / 100.0,
            Size = download.Size,
            Remaining = download.Size - download.BytesTransferred,
            Speed = download.AverageSpeed
        });
    }

    private static string MapStatus(TransferStates state)
    {
        return state switch
        {
            TransferStates.Queued => "queued",
            TransferStates.Initializing => "queued",
            TransferStates.Requested => "running",
            TransferStates.InProgress => "running",
            TransferStates.Completed => "completed",
            TransferStates.Cancelled => "cancelled",
            TransferStates.TimedOut => "failed",
            _ => "failed"
        };
    }

    private static string ParseStatus(string status)
    {
        return status.ToLowerInvariant();
    }
}

public record DownloadRequest(List<DownloadItem> Items);

public record DownloadItem(
    string User,
    string RemotePath,
    string TargetDir,
    string? TargetFilename = null);
