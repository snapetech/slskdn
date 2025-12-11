namespace slskd.API.Native;

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
    /// Get all jobs with optional filtering.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? type,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting jobs with filters: type={Type}, status={Status}", type, status);

        var allJobs = new List<object>();

        // Get discography jobs
        if (type == null || type.Equals("discography", StringComparison.OrdinalIgnoreCase))
        {
            if (jobServiceList != null)
            {
                var discJobs = jobServiceList.GetAllDiscographyJobs();
                foreach (var job in discJobs)
                {
                    if (status == null || MapStatus(job.Status).Equals(status, StringComparison.OrdinalIgnoreCase))
                    {
                        allJobs.Add(new
                        {
                            id = job.JobId,
                            type = "discography",
                            status = MapStatus(job.Status)
                        });
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
                foreach (var job in labelJobs)
                {
                    if (status == null || MapStatus(job.Status).Equals(status, StringComparison.OrdinalIgnoreCase))
                    {
                        allJobs.Add(new
                        {
                            id = job.JobId,
                            type = "label_crate",
                            status = MapStatus(job.Status)
                        });
                    }
                }
            }
        }

        await Task.CompletedTask;
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
