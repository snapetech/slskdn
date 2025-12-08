// <copyright file="DiscoveryController.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Transfers.MultiSource.Discovery.API
{
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Authentication;

    /// <summary>
    ///     Source discovery controller.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
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
            if (string.IsNullOrWhiteSpace(request?.SearchTerm))
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

            Log.Information("[Discovery API] Starting discovery for: {SearchTerm}", request.SearchTerm);

            await Discovery.StartDiscoveryAsync(
                request.SearchTerm,
                request.EnableHashVerification ?? true, // Default ON for FLAC testing
                HttpContext.RequestAborted);

            return Ok(new
            {
                message = "Discovery started",
                searchTerm = request.SearchTerm,
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
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return BadRequest("pattern query parameter is required");
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
        public string SearchTerm { get; set; }

        /// <summary>Gets or sets whether to enable FLAC hash verification (default true).</summary>
        public bool? EnableHashVerification { get; set; }
    }
}
