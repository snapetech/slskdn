// <copyright file="DiscoveryController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Transfers.MultiSource.Discovery.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Authentication;
    using slskd.Core.Security;

    /// <summary>
    ///     Source discovery controller.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class DiscoveryController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="DiscoveryController"/> class.
        /// </summary>
        /// <param name="discoveryService">The discovery service.</param>
        public DiscoveryController(ISourceDiscoveryService discoveryService)
        {
            Discovery = discoveryService;
        }

        private ISourceDiscoveryService Discovery { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<DiscoveryController>();

        /// <summary>
        ///     Gets the current discovery status and statistics.
        /// </summary>
        /// <returns>Discovery stats.</returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                isRunning = Discovery.IsRunning,
                currentSearchTerm = Discovery.CurrentSearchTerm,
                stats = Discovery.GetStats(),
            });
        }

        /// <summary>
        ///     Starts continuous source discovery for the specified search term.
        /// </summary>
        /// <param name="request">The discovery request.</param>
        /// <returns>Status.</returns>
        [HttpPost("start")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Start([FromBody] DiscoveryStartRequest request)
        {
            if (request == null)
            {
                return BadRequest("SearchTerm is required");
            }

            var normalizedSearchTerm = request.SearchTerm?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSearchTerm))
            {
                return BadRequest("SearchTerm is required");
            }

            if (Discovery.IsRunning)
            {
                return Conflict(new
                {
                    error = "Discovery already running",
                    currentSearchTerm = Discovery.CurrentSearchTerm,
                    hint = "Call /api/v0/discovery/stop first",
                });
            }

            Log.Information("[Discovery API] Starting discovery for: {SearchTerm}", normalizedSearchTerm);

            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            await Discovery.StartDiscoveryAsync(
                normalizedSearchTerm,
                request.EnableHashVerification ?? true, // Default ON for FLAC testing
                cancellationToken);

            return Ok(new
            {
                message = "Discovery started",
                searchTerm = normalizedSearchTerm,
                hashVerificationEnabled = request.EnableHashVerification ?? true,
            });
        }

        /// <summary>
        ///     Stops the current discovery process.
        /// </summary>
        /// <returns>Status.</returns>
        [HttpPost("stop")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Stop()
        {
            if (!Discovery.IsRunning)
            {
                return Ok(new { message = "Discovery not running" });
            }

            await Discovery.StopDiscoveryAsync();

            return Ok(new
            {
                message = "Discovery stopped",
                stats = Discovery.GetStats(),
            });
        }

        /// <summary>
        ///     Gets discovered sources for a specific file size.
        /// </summary>
        /// <param name="size">The file size in bytes.</param>
        /// <param name="limit">Maximum results (default 100).</param>
        /// <returns>List of sources.</returns>
        [HttpGet("sources/by-size/{size}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetSourcesBySize(long size, [FromQuery] int limit = 100)
        {
            if (size <= 0)
            {
                return BadRequest("size must be greater than zero");
            }

            if (limit <= 0)
            {
                return BadRequest("limit must be greater than zero");
            }

            var sources = Discovery.GetSourcesBySize(size, limit);
            return Ok(new
            {
                size,
                sourceCount = sources.Count,
                sources,
            });
        }

        /// <summary>
        ///     Gets discovered sources matching a filename pattern.
        /// </summary>
        /// <param name="pattern">The filename pattern.</param>
        /// <param name="limit">Maximum results (default 100).</param>
        /// <returns>List of sources.</returns>
        [HttpGet("sources/by-filename")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetSourcesByFilename([FromQuery] string pattern, [FromQuery] int limit = 100)
        {
            pattern = pattern?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return BadRequest("pattern query parameter is required");
            }

            if (limit <= 0)
            {
                return BadRequest("limit must be greater than zero");
            }

            var sources = Discovery.GetSourcesByFilename(pattern, limit);
            return Ok(new
            {
                pattern,
                sourceCount = sources.Count,
                sources,
            });
        }

        /// <summary>
        ///     Gets file size summaries with source counts.
        /// </summary>
        /// <param name="minSources">Minimum sources to include (default 2).</param>
        /// <returns>List of file size summaries.</returns>
        [HttpGet("summaries")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetSummaries([FromQuery] int minSources = 2)
        {
            if (minSources <= 0)
            {
                return BadRequest("minSources must be greater than zero");
            }

            var summaries = Discovery.GetFileSizeSummaries(minSources);
            return Ok(new
            {
                minSources,
                count = summaries.Count,
                summaries,
            });
        }

        /// <summary>
        ///     Gets the count of users flagged as not supporting partial downloads.
        /// </summary>
        /// <returns>Count of flagged users.</returns>
        [HttpGet("no-partial-count")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetNoPartialCount()
        {
            var count = Discovery.GetNoPartialSupportCount();
            return Ok(new
            {
                usersWithoutPartialSupport = count,
                message = $"{count} users are flagged as not supporting partial/chunked downloads",
            });
        }

        /// <summary>
        ///     Resets all partial support flags (gives everyone another chance).
        /// </summary>
        /// <returns>Confirmation.</returns>
        [HttpPost("reset-partial-flags")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult ResetPartialFlags()
        {
            var beforeCount = Discovery.GetNoPartialSupportCount();
            Discovery.ResetPartialSupportFlags();
            return Ok(new
            {
                message = $"Reset partial support flags for {beforeCount} users. They will be tried again on next swarm.",
            });
        }
    }

    /// <summary>
    ///     Request to start discovery.
    /// </summary>
    public class DiscoveryStartRequest
    {
        /// <summary>Gets or sets the search term (e.g., artist name).</summary>
        public string SearchTerm { get; set; } = string.Empty;

        /// <summary>Gets or sets whether to enable FLAC hash verification (default true).</summary>
        public bool? EnableHashVerification { get; set; }
    }
}
