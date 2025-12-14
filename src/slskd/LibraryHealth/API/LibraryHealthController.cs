// <copyright file="LibraryHealthController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.LibraryHealth.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// API controller for Library Health (Collection Doctor).
    /// </summary>
    [ApiController]
    [Route("api/library/health")]
    [Produces("application/json")]
    public class LibraryHealthController : ControllerBase
    {
        private readonly ILibraryHealthService libraryHealth;
        private readonly ILogger<LibraryHealthController> log;

        public LibraryHealthController(
            ILibraryHealthService libraryHealth,
            ILogger<LibraryHealthController> log)
        {
            this.libraryHealth = libraryHealth;
            this.log = log;
        }

        /// <summary>
        /// Start a library health scan.
        /// </summary>
        /// <param name="request">Scan request parameters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Scan ID.</returns>
        [HttpPost("scans")]
        [Authorize]
        public async Task<ActionResult<StartScanResponse>> StartScan(
            [FromBody] LibraryHealthScanRequest request,
            CancellationToken ct)
        {
            log.LogInformation("Starting library health scan for path: {Path}", request.LibraryPath);

            var scanId = await libraryHealth.StartScanAsync(request, ct);

            return Ok(new StartScanResponse
            {
                ScanId = scanId,
                Message = "Scan started successfully"
            });
        }

        /// <summary>
        /// Get the status of a library health scan.
        /// </summary>
        /// <param name="scanId">Scan identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Scan status.</returns>
        [HttpGet("scans/{scanId}")]
        [Authorize]
        public async Task<ActionResult<LibraryHealthScan>> GetScanStatus(
            string scanId,
            CancellationToken ct)
        {
            var scan = await libraryHealth.GetScanStatusAsync(scanId, ct);

            if (scan == null)
            {
                return NotFound(new { message = $"Scan {scanId} not found" });
            }

            return Ok(scan);
        }

        /// <summary>
        /// Get library health summary for a given path.
        /// </summary>
        /// <param name="libraryPath">Path to scan (query parameter).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Health summary.</returns>
        [HttpGet("summary")]
        [Authorize]
        public async Task<ActionResult<LibraryHealthSummary>> GetSummary(
            [FromQuery] string libraryPath,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                return BadRequest(new { message = "libraryPath query parameter is required" });
            }

            log.LogInformation("Getting library health summary for path: {Path}", libraryPath);

            var summary = await libraryHealth.GetSummaryAsync(libraryPath, ct);

            return Ok(summary);
        }

        /// <summary>
        /// Get library health issues with optional filtering.
        /// </summary>
        /// <param name="filter">Filter parameters (from query string).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of issues.</returns>
        [HttpGet("issues")]
        [Authorize]
        public async Task<ActionResult<IssuesResponse>> GetIssues(
            [FromQuery] LibraryHealthIssueFilter filter,
            CancellationToken ct)
        {
            log.LogInformation(
                "Getting library health issues: Types={Types}, Severities={Severities}, Statuses={Statuses}, Limit={Limit}",
                filter.Types != null ? string.Join(",", filter.Types) : "all",
                filter.Severities != null ? string.Join(",", filter.Severities) : "all",
                filter.Statuses != null ? string.Join(",", filter.Statuses) : "all",
                filter.Limit);

            var issues = await libraryHealth.GetIssuesAsync(filter, ct);

            return Ok(new IssuesResponse
            {
                Issues = issues,
                TotalCount = issues.Count,
                Filter = filter
            });
        }

        /// <summary>
        /// Get issues grouped by type.
        /// </summary>
        /// <param name="libraryPath">Path to filter by (optional).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Issues grouped by type with counts.</returns>
        [HttpGet("issues/by-type")]
        [Authorize]
        public async Task<ActionResult<IssuesByTypeResponse>> GetIssuesByType(
            [FromQuery] string libraryPath,
            CancellationToken ct)
        {
            var filter = new LibraryHealthIssueFilter { LibraryPath = libraryPath };
            var issues = await libraryHealth.GetIssuesAsync(filter, ct);

            var grouped = issues
                .GroupBy(i => i.Type)
                .Select(g => new IssueTypeGroup
                {
                    Type = g.Key,
                    Count = g.Count(),
                    BySeverity = g.GroupBy(i => i.Severity)
                        .ToDictionary(sg => sg.Key, sg => sg.Count())
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            return Ok(new IssuesByTypeResponse
            {
                Groups = grouped,
                TotalIssues = issues.Count
            });
        }

        /// <summary>
        /// Get issues grouped by artist.
        /// </summary>
        /// <param name="limit">Maximum number of artists to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Issues grouped by artist.</returns>
        [HttpGet("issues/by-artist")]
        [Authorize]
        public async Task<ActionResult<IssuesByArtistResponse>> GetIssuesByArtist(
            [FromQuery] int limit = 20,
            CancellationToken ct = default)
        {
            var filter = new LibraryHealthIssueFilter();
            var issues = await libraryHealth.GetIssuesAsync(filter, ct);

            var grouped = issues
                .Where(i => !string.IsNullOrWhiteSpace(i.Artist))
                .GroupBy(i => i.Artist)
                .Select(g => new IssueArtistGroup
                {
                    Artist = g.Key,
                    Count = g.Count(),
                    ByType = g.GroupBy(i => i.Type)
                        .ToDictionary(tg => tg.Key, tg => tg.Count())
                })
                .OrderByDescending(g => g.Count)
                .Take(limit)
                .ToList();

            return Ok(new IssuesByArtistResponse
            {
                Groups = grouped,
                TotalArtists = grouped.Count
            });
        }

        /// <summary>
        /// Get issues grouped by release.
        /// </summary>
        /// <param name="limit">Maximum number of releases to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Issues grouped by release.</returns>
        [HttpGet("issues/by-release")]
        [Authorize]
        public async Task<ActionResult<IssuesByReleaseResponse>> GetIssuesByRelease(
            [FromQuery] int limit = 20,
            CancellationToken ct = default)
        {
            var filter = new LibraryHealthIssueFilter();
            var issues = await libraryHealth.GetIssuesAsync(filter, ct);

            var grouped = issues
                .Where(i => !string.IsNullOrWhiteSpace(i.Album))
                .GroupBy(i => new { i.Artist, i.Album, i.MusicBrainzReleaseId })
                .Select(g => new IssueReleaseGroup
                {
                    Artist = g.Key.Artist,
                    Album = g.Key.Album,
                    MusicBrainzReleaseId = g.Key.MusicBrainzReleaseId,
                    Count = g.Count(),
                    ByType = g.GroupBy(i => i.Type)
                        .ToDictionary(tg => tg.Key, tg => tg.Count())
                })
                .OrderByDescending(g => g.Count)
                .Take(limit)
                .ToList();

            return Ok(new IssuesByReleaseResponse
            {
                Groups = grouped,
                TotalReleases = grouped.Count
            });
        }

        /// <summary>
        /// Get issues grouped by codec (using issue metadata when available).
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Issues grouped by codec.</returns>
        [HttpGet("issues/by-codec")]
        [Authorize]
        public async Task<ActionResult<object>> GetIssuesByCodec(CancellationToken ct = default)
        {
            var filter = new LibraryHealthIssueFilter();
            var issues = await libraryHealth.GetIssuesAsync(filter, ct);

            var grouped = issues
                .Select(i =>
                {
                    i.Metadata.TryGetValue("codec", out var codecObj);
                    var codec = (codecObj as string)?.ToUpperInvariant() ?? "UNKNOWN";
                    var transcode = i.Type == LibraryIssueType.SuspectedTranscode;
                    if (!transcode && i.Metadata.TryGetValue("transcode_suspect", out var suspectObj) && suspectObj is bool b)
                    {
                        transcode = b;
                    }

                    return (codec, transcode);
                })
                .GroupBy(x => x.codec)
                .Select(g => new IssueCodecGroup
                {
                    Codec = g.Key,
                    Count = g.Count(),
                    TranscodeSuspect = g.Count(x => x.transcode),
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            return Ok(new
            {
                Groups = grouped,
                TotalIssues = issues.Count,
            });
        }

        /// <summary>
        /// Update the status of a library health issue.
        /// </summary>
        /// <param name="issueId">Issue identifier.</param>
        /// <param name="request">Status update request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>No content on success.</returns>
        [HttpPatch("issues/{issueId}")]
        [Authorize]
        public async Task<IActionResult> UpdateIssueStatus(
            string issueId,
            [FromBody] UpdateIssueStatusRequest request,
            CancellationToken ct)
        {
            log.LogInformation("Updating issue {IssueId} status to {Status}", issueId, request.Status);

            await libraryHealth.UpdateIssueStatusAsync(issueId, request.Status, ct);

            return NoContent();
        }

        /// <summary>
        /// Create a remediation job for one or more issues.
        /// </summary>
        /// <param name="request">Remediation request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Job ID.</returns>
        [HttpPost("issues/fix")]
        [Authorize]
        public async Task<ActionResult<RemediationResponse>> CreateRemediationJob(
            [FromBody] RemediationRequest request,
            CancellationToken ct)
        {
            log.LogInformation("Creating remediation job for {Count} issues", request.IssueIds.Count);

            var jobId = await libraryHealth.CreateRemediationJobAsync(request.IssueIds, ct);

            return Ok(new RemediationResponse
            {
                JobId = jobId,
                Message = $"Remediation job created for {request.IssueIds.Count} issue(s)"
            });
        }
    }

    // Response DTOs
    public class StartScanResponse
    {
        public string ScanId { get; set; }
        public string Message { get; set; }
    }

    public class IssuesResponse
    {
        public List<LibraryIssue> Issues { get; set; }
        public int TotalCount { get; set; }
        public LibraryHealthIssueFilter Filter { get; set; }
    }

    public class IssuesByTypeResponse
    {
        public List<IssueTypeGroup> Groups { get; set; }
        public int TotalIssues { get; set; }
    }

    public class IssueTypeGroup
    {
        public LibraryIssueType Type { get; set; }
        public int Count { get; set; }
        public Dictionary<LibraryIssueSeverity, int> BySeverity { get; set; }
    }

    public class IssuesByArtistResponse
    {
        public List<IssueArtistGroup> Groups { get; set; }
        public int TotalArtists { get; set; }
    }

    public class IssueArtistGroup
    {
        public string Artist { get; set; }
        public int Count { get; set; }
        public Dictionary<LibraryIssueType, int> ByType { get; set; }
    }

    public class IssuesByReleaseResponse
    {
        public List<IssueReleaseGroup> Groups { get; set; }
        public int TotalReleases { get; set; }
    }

    public class IssueReleaseGroup
    {
        public string Artist { get; set; }
        public string Album { get; set; }
        public string MusicBrainzReleaseId { get; set; }
        public int Count { get; set; }
        public Dictionary<LibraryIssueType, int> ByType { get; set; }
    }

    public class UpdateIssueStatusRequest
    {
        public LibraryIssueStatus Status { get; set; }
    }

    public class RemediationRequest
    {
        public List<string> IssueIds { get; set; }
    }

    public class RemediationResponse
    {
        public string JobId { get; set; }
        public string Message { get; set; }
    }
}
