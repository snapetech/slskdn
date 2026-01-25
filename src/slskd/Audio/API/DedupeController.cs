// <copyright file="DedupeController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Audio.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Core.Security;

    [ApiController]
    [Route("api/audio/variants/dedupe")]
    [AllowAnonymous] // PR-02: intended-public
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class DedupeController : ControllerBase
    {
        private readonly IDedupeService dedupeService;

        public DedupeController(IDedupeService dedupeService)
        {
            this.dedupeService = dedupeService;
        }

        /// <summary>
        ///     Returns deduplication groups for a recording using audio_sketch_hash and stream hashes.
        /// </summary>
        [HttpGet("{recordingId}")]
        public async Task<ActionResult<DedupeResult>> Get(string recordingId, CancellationToken ct)
        {
            var result = await dedupeService.GetDedupeAsync(recordingId, ct).ConfigureAwait(false);
            return Ok(result);
        }
    }
}
