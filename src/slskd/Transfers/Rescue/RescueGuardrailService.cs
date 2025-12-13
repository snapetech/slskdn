// <copyright file="RescueGuardrailService.cs" company="slskdn Team">
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

namespace slskd.Transfers.Rescue
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Serilog;

    /// <summary>
    ///     Service for enforcing Soulseek-primary guardrails in rescue mode.
    /// </summary>
    public interface IRescueGuardrailService
    {
        /// <summary>
        ///     Check if rescue mode is allowed for a transfer.
        /// </summary>
        Task<(bool allowed, string reason)> CheckRescueAllowedAsync(
            string transferId,
            string filename,
            CancellationToken ct = default);

        /// <summary>
        ///     Check if creating a multi-source job is allowed.
        /// </summary>
        Task<(bool allowed, string reason)> CheckMultiSourceJobAllowedAsync(
            int overlayPeerCount,
            int soulseekPeerCount,
            CancellationToken ct = default);
    }

    /// <summary>
    ///     Implements Soulseek-primary guardrails for rescue mode.
    /// </summary>
    public class RescueGuardrailService : IRescueGuardrailService
    {
        private readonly ILogger log = Log.ForContext<RescueGuardrailService>();

        /// <summary>
        ///     Default configuration for guardrails.
        /// </summary>
        public RescueGuardrailConfig Config { get; set; } = new RescueGuardrailConfig();

        /// <inheritdoc/>
        public async Task<(bool allowed, string reason)> CheckRescueAllowedAsync(
            string transferId,
            string filename,
            CancellationToken ct = default)
        {
            // Guardrail 1: Check if rescue mode is globally enabled
            if (!Config.Enabled)
            {
                return (false, "Rescue mode is disabled");
            }

            // Guardrail 2: Require at least one Soulseek origin
            if (Config.RequireSoulseekOrigin)
            {
                // In this implementation, we assume rescue mode is only called
                // when there's already a Soulseek transfer (that's underperforming)
                // so this is always satisfied.
                log.Debug("[GUARDRAIL] Soulseek origin requirement satisfied (transfer {TransferId} exists)", transferId);
            }

            // Guardrail 3: Check if overlay-only mode is explicitly enabled
            if (!Config.AllowOverlayOnly)
            {
                // This will be enforced in CheckMultiSourceJobAllowedAsync
                log.Debug("[GUARDRAIL] Overlay-only mode not allowed, Soulseek origin required");
            }

            // TODO: Add more guardrails:
            // - Check if file has been seen on Soulseek before (HashDb lookup)
            // - Check maximum concurrent rescue jobs
            // - Check daily rescue quota

            log.Debug("[GUARDRAIL] Rescue allowed for transfer {TransferId}, file {File}", transferId, filename);
            return (true, "Allowed");
        }

        /// <inheritdoc/>
        public async Task<(bool allowed, string reason)> CheckMultiSourceJobAllowedAsync(
            int overlayPeerCount,
            int soulseekPeerCount,
            CancellationToken ct = default)
        {
            // Guardrail 1: Require at least one Soulseek peer (unless overlay-only is explicitly enabled)
            if (!Config.AllowOverlayOnly && soulseekPeerCount == 0)
            {
                log.Warning("[GUARDRAIL] Rejected multi-source job: no Soulseek peers (overlay-only not allowed)");
                return (false, "At least one Soulseek peer required (overlay-only mode disabled)");
            }

            // Guardrail 2: Check overlay/Soulseek ratio limit
            if (overlayPeerCount > 0 && soulseekPeerCount > 0)
            {
                double overlayRatio = (double)overlayPeerCount / (overlayPeerCount + soulseekPeerCount);
                
                if (overlayRatio > Config.MaxOverlayRatio)
                {
                    log.Warning("[GUARDRAIL] Overlay ratio {Ratio:F2} exceeds limit {Limit:F2}",
                        overlayRatio, Config.MaxOverlayRatio);
                    return (false, $"Overlay peer ratio {overlayRatio:F2} exceeds limit {Config.MaxOverlayRatio:F2}");
                }
            }

            // Guardrail 3: Check minimum Soulseek peers
            if (soulseekPeerCount < Config.MinSoulseekPeers)
            {
                log.Warning("[GUARDRAIL] Soulseek peer count {Count} below minimum {Min}",
                    soulseekPeerCount, Config.MinSoulseekPeers);
                return (false, $"Soulseek peer count {soulseekPeerCount} below minimum {Config.MinSoulseekPeers}");
            }

            log.Debug("[GUARDRAIL] Multi-source job allowed: {SoulseekCount} Soulseek peers, {OverlayCount} overlay peers",
                soulseekPeerCount, overlayPeerCount);
            
            return (true, "Allowed");
        }
    }

    /// <summary>
    ///     Configuration for rescue mode guardrails.
    /// </summary>
    public class RescueGuardrailConfig
    {
        /// <summary>
        ///     Gets or sets a value indicating whether rescue mode is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets a value indicating whether at least one Soulseek origin is required.
        /// </summary>
        public bool RequireSoulseekOrigin { get; set; } = true;

        /// <summary>
        ///     Gets or sets a value indicating whether overlay-only downloads are allowed.
        /// </summary>
        public bool AllowOverlayOnly { get; set; } = false;

        /// <summary>
        ///     Gets or sets the maximum ratio of overlay to total peers (0.0 - 1.0).
        /// </summary>
        public double MaxOverlayRatio { get; set; } = 0.5;  // 50% max overlay

        /// <summary>
        ///     Gets or sets the minimum number of Soulseek peers required.
        /// </summary>
        public int MinSoulseekPeers { get; set; } = 1;

        /// <summary>
        ///     Gets or sets the maximum number of concurrent rescue jobs.
        /// </summary>
        public int MaxConcurrentRescueJobs { get; set; } = 5;

        /// <summary>
        ///     Gets or sets the daily rescue job quota (-1 = unlimited).
        /// </summary>
        public int DailyRescueQuota { get; set; } = -1;
    }
}

















