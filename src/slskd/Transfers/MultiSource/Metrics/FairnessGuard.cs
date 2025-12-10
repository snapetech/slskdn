namespace slskd.Transfers.MultiSource.Metrics
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.HashDb.Models;

    public interface IFairnessGuard
    {
        Task<FairnessDecision> EvaluateAsync(CancellationToken ct = default);
    }

    public sealed class FairnessDecision
    {
        public bool Allowed => !ThrottleOverlayDownloads;

        public bool ThrottleOverlayDownloads { get; init; }

        public string Reason { get; init; }

        public double OverlayUploadDownloadRatio { get; init; }

        public double OverlayToSoulseekUploadRatio { get; init; }

        public TrafficTotals Totals { get; init; }
    }

    /// <summary>
    ///     Enforces fairness constraints between overlay and Soulseek traffic.
    /// </summary>
    public class FairnessGuard : IFairnessGuard
    {
        private readonly ITrafficAccountingService accounting;
        private readonly FairnessConfig config;

        public FairnessGuard(
            ITrafficAccountingService accounting,
            FairnessConfig? config = null)
        {
            this.accounting = accounting;
            this.config = config ?? new FairnessConfig();
        }

        public async Task<FairnessDecision> EvaluateAsync(CancellationToken ct = default)
        {
            var opts = config;
            if (opts.Enable == false)
            {
                return new FairnessDecision
                {
                    ThrottleOverlayDownloads = false,
                    Reason = "Fairness disabled",
                    OverlayUploadDownloadRatio = 1.0,
                    OverlayToSoulseekUploadRatio = 1.0,
                    Totals = new TrafficTotals(),
                };
            }

            var totals = await accounting.GetTotalsAsync(ct).ConfigureAwait(false);

            double overlayDownload = totals.OverlayDownloadBytes;
            double overlayUpload = totals.OverlayUploadBytes;
            double soulseekUpload = totals.SoulseekUploadBytes;

            double uploadDownloadRatio = overlayDownload > 0
                ? overlayUpload / overlayDownload
                : 1.0; // neutral if no overlay downloads yet

            double overlayToSoulseekUploadRatio = soulseekUpload > 0
                ? overlayUpload / soulseekUpload
                : overlayUpload > 0 ? double.PositiveInfinity : 0.0;

            bool throttle = false;
            string reason = string.Empty;

            if (uploadDownloadRatio < opts.MinOverlayUploadDownloadRatio)
            {
                throttle = true;
                reason = $"overlay upload/download ratio {uploadDownloadRatio:F2} below minimum {opts.MinOverlayUploadDownloadRatio:F2}";
            }

            if (overlayToSoulseekUploadRatio > opts.MaxOverlayToSoulseekUploadRatio)
            {
                throttle = true;
                var more = $"overlay/Soulseek upload ratio {overlayToSoulseekUploadRatio:F2} above maximum {opts.MaxOverlayToSoulseekUploadRatio:F2}";
                reason = string.IsNullOrEmpty(reason) ? more : $"{reason}; {more}";
            }

            return new FairnessDecision
            {
                ThrottleOverlayDownloads = throttle,
                Reason = string.IsNullOrEmpty(reason) ? "within fairness constraints" : reason,
                OverlayUploadDownloadRatio = uploadDownloadRatio,
                OverlayToSoulseekUploadRatio = overlayToSoulseekUploadRatio,
                Totals = totals,
            };
        }
    }

    public class FairnessConfig
    {
        /// <summary>
        ///     Enable/disable fairness guard.
        /// </summary>
        public bool Enable { get; init; } = true;

        /// <summary>
        ///     Minimum ratio overlay_upload / overlay_download.
        /// </summary>
        public double MinOverlayUploadDownloadRatio { get; init; } = 0.5;

        /// <summary>
        ///     Maximum ratio overlay_upload / Soulseek_upload.
        /// </summary>
        public double MaxOverlayToSoulseekUploadRatio { get; init; } = 3.0;
    }
}

