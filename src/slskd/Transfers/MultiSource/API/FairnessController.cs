namespace slskd.Transfers.MultiSource.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Transfers.MultiSource.Metrics;

    /// <summary>
    ///     Fairness / contribution summary API.
    /// </summary>
    [Route("api/v{version:apiVersion}/fairness")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthPolicy.Any)]
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


