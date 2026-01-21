// <copyright file="RankingController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the GNU Affero General Public License v3.0.
// </copyright>

namespace slskd.Transfers.Ranking.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Core.Security;

    /// <summary>
    ///     Ranking and download history API.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class RankingController : ControllerBase
    {
        private readonly ISourceRankingService rankingService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RankingController"/> class.
        /// </summary>
        /// <param name="rankingService">The ranking service.</param>
        public RankingController(ISourceRankingService rankingService)
        {
            this.rankingService = rankingService;
        }

        /// <summary>
        ///     Gets download history for a specific user.
        /// </summary>
        /// <param name="username">The username to get history for.</param>
        /// <returns>The user's download history.</returns>
        [HttpGet("history/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(UserDownloadHistory), 200)]
        public async Task<IActionResult> GetHistory([FromRoute] string username)
        {
            var history = await rankingService.GetHistoryAsync(username);
            return Ok(history);
        }

        /// <summary>
        ///     Gets download history for multiple users.
        /// </summary>
        /// <param name="usernames">The usernames to get history for.</param>
        /// <returns>A dictionary of username to download history.</returns>
        [HttpPost("history")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IDictionary<string, UserDownloadHistory>), 200)]
        public async Task<IActionResult> GetHistories([FromBody] List<string> usernames)
        {
            var histories = await rankingService.GetHistoriesAsync(usernames);
            return Ok(histories);
        }

        /// <summary>
        ///     Ranks a list of source candidates using smart scoring.
        /// </summary>
        /// <param name="candidates">The candidates to rank.</param>
        /// <returns>The ranked candidates, best first.</returns>
        [HttpPost("rank")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<RankedSource>), 200)]
        public async Task<IActionResult> RankSources([FromBody] List<SourceCandidate> candidates)
        {
            var ranked = await rankingService.RankSourcesAsync(candidates);
            return Ok(ranked);
        }
    }
}
