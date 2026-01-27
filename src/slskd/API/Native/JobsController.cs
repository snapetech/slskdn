// <copyright file="JobsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using slskd.Jobs;
using slskd.Integrations.MusicBrainz;

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
    private readonly IJobServiceWithList? jobServiceList;
    private readonly ILogger<JobsController> logger;

    public JobsController(
        IDiscographyJobService discographyJobService,
        ILabelCrateJobService labelCrateJobService,
        ILogger<JobsController> logger,
        IJobServiceWithList? jobServiceList = null)
    {
        this.discographyJobService = discographyJobService;
        this.labelCrateJobService = labelCrateJobService;
        this.jobServiceList = jobServiceList;
        this.logger = logger;
    }

    /// <summary>
    /// Create a MusicBrainz release download job.
    /// </summary>
    [HttpPost("mb-release")]
    [Authorize]
    public async Task<IActionResult> CreateMbReleaseJob(
        [FromBody] MbReleaseJobRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating MB release job for {ReleaseId}", request.MbReleaseId);

        // For now, create as a discography job with single release
        var jobId = await discographyJobService.CreateJobAsync(
            new DiscographyJobRequest
            {
                ArtistId = request.MbReleaseId, // treated as artist ID placeholder
                Profile = DiscographyProfile.AllReleases,
                TargetDirectory = request.TargetDir
            },
            cancellationToken);

        return Ok(new
        {
            job_id = jobId,
            status = "pending"
        });
    }

    /// <summary>
    /// Create a discography download job.
    /// </summary>
    [HttpPost("discography")]
    [Authorize]
    public async Task<IActionResult> CreateDiscographyJob(
        [FromBody] DiscographyJobRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating discography job for {ArtistId}", request.ArtistId);

        var jobId = await discographyJobService.CreateJobAsync(request, cancellationToken);

        return Ok(new
        {
            job_id = jobId,
            status = "pending"
        });
    }

    /// <summary>
    /// Create a label crate download job.
    /// </summary>
    [HttpPost("label-crate")]
    [Authorize]
    public async Task<IActionResult> CreateLabelCrateJob(
        [FromBody] LabelCrateJobRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating label crate job for {Label}", request.LabelName);

        var jobId = await labelCrateJobService.CreateJobAsync(request, cancellationToken);

        return Ok(new
        {
            job_id = jobId,
            status = "pending"
        });
    }

    /// <summary>
    /// Get all jobs with optional filtering, pagination, and sorting (T-1410).
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        CancellationToken cancellationToken)
    {
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
                                }
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
                                }
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
                _ => allJobs // Unknown sort field, return unsorted
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

        await Task.CompletedTask;
        return Ok(new
        {
            jobs = paginatedJobs,
            total = totalCount,
            limit = effectiveLimit,
            offset = effectiveOffset,
            has_more = (effectiveOffset + effectiveLimit) < totalCount
        });
    }

    /// <summary>
    /// Get a single job by ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetJob(
        string id,
        CancellationToken cancellationToken)
    {
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
                    target_dir = discographyJob.TargetDirectory
                },
                progress = new
                {
                    releases_total = discographyJob.TotalReleases,
                    releases_done = discographyJob.CompletedReleases,
                    releases_failed = discographyJob.FailedReleases
                },
                created_at = discographyJob.CreatedAt
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
                    label_name = labelCrateJob.LabelName
                },
                progress = new
                {
                    releases_total = labelCrateJob.TotalReleases,
                    releases_done = labelCrateJob.CompletedReleases,
                    releases_failed = labelCrateJob.FailedReleases
                },
                created_at = labelCrateJob.CreatedAt
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
            _ => "unknown"
        };
    }
}

public record MbReleaseJobRequest(
    string MbReleaseId,
    string TargetDir,
    string Tracks = "all",
    JobConstraints? Constraints = null);

public record JobConstraints(
    string[]? PreferredCodecs = null,
    bool AllowLossy = false,
    bool PreferCanonical = true,
    bool UseOverlay = true);

/// <summary>
/// Helper interface for test host to access all jobs.
/// </summary>
public interface IJobServiceWithList
{
    IReadOnlyList<DiscographyJob> GetAllDiscographyJobs();
    IReadOnlyList<LabelCrateJob> GetAllLabelCrateJobs();
}
