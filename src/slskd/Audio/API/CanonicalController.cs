// <copyright file="CanonicalController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Audio.API
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Core.Security;

    [ApiController]
    [Route("api/audio/canonical")]
    [AllowAnonymous] // PR-02: intended-public
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
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
