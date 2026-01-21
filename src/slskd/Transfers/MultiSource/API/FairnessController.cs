// <copyright file="FairnessController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Transfers.MultiSource.Metrics;
    using slskd.Core.Security;

    /// <summary>
    ///     Fairness / contribution summary API.
    /// </summary>
    [Route("api/v{version:apiVersion}/fairness")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class FairnessController : ControllerBase
    {
        private readonly IFairnessGuard fairness;

        public FairnessController(IFairnessGuard fairness)
        {
            this.fairness = fairness;
        }

        /// <summary>
        ///     Returns a contribution summary and fairness decision.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken ct)
        {
            var decision = await fairness.EvaluateAsync(ct).ConfigureAwait(false);

            return Ok(new
            {
                throttleOverlayDownloads = decision.ThrottleOverlayDownloads,
                decision.Reason,
                decision.OverlayUploadDownloadRatio,
                decision.OverlayToSoulseekUploadRatio,
                totals = new
                {
                    decision.Totals.OverlayUploadBytes,
                    decision.Totals.OverlayDownloadBytes,
                    decision.Totals.SoulseekUploadBytes,
                    decision.Totals.SoulseekDownloadBytes,
                },
            });
        }
    }
}
