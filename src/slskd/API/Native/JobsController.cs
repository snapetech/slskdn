namespace slskd.API.Native;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Jobs;
using slskd.Integrations.MusicBrainz;

/// <summary>
/// Provides slskdn-native job management API.
/// </summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly IDiscographyJobService discographyJobService;
    private readonly ILabelCrateJobService labelCrateJobService;
    private readonly ILogger<JobsController> logger;

    public JobsController(
        IDiscographyJobService discographyJobService,
        ILabelCrateJobService labelCrateJobService,
        ILogger<JobsController> logger)
    {
        this.discographyJobService = discographyJobService;
        this.labelCrateJobService = labelCrateJobService;
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
        // TODO: Implement dedicated MB release job service
        var job = await discographyJobService.CreateJobAsync(
            new DiscographyJobRequest
            {
                ArtistId = request.MbReleaseId, // Temp: treat as artist for now
                Profile = DiscographyProfile.AllReleases,
                TargetDirectory = request.TargetDir,
                PreferredCodecs = request.Constraints?.PreferredCodecs ?? new[] { "FLAC" },
                AllowLossy = request.Constraints?.AllowLossy ?? false,
                PreferCanonical = request.Constraints?.PreferCanonical ?? true,
                UseOverlay = request.Constraints?.UseOverlay ?? true
            },
            cancellationToken);

        return Ok(new
        {
            job_id = job.JobId,
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

        var job = await discographyJobService.CreateJobAsync(request, cancellationToken);

        return Ok(new
        {
            job_id = job.JobId,
            status = MapStatus(job.Status)
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

        var job = await labelCrateJobService.CreateJobAsync(request, cancellationToken);

        return Ok(new
        {
            job_id = job.JobId,
            status = MapStatus(job.Status)
        });
    }

    /// <summary>
    /// Get all jobs with optional filtering.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? type,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting jobs: type={Type}, status={Status}", type, status);

        // Get both types of jobs
        var discographyJobs = await discographyJobService.GetAllJobsAsync(cancellationToken);
        var labelCrateJobs = await labelCrateJobService.GetAllJobsAsync(cancellationToken);

        var allJobs = new List<object>();

        // Map discography jobs
        allJobs.AddRange(discographyJobs
            .Where(j => string.IsNullOrEmpty(type) || type.ToLowerInvariant() == "discography")
            .Where(j => string.IsNullOrEmpty(status) || MapStatus(j.Status).ToLowerInvariant() == status.ToLowerInvariant())
            .Select(j => new
            {
                id = j.JobId,
                type = "discography",
                status = MapStatus(j.Status),
                spec = new
                {
                    artist_id = j.ArtistId,
                    profile = j.Profile.ToString(),
                    target_dir = j.TargetDirectory
                },
                progress = new
                {
                    releases_total = j.TotalReleases,
                    releases_done = j.CompletedReleases,
                    releases_failed = j.FailedReleases
                },
                created_at = j.CreatedAt,
                updated_at = j.UpdatedAt
            }));

        // Map label crate jobs
        allJobs.AddRange(labelCrateJobs
            .Where(j => string.IsNullOrEmpty(type) || type.ToLowerInvariant() == "label_crate")
            .Where(j => string.IsNullOrEmpty(status) || MapStatus(j.Status).ToLowerInvariant() == status.ToLowerInvariant())
            .Select(j => new
            {
                id = j.JobId,
                type = "label_crate",
                status = MapStatus(j.Status),
                spec = new
                {
                    label_name = j.LabelName,
                    target_dir = j.TargetDirectory
                },
                progress = new
                {
                    releases_total = j.TotalReleases,
                    releases_done = j.CompletedReleases,
                    releases_failed = j.FailedReleases
                },
                created_at = j.CreatedAt,
                updated_at = j.UpdatedAt
            }));

        return Ok(new { jobs = allJobs });
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
                created_at = discographyJob.CreatedAt,
                updated_at = discographyJob.UpdatedAt
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
                    target_dir = labelCrateJob.TargetDirectory
                },
                progress = new
                {
                    releases_total = labelCrateJob.TotalReleases,
                    releases_done = labelCrateJob.CompletedReleases,
                    releases_failed = labelCrateJob.FailedReleases
                },
                created_at = labelCrateJob.CreatedAt,
                updated_at = labelCrateJob.UpdatedAt
            });
        }

        return NotFound();
    }

    private static string MapStatus(JobStatus status)
    {
        return status switch
        {
            JobStatus.Pending => "pending",
            JobStatus.InProgress => "running",
            JobStatus.Completed => "completed",
            JobStatus.Failed => "failed",
            JobStatus.Cancelled => "cancelled",
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
