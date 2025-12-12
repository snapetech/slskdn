// <copyright file="CompositeModerationProvider.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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

namespace slskd.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Composite implementation of <see cref="IModerationProvider"/> that orchestrates multiple sub-providers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This provider delegates to configured sub-providers in order:
    ///         1. Hash blocklist (fastest, local)
    ///         2. External moderation client (opt-in, slower)
    ///         3. Falls back to Unknown if no providers flag the content
    ///     </para>
    ///     <para>
    ///         🔒 MANDATORY: Implements failsafe mode from `MCP-HARDENING.md` Section 5.
    ///         On provider failure, behavior depends on configuration:
    ///         - "block" mode: Block on error (conservative)
    ///         - "allow" mode: Continue to next provider (permissive)
    ///     </para>
    /// </remarks>
    public class CompositeModerationProvider : IModerationProvider
    {
        private readonly IOptionsMonitor<ModerationOptions> _options;
        private readonly ILogger<CompositeModerationProvider> _logger;
        private readonly IHashBlocklistChecker? _hashBlocklist;
        private readonly IPeerReputationStore? _peerReputation;
        private readonly IExternalModerationClient? _externalClient;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CompositeModerationProvider"/> class.
        /// </summary>
        public CompositeModerationProvider(
            IOptionsMonitor<ModerationOptions> options,
            ILogger<CompositeModerationProvider> logger,
            IHashBlocklistChecker? hashBlocklist = null,
            IPeerReputationStore? peerReputation = null,
            IExternalModerationClient? externalClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hashBlocklist = hashBlocklist;
            _peerReputation = peerReputation;
            _externalClient = externalClient;
        }

        /// <inheritdoc/>
        public async Task<ModerationDecision> CheckLocalFileAsync(
            LocalFileMetadata file,
            CancellationToken ct)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var opts = _options.CurrentValue;

            // 1. HASH BLOCKLIST (if enabled)
            if (_hashBlocklist != null && opts.HashBlocklist.Enabled)
            {
                try
                {
                    var isBlocked = await _hashBlocklist.IsBlockedHashAsync(file.PrimaryHash, ct);
                    if (isBlocked)
                    {
                        _logger.LogWarning(
                            "[SECURITY] MCP blocked file | InternalId={Id} | Reason=hash_blocklist",
                            file.Id);

                        return ModerationDecision.Block(
                            "hash_blocklist",
                            "provider:blocklist");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SECURITY] Hash blocklist check failed | Id={Id}", file.Id);

                    // FAILSAFE: Apply configured mode
                    if (opts.FailsafeMode == "block")
                    {
                        _logger.LogWarning("[SECURITY] Failsafe mode 'block' activated");
                        return ModerationDecision.Block("failsafe_block_on_error");
                    }
                    // Otherwise: Continue to next provider
                }
            }

            // 2. EXTERNAL MODERATION (if enabled)
            if (_externalClient != null && opts.ExternalModeration.Enabled)
            {
                try
                {
                    var decision = await _externalClient.AnalyzeFileAsync(file, ct);
                    if (decision.IsBlocking())
                    {
                        _logger.LogWarning(
                            "[SECURITY] MCP external moderation | InternalId={Id} | Verdict={Verdict}",
                            file.Id,
                            decision.Verdict);

                        return decision;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SECURITY] External moderation check failed | Id={Id}", file.Id);

                    // FAILSAFE: Apply configured mode
                    if (opts.FailsafeMode == "block")
                    {
                        _logger.LogWarning("[SECURITY] Failsafe mode 'block' activated");
                        return ModerationDecision.Block("failsafe_block_on_error");
                    }
                }
            }

            // 3. DEFAULT: No blockers found
            return ModerationDecision.Unknown("no_blockers_triggered");
        }

        /// <inheritdoc/>
        public async Task<ModerationDecision> CheckContentIdAsync(
            string contentId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                throw new ArgumentException("Content ID cannot be null or empty.", nameof(contentId));
            }

            // For now, return Unknown
            // Future: Map contentId to file metadata and check
            await Task.CompletedTask;
            return ModerationDecision.Unknown("content_id_check_not_implemented");
        }

        /// <inheritdoc/>
        public async Task ReportContentAsync(
            string contentId,
            ModerationReport report,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                throw new ArgumentException("Content ID cannot be null or empty.", nameof(contentId));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            // For now, just log (no-op)
            _logger.LogInformation(
                "[SECURITY] MCP content report | ContentId={ContentId} | Reason={Reason}",
                contentId,
                report.ReasonCode);

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task ReportPeerAsync(
            string peerId,
            PeerReport report,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(peerId))
            {
                throw new ArgumentException("Peer ID cannot be null or empty.", nameof(peerId));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            // Forward to reputation store (if configured)
            if (_peerReputation != null && _options.CurrentValue.Reputation.Enabled)
            {
                try
                {
                    await _peerReputation.RecordPeerEventAsync(peerId, report, ct);

                    _logger.LogInformation(
                        "[SECURITY] MCP peer report | PeerIdPrefix={PeerIdPrefix} | Reason={Reason}",
                        HashPeerId(peerId),
                        report.ReasonCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SECURITY] Failed to record peer event | PeerIdPrefix={PeerIdPrefix}",
                        HashPeerId(peerId));
                }
            }
        }

        /// <summary>
        ///     Hashes a peer ID for logging (privacy protection).
        /// </summary>
        /// <remarks>
        ///     🔒 Required by MCP-HARDENING.md Section 1.3.
        ///     Returns first 16 chars of SHA256 hash for correlation without exposure.
        /// </remarks>
        private string HashPeerId(string peerId)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(peerId);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).Substring(0, 16);
        }
    }

    /// <summary>
    ///     No-op implementation of <see cref="IModerationProvider"/> that always returns Unknown.
    /// </summary>
    /// <remarks>
    ///     Used when moderation is disabled via configuration.
    /// </remarks>
    public class NoopModerationProvider : IModerationProvider
    {
        /// <inheritdoc/>
        public Task<ModerationDecision> CheckLocalFileAsync(
            LocalFileMetadata file,
            CancellationToken ct)
        {
            return Task.FromResult(ModerationDecision.Unknown("moderation_disabled"));
        }

        /// <inheritdoc/>
        public Task<ModerationDecision> CheckContentIdAsync(
            string contentId,
            CancellationToken ct)
        {
            return Task.FromResult(ModerationDecision.Unknown("moderation_disabled"));
        }

        /// <inheritdoc/>
        public Task ReportContentAsync(
            string contentId,
            ModerationReport report,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task ReportPeerAsync(
            string peerId,
            PeerReport report,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}

