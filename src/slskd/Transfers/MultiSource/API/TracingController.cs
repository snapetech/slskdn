namespace slskd.Transfers.MultiSource.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Transfers.MultiSource.Tracing;

    /// <summary>
    ///     API for swarm trace summaries.
    /// </summary>
    [Route("api/v{version:apiVersion}/traces")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthPolicy.Any)]
    public class TracingController : ControllerBase
    {
        private readonly ISwarmTraceSummarizer summarizer;

        public TracingController(ISwarmTraceSummarizer summarizer)
        {
            this.summarizer = summarizer;
        }

        /// <summary>
        ///     Gets a summary of swarm events for a job.
        /// </summary>
        [HttpGet("{jobId}/summary")]
        public async Task<IActionResult> GetSummary(string jobId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest("jobId is required");
            }

            var summary = await summarizer.SummarizeAsync(jobId, ct).ConfigureAwait(false);
            return Ok(summary);
        }
    }
}


