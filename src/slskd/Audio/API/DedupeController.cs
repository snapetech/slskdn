// <copyright file="DedupeController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Audio.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/audio/variants/dedupe")]
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
