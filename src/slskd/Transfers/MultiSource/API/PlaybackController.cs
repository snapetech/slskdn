namespace slskd.Transfers.MultiSource.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Transfers.MultiSource.Playback;

    /// <summary>
    ///     Playback feedback API (experimental).
    /// </summary>
    [Route("api/v{version:apiVersion}/playback")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthPolicy.Any)]
    public class PlaybackController : ControllerBase
    {
        private readonly IPlaybackFeedbackService feedback;
        private readonly IPlaybackPriorityService priorities;

        public PlaybackController(IPlaybackFeedbackService feedback, IPlaybackPriorityService priorities)
        {
            this.feedback = feedback;
            this.priorities = priorities;
        }

        /// <summary>
        ///     Submit playback feedback for a job/track.
        /// </summary>
        [HttpPost("feedback")]
        public async Task<IActionResult> PostFeedback([FromBody] PlaybackFeedback payload, CancellationToken ct)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.JobId))
            {
                return BadRequest("jobId is required");
            }

            await feedback.RecordAsync(payload, ct).ConfigureAwait(false);
            var priority = priorities.GetPriority(payload.JobId);
            return Ok(new { priority });
        }

        /// <summary>
        ///     Gets current playback diagnostics for a job.
        /// </summary>
        [HttpGet("{jobId}/diagnostics")]
        public IActionResult GetDiagnostics(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest("jobId is required");
            }

            var fb = priorities.GetLatest(jobId);
            if (fb == null)
            {
                return NotFound();
            }

            var diag = new PlaybackDiagnostics
            {
                JobId = jobId,
                TrackId = fb.TrackId,
                PositionMs = fb.PositionMs,
                BufferAheadMs = fb.BufferAheadMs,
                Priority = priorities.GetPriority(jobId),
            };

            return Ok(diag);
        }
    }
}

















