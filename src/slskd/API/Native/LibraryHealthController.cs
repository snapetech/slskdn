namespace slskd.API.Native;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.LibraryHealth;

/// <summary>
/// Provides slskdn-native library health API.
/// </summary>
[ApiController]
[Route("api/slskdn/library")]
[Produces("application/json")]
public class LibraryHealthController : ControllerBase
{
    private readonly ILibraryHealthService healthService;
    private readonly IOptionsMonitor<Options> optionsMonitor;
    private readonly ILogger<LibraryHealthController> logger;

    public LibraryHealthController(
        ILibraryHealthService healthService,
        IOptionsMonitor<Options> optionsMonitor,
        ILogger<LibraryHealthController> logger)
    {
        this.healthService = healthService;
        this.optionsMonitor = optionsMonitor;
        this.logger = logger;
    }

    /// <summary>
    /// Get library health summary and issues.
    /// </summary>
    [HttpGet("health")]
    [Authorize]
    public async Task<IActionResult> GetHealth(
        [FromQuery] string? path,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.LibraryHealth?.Enabled != true)
        {
            return BadRequest(new { error = "Library health not enabled" });
        }

        logger.LogInformation("Library health check requested for path: {Path}", path ?? "(all)");

        var summary = await healthService.GetSummaryAsync(path, cancellationToken);
        var issues = await healthService.GetIssuesAsync(
            new LibraryHealthIssueFilter
            {
                LibraryPath = path,
                Limit = limit
            },
            cancellationToken);

        return Ok(new
        {
            path = path ?? "(all)",
            summary = new
            {
                suspected_transcodes = summary.SuspectedTranscodes,
                non_canonical_variants = summary.NonCanonicalVariants,
                incomplete_releases = summary.IncompleteReleases,
                total_issues = summary.TotalIssues
            },
            issues = issues.Select(i => new
            {
                type = i.Type.ToString(),
                file = i.FilePath,
                mb_recording_id = i.MusicBrainzRecordingId,
                reason = i.Reason,
                severity = i.Severity.ToString()
            })
        });
    }
}
