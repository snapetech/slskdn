// <copyright file="LibraryHealthController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd;
using slskd.Core.Security;
using slskd.LibraryHealth;
using OptionsModel = slskd.Options;

/// <summary>
/// Provides slskdn-native library health API.
/// </summary>
[ApiController]
[Route("api/slskdn/library")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class LibraryHealthController : ControllerBase
{
    private readonly ILibraryHealthService healthService;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private readonly ILogger<LibraryHealthController> logger;

    public LibraryHealthController(
        ILibraryHealthService healthService,
        IOptionsMonitor<OptionsModel> optionsMonitor,
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
        path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (limit <= 0)
        {
            return BadRequest(new { error = "limit must be greater than 0" });
        }

        logger.LogInformation("Library health check requested for path: {Path}", path ?? "(all)");

        var summary = await healthService.GetSummaryAsync(path ?? string.Empty, cancellationToken);
        var issues = await healthService.GetIssuesAsync(
            new LibraryHealthIssueFilter
            {
                LibraryPath = path ?? string.Empty,
                Limit = limit,
            },
            cancellationToken);

        return Ok(new
        {
            path = path ?? "(all)",
            summary = new
            {
                total_issues = summary.TotalIssues,
                issues_open = summary.IssuesOpen,
                issues_resolved = summary.IssuesResolved
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

    /// <summary>
    /// Create a remediation job for library issues.
    /// </summary>
    [HttpPost("remediate")]
    [Authorize]
    public async Task<IActionResult> CreateRemediationJob(
        [FromBody] LibraryRemediationRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Remediation job requested for {IssueCount} issues", request?.IssueIds?.Count ?? 0);

        var issueIds = request?.IssueIds?
            .Where(issueId => !string.IsNullOrWhiteSpace(issueId))
            .Select(issueId => issueId.Trim())
            .ToList();

        if (issueIds == null || issueIds.Count == 0)
        {
            return BadRequest(new { error = "issue_ids is required" });
        }

        var jobId = await healthService.CreateRemediationJobAsync(issueIds, cancellationToken);

        return Ok(new { job_id = jobId });
    }
}

public record LibraryRemediationRequest([property: System.Text.Json.Serialization.JsonPropertyName("issue_ids")] List<string>? IssueIds);
