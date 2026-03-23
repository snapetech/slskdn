// <copyright file="DiscographyJobsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Jobs.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Integrations.MusicBrainz;
    using slskd.Core.Security;

    [ApiController]
    [Route("api/jobs/discography")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class DiscographyJobsController : ControllerBase
    {
        private readonly IDiscographyJobService jobService;

        public DiscographyJobsController(IDiscographyJobService jobService)
        {
            this.jobService = jobService;
        }

        /// <summary>
        ///     Creates a discography job for an artist.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> Create([FromBody] DiscographyJobRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                return BadRequest("artistId is required");
            }

            request.ArtistId = request.ArtistId?.Trim() ?? string.Empty;
            request.TargetDirectory = string.IsNullOrWhiteSpace(request.TargetDirectory) ? null : request.TargetDirectory.Trim();
            request.ReleaseIds = request.ReleaseIds?
                .Select(id => id?.Trim() ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrWhiteSpace(request.ArtistId))
            {
                return BadRequest("artistId is required");
            }

            var jobId = await jobService.CreateJobAsync(request, ct).ConfigureAwait(false);
            var job = await jobService.GetJobAsync(jobId, ct).ConfigureAwait(false);
            return Ok(job);
        }

        /// <summary>
        ///     Gets a discography job by ID.
        /// </summary>
        [HttpGet("{jobId}")]
        public async Task<ActionResult<object>> Get(string jobId, CancellationToken ct)
        {
            jobId = jobId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest("jobId is required");
            }

            var job = await jobService.GetJobAsync(jobId, ct).ConfigureAwait(false);
            if (job == null)
            {
                return NotFound();
            }

            return Ok(job);
        }
    }
}
