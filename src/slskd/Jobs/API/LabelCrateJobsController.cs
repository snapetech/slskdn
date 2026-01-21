// <copyright file="LabelCrateJobsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Jobs.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Core.Security;

    [ApiController]
    [Route("api/jobs/label-crate")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class LabelCrateJobsController : ControllerBase
    {
        private readonly ILabelCrateJobService jobService;

        public LabelCrateJobsController(ILabelCrateJobService jobService)
        {
            this.jobService = jobService;
        }

        /// <summary>
        ///     Creates a label crate job.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> Create([FromBody] LabelCrateJobRequest request, CancellationToken ct)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.LabelId) && string.IsNullOrWhiteSpace(request.LabelName)))
            {
                return BadRequest("labelId or labelName is required");
            }

            var jobId = await jobService.CreateJobAsync(request, ct).ConfigureAwait(false);
            var job = await jobService.GetJobAsync(jobId, ct).ConfigureAwait(false);
            return Ok(job);
        }

        /// <summary>
        ///     Gets a label crate job by ID.
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
