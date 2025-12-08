// <copyright file="BackfillController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.Backfill.API
{
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;

    /// <summary>
    ///     Backfill Scheduler API controller.
    /// </summary>
    [Route("api/v{version:apiVersion}/backfill")]
    [ApiVersion("0")]
    [ApiController]
    public class BackfillController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="BackfillController"/> class.
        /// </summary>
        public BackfillController(IBackfillSchedulerService backfill)
        {
            Backfill = backfill;
        }

        private IBackfillSchedulerService Backfill { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<BackfillController>();

        /// <summary>
        ///     Gets backfill statistics.
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetStats()
        {
            return Ok(Backfill.Stats);
        }

        /// <summary>
        ///     Gets backfill configuration.
        /// </summary>
        [HttpGet("config")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetConfig()
        {
            return Ok(Backfill.Config);
        }

        /// <summary>
        ///     Gets backfill candidates.
        /// </summary>
        [HttpGet("candidates")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetCandidates([FromQuery] int limit = 10)
        {
            var candidates = await Backfill.GetCandidatesAsync(limit);
            return Ok(new
            {
                count = candidates.Count(),
                candidates,
            });
        }

        /// <summary>
        ///     Enables or disables the backfill scheduler.
        /// </summary>
        [HttpPost("enable")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult SetEnabled([FromQuery] bool enabled = true)
        {
            Backfill.SetEnabled(enabled);
            return Ok(new { enabled = Backfill.IsEnabled });
        }

        /// <summary>
        ///     Manually triggers a backfill cycle.
        /// </summary>
        [HttpPost("trigger")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> TriggerCycle()
        {
            var result = await Backfill.TriggerCycleAsync();
            return Ok(result);
        }

        /// <summary>
        ///     Manually backfills a specific file.
        /// </summary>
        [HttpPost("file")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> BackfillFile([FromBody] BackfillFileRequest request)
        {
            if (string.IsNullOrEmpty(request?.PeerId) || string.IsNullOrEmpty(request.Path) || request.Size <= 0)
            {
                return BadRequest(new { error = "peerId, path, and size are required" });
            }

            var result = await Backfill.BackfillFileAsync(request.PeerId, request.Path, request.Size);
            return Ok(result);
        }

        /// <summary>
        ///     Reports system as idle (for testing).
        /// </summary>
        [HttpPost("idle")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult ReportIdle()
        {
            Backfill.ReportIdle();
            return Ok(new { isIdle = Backfill.IsIdle });
        }

        /// <summary>
        ///     Reports system as busy (for testing).
        /// </summary>
        [HttpPost("busy")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult ReportBusy()
        {
            Backfill.ReportBusy();
            return Ok(new { isIdle = Backfill.IsIdle });
        }
    }

    /// <summary>
    ///     Request to backfill a specific file.
    /// </summary>
    public class BackfillFileRequest
    {
        /// <summary>Gets or sets the peer ID (username).</summary>
        public string PeerId { get; set; }

        /// <summary>Gets or sets the file path.</summary>
        public string Path { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }
    }
}


