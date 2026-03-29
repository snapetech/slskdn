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
            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

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
            if (usernames == null || usernames.Count == 0)
            {
                return BadRequest("At least one username is required");
            }

            var normalizedUsernames = usernames
                .Select(username => username?.Trim() ?? string.Empty)
                .Where(username => !string.IsNullOrWhiteSpace(username))
                .Distinct(System.StringComparer.Ordinal)
                .ToList();

            if (normalizedUsernames.Count == 0)
            {
                return BadRequest("Each username must be non-empty");
            }

            var histories = await rankingService.GetHistoriesAsync(normalizedUsernames);
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
            if (candidates == null || candidates.Count == 0)
            {
                return BadRequest("At least one source candidate is required");
            }

            var normalizedCandidates = candidates
                .Where(candidate => candidate != null)
                .Select(candidate => new SourceCandidate
                {
                    Username = candidate.Username?.Trim() ?? string.Empty,
                    Filename = candidate.Filename?.Trim() ?? string.Empty,
                    Size = candidate.Size,
                    HasFreeUploadSlot = candidate.HasFreeUploadSlot,
                    QueueLength = candidate.QueueLength,
                    UploadSpeed = candidate.UploadSpeed,
                    SizeDiffPercent = candidate.SizeDiffPercent
                })
                .DistinctBy(candidate => (candidate.Username, candidate.Filename, candidate.Size))
                .ToList();

            if (normalizedCandidates.Count == 0 || normalizedCandidates.Any(candidate =>
                    string.IsNullOrWhiteSpace(candidate.Username) ||
                    string.IsNullOrWhiteSpace(candidate.Filename)))
            {
                return BadRequest("Each source candidate requires a non-empty username and filename");
            }

            var ranked = await rankingService.RankSourcesAsync(normalizedCandidates);
            return Ok(ranked);
        }
    }
}
