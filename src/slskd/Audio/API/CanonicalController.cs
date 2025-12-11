namespace slskd.Audio.API
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/audio/canonical")]
    public class CanonicalController : ControllerBase
    {
        private readonly ICanonicalStatsService canonicalStats;

        public CanonicalController(ICanonicalStatsService canonicalStats)
        {
            this.canonicalStats = canonicalStats;
        }

        /// <summary>
        /// Get canonical stats per codec profile for a recording (debugging).
        /// </summary>
        [HttpGet("{recordingId}")]
        public async Task<ActionResult<IEnumerable<CanonicalStats>>> Get(string recordingId, CancellationToken ct)
        {
            var candidates = await canonicalStats.GetCanonicalVariantCandidatesAsync(recordingId, ct).ConfigureAwait(false);
            return Ok(new { recordingId, candidates });
        }
    }
}


