// <copyright file="JobsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using slskd.Core.Security;
using slskd.Integrations.MusicBrainz;
using slskd.Jobs;

/// <summary>
/// Provides slskdn-native job management API.
/// </summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class JobsController : ControllerBase
{
    private readonly IDiscographyJobService discographyJobService;
    private readonly ILabelCrateJobService labelCrateJobService;
    private readonly IMusicBrainzClient musicBrainzClient;
    private readonly IJobServiceWithList? jobServiceList;
    private readonly ILogger<JobsController> logger;

    public JobsController(
        IDiscographyJobService discographyJobService,
        ILabelCrateJobService labelCrateJobService,
        IMusicBrainzClient musicBrainzClient,
        ILogger<JobsController> logger,
        IJobServiceWithList? jobServiceList = null)
    {
        this.discographyJobService = discographyJobService;
        this.labelCrateJobService = labelCrateJobService;
        this.musicBrainzClient = musicBrainzClient;
        this.jobServiceList = jobServiceList;
        this.logger = logger;
    }

    /// <summary>
    /// Create a MusicBrainz release download job.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [HttpPost("mb-release")]
    [Authorize]
    public async Task<IActionResult> CreateMbReleaseJob(
        [FromBody] MbReleaseJobRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request is required");
        }

        var releaseId = request.MbReleaseId?.Trim() ?? string.Empty;
        var targetDir = request.TargetDir?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(releaseId))
        {
            return BadRequest("mb_release_id is required");
        }

        logger.LogInformation("Creating MB release job for {ReleaseId}", releaseId);

        var release = await musicBrainzClient.GetReleaseAsync(releaseId, cancellationToken);
        if (release == null || string.IsNullOrWhiteSpace(release.MusicBrainzArtistId))
        {
            return NotFound($"Unable to resolve release {releaseId} into a SongID-ready MusicBrainz target.");
        }

        var jobId = await discographyJobService.CreateJobAsync(
            new DiscographyJobRequest
            {
                ArtistId = release.MusicBrainzArtistId,
                Profile = DiscographyProfile.AllReleases,
                TargetDirectory = targetDir,
                ReleaseIds = new List<string> { releaseId },
            },
            cancellationToken);

        return Ok(new
        {
            job_id = jobId,
            status = "pending",
        });
    }

    /// <summary>
    /// Create a discography download job.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [HttpPost("discography")]
    [Authorize]
    public async Task<IActionResult> CreateDiscographyJob(
        [FromBody] DiscographyJobRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request is required");
        }

        request.ArtistId = request.ArtistId?.Trim() ?? string.Empty;
        request.TargetDirectory = request.TargetDirectory?.Trim() ?? string.Empty;
        request.ReleaseIds = request.ReleaseIds?
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(request.ArtistId))
        {
            return BadRequest("artist_id is required");
        }

        logger.LogInformation("Creating discography job for {ArtistId}", request.ArtistId);

        var jobId = await discographyJobService.CreateJobAsync(request, cancellationToken);

        return Ok(new
        {
            job_id = jobId,
            status = "pending",
        });
    }

    /// <summary>
    /// Create a label crate download job.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [HttpPost("label-crate")]
    [Authorize]
    public async Task<IActionResult> CreateLabelCrateJob(
        [FromBody] LabelCrateJobRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request is required");
        }

        request.LabelId = request.LabelId?.Trim();
        request.LabelName = request.LabelName?.Trim();

        if (string.IsNullOrWhiteSpace(request.LabelId) && string.IsNullOrWhiteSpace(request.LabelName))
        {
            return BadRequest("label_id or label_name is required");
        }

        logger.LogInformation("Creating label crate job for {Label}", request.LabelName ?? request.LabelId);

        var jobId = await labelCrateJobService.CreateJobAsync(request, cancellationToken);

        return Ok(new
        {
            job_id = jobId,
            status = "pending",
        });
    }

    /// <summary>
    /// Get all jobs with optional filtering, pagination, and sorting (T-1410).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [HttpGet]
    [Authorize]
    public Task<IActionResult> GetJobs(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        CancellationToken cancellationToken)
    {
        type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        sortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();
        sortOrder = string.IsNullOrWhiteSpace(sortOrder) ? null : sortOrder.Trim();

        logger.LogDebug("Getting jobs with filters: type={Type}, status={Status}, limit={Limit}, offset={Offset}, sortBy={SortBy}, sortOrder={SortOrder}",
            type, status, limit, offset, sortBy, sortOrder);

        var allJobs = new List<object>();

        // Get discography jobs
        if (type == null || type.Equals("discography", StringComparison.OrdinalIgnoreCase))
        {
            if (jobServiceList != null)
            {
                var discJobs = jobServiceList.GetAllDiscographyJobs();
                if (discJobs != null)
                {
                    foreach (var job in discJobs)
                    {
                        if (status == null || MapStatus(job.Status).Equals(status, StringComparison.OrdinalIgnoreCase))
                        {
                            allJobs.Add(new
                            {
                                id = job.JobId,
                                type = "discography",
                                status = MapStatus(job.Status),
                                created_at = job.CreatedAt,
                                progress = new
                                {
                                    releases_total = job.TotalReleases,
                                    releases_done = job.CompletedReleases,
                                    releases_failed = job.FailedReleases
                                },
                            });
                        }
                    }
                }
            }
        }

        // Get label crate jobs
        if (type == null || type.Equals("label_crate", StringComparison.OrdinalIgnoreCase))
        {
            if (jobServiceList != null)
            {
                var labelJobs = jobServiceList.GetAllLabelCrateJobs();
                if (labelJobs != null)
                {
                    foreach (var job in labelJobs)
                    {
                        if (status == null || MapStatus(job.Status).Equals(status, StringComparison.OrdinalIgnoreCase))
                        {
                            allJobs.Add(new
                            {
                                id = job.JobId,
                                type = "label_crate",
                                status = MapStatus(job.Status),
                                created_at = job.CreatedAt,
                                progress = new
                                {
                                    releases_total = job.TotalReleases,
                                    releases_done = job.CompletedReleases,
                                    releases_failed = job.FailedReleases
                                },
                            });
                        }
                    }
                }
            }
        }

        // T-1410: Apply sorting
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var sortOrderLower = (sortOrder ?? "asc").ToLowerInvariant();
            var descending = sortOrderLower == "desc" || sortOrderLower == "descending";

            allJobs = sortBy.ToLowerInvariant() switch
            {
                "status" => descending
                    ? allJobs.OrderByDescending(j => ((dynamic)j).status).ToList()
                    : allJobs.OrderBy(j => ((dynamic)j).status).ToList(),
                "created_at" or "created" => descending
                    ? allJobs.OrderByDescending(j => ((dynamic)j).created_at).ToList()
                    : allJobs.OrderBy(j => ((dynamic)j).created_at).ToList(),
                "id" => descending
                    ? allJobs.OrderByDescending(j => ((dynamic)j).id).ToList()
                    : allJobs.OrderBy(j => ((dynamic)j).id).ToList(),
                _ => allJobs, // Unknown sort field, return unsorted
            };
        }
        else
        {
            // Default: sort by created_at descending (newest first)
            allJobs = allJobs.OrderByDescending(j => ((dynamic)j).created_at).ToList();
        }

        // T-1410: Apply pagination
        var totalCount = allJobs.Count;
        var effectiveOffset = Math.Max(0, offset ?? 0);
        var effectiveLimit = limit > 0 ? limit.Value : 100; // Default limit 100, max reasonable

        var paginatedJobs = allJobs
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .ToList();

        return Task.FromResult<IActionResult>(Ok(new
        {
            jobs = paginatedJobs,
            total = totalCount,
            limit = effectiveLimit,
            offset = effectiveOffset,
            has_more = (effectiveOffset + effectiveLimit) < totalCount,
        }));
    }

    /// <summary>
    /// Get a single job by ID.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetJob(
        string id,
        CancellationToken cancellationToken)
    {
        id = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest();
        }

        logger.LogDebug("Getting job: {JobId}", id);

        // Try discography job first
        var discographyJob = await discographyJobService.GetJobAsync(id, cancellationToken);
        if (discographyJob != null)
        {
            return Ok(new
            {
                id = discographyJob.JobId,
                type = "discography",
                status = MapStatus(discographyJob.Status),
                spec = new
                {
                    artist_id = discographyJob.ArtistId,
                    profile = discographyJob.Profile.ToString(),
                    target_dir = discographyJob.TargetDirectory,
                },
                progress = new
                {
                    releases_total = discographyJob.TotalReleases,
                    releases_done = discographyJob.CompletedReleases,
                    releases_failed = discographyJob.FailedReleases,
                },
                created_at = discographyJob.CreatedAt,
            });
        }

        // Try label crate job
        var labelCrateJob = await labelCrateJobService.GetJobAsync(id, cancellationToken);
        if (labelCrateJob != null)
        {
            return Ok(new
            {
                id = labelCrateJob.JobId,
                type = "label_crate",
                status = MapStatus(labelCrateJob.Status),
                spec = new
                {
                    label_name = labelCrateJob.LabelName,
                },
                progress = new
                {
                    releases_total = labelCrateJob.TotalReleases,
                    releases_done = labelCrateJob.CompletedReleases,
                    releases_failed = labelCrateJob.FailedReleases,
                },
                created_at = labelCrateJob.CreatedAt,
            });
        }

        return NotFound();
    }

    private static string MapStatus(JobStatus status)
    {
        return status switch
        {
            JobStatus.Pending => "pending",
            JobStatus.Running => "running",
            JobStatus.Completed => "completed",
            JobStatus.Failed => "failed",
            _ => "unknown",
        };
    }
}

public record MbReleaseJobRequest(
    [property: JsonPropertyName("mb_release_id")] string MbReleaseId,
    [property: JsonPropertyName("target_dir")] string TargetDir,
    [property: JsonPropertyName("tracks")] string Tracks = "all",
    [property: JsonPropertyName("constraints")] JobConstraints? Constraints = null);

public record JobConstraints(
    [property: JsonPropertyName("preferred_codecs")] string[]? PreferredCodecs = null,
    [property: JsonPropertyName("allow_lossy")] bool AllowLossy = false,
    [property: JsonPropertyName("prefer_canonical")] bool PreferCanonical = true,
    [property: JsonPropertyName("use_overlay")] bool UseOverlay = true);

/// <summary>
/// Helper interface for test host to access all jobs.
/// </summary>
public interface IJobServiceWithList
{
    IReadOnlyList<DiscographyJob> GetAllDiscographyJobs();
    IReadOnlyList<LabelCrateJob> GetAllLabelCrateJobs();
}
