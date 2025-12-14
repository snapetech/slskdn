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

    [ApiController]
    [Route("api/jobs/discography")]
    [Authorize(Policy = AuthPolicy.Any)]
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
            if (request == null || string.IsNullOrWhiteSpace(request.ArtistId))
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
            var job = await jobService.GetJobAsync(jobId, ct).ConfigureAwait(false);
            if (job == null)
            {
                return NotFound();
            }

            return Ok(job);
        }
    }
}
