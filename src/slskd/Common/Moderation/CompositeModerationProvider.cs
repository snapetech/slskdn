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
    using slskd.Shares;

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
        private readonly IShareRepository? _shareRepository; // T-MCP03: For looking up content items
        private readonly IModerationProvider? _llmModerationProvider; // T-MCP-LM02: LLM moderation provider

        /// <summary>
        ///     Initializes a new instance of the <see cref="CompositeModerationProvider"/> class.
        /// </summary>
        public CompositeModerationProvider(
            IOptionsMonitor<ModerationOptions> options,
            ILogger<CompositeModerationProvider> logger,
            IHashBlocklistChecker? hashBlocklist = null,
            IPeerReputationStore? peerReputation = null,
            IExternalModerationClient? externalClient = null,
            IShareRepository? shareRepository = null, // T-MCP03: Optional injection
            IModerationProvider? llmModerationProvider = null) // T-MCP-LM02: Optional LLM provider
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hashBlocklist = hashBlocklist;
            _peerReputation = peerReputation;
            _externalClient = externalClient;
            _shareRepository = shareRepository; // T-MCP03
            _llmModerationProvider = llmModerationProvider; // T-MCP-LM02
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

            // 3. LLM MODERATION (T-MCP-LM02)
            if (_llmModerationProvider != null && opts.LlmModeration.Enabled)
            {
                try
                {
                    _logger.LogDebug("[MCP] Calling LLM moderation provider for file | Id={Id}", file.Id);

                    var llmDecision = await _llmModerationProvider.CheckLocalFileAsync(file, ct);

                    // Only override with LLM decision if it's definitive (not Unknown)
                    if (llmDecision.Verdict != ModerationVerdict.Unknown)
                    {
                        if (llmDecision.IsBlocking())
                        {
                            _logger.LogWarning(
                                "[SECURITY] MCP LLM moderation blocked | InternalId={Id} | Reason={Reason}",
                                file.Id,
                                llmDecision.Reason);

                            return llmDecision;
                        }

                        // For allowed content, log but continue (LLM is not authoritative for allowing)
                        _logger.LogDebug(
                            "[MCP] LLM moderation allowed | InternalId={Id} | Reason={Reason}",
                            file.Id,
                            llmDecision.Reason);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "[MCP] LLM moderation inconclusive, continuing to next provider | InternalId={Id}",
                            file.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SECURITY] LLM moderation provider failed | Id={Id}", file.Id);

                    // FAILSAFE: Apply configured fallback behavior
                    var fallback = opts.LlmModeration.FallbackBehavior;
                    if (fallback == "block")
                    {
                        _logger.LogWarning("[SECURITY] LLM failsafe mode 'block' activated for file | Id={Id}", file.Id);
                        return ModerationDecision.Block("llm_provider_failed_failsafe_block");
                    }
                    // For "allow" or "pass_to_next_provider", continue to default
                }
            }

            // 4. DEFAULT: No blockers found
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

            // T-MCP03: Implement content ID checking
            // Strategy:
            // 1. Look up content item in database (if available)
            // 2. If found and already checked, return cached decision
            // 3. If found but stale or new, look up associated file and run CheckLocalFileAsync
            // 4. If not found, return Unknown (content not yet mapped)

            if (_shareRepository == null)
            {
                _logger.LogDebug("[MCP] CheckContentIdAsync called but no share repository available");
                return ModerationDecision.Unknown("no_share_repository");
            }

            try
            {
                var contentItem = _shareRepository.FindContentItem(contentId);

                if (contentItem == null)
                {
                    // Content ID not yet mapped to a file
                    _logger.LogDebug("[MCP] Content ID not found | ContentId={ContentId}", contentId);
                    return ModerationDecision.Unknown("content_not_mapped");
                }

                // Content item exists; check if decision is still valid
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var ageSeconds = now - contentItem.Value.CheckedAt;

                // If checked recently (within 1 hour) and not advertisable, return cached decision
                if (ageSeconds < 3600 && !contentItem.Value.IsAdvertisable)
                {
                    _logger.LogDebug(
                        "[MCP] Returning cached non-advertisable decision | ContentId={ContentId} | Reason={Reason}",
                        contentId,
                        contentItem.Value.ModerationReason);

                    return contentItem.Value.ModerationReason != null
                        ? ModerationDecision.Block(contentItem.Value.ModerationReason, "cached_decision")
                        : ModerationDecision.Quarantine(contentItem.Value.ModerationReason ?? "cached_quarantine", "cached_decision");
                }

                // If checked recently and advertisable, return Allowed
                if (ageSeconds < 3600 && contentItem.Value.IsAdvertisable)
                {
                    _logger.LogDebug("[MCP] Returning cached advertisable decision | ContentId={ContentId}", contentId);
                    return ModerationDecision.Allow("cached_allowed");
                }

                // Decision is stale or needs re-check; look up the file
                var (filename, size) = _shareRepository.FindFileInfo(contentItem.Value.MaskedFilename);

                if (string.IsNullOrEmpty(filename))
                {
                    _logger.LogWarning(
                        "[MCP] Content item references missing file | ContentId={ContentId} | File={File}",
                        contentId,
                        contentItem.Value.MaskedFilename);

                    return ModerationDecision.Unknown("file_not_found");
                }

                // Build LocalFileMetadata and check via CheckLocalFileAsync
                // Note: We don't have full metadata here, so we'll use what we have
                var fileMetadata = new LocalFileMetadata
                {
                    Id = contentItem.Value.MaskedFilename,
                    SizeBytes = size,
                    PrimaryHash = null, // Not available from FindFileInfo
                    MediaInfo = System.IO.Path.GetExtension(filename),
                };

                // Re-check the file
                var decision = await CheckLocalFileAsync(fileMetadata, ct);

                // Update content item with new decision
                var isAdvertisable = decision.Verdict == ModerationVerdict.Allowed;
                _shareRepository.UpsertContentItem(
                    contentId,
                    contentItem.Value.Domain,
                    contentItem.Value.WorkId,
                    contentItem.Value.MaskedFilename,
                    isAdvertisable,
                    decision.Reason,
                    now);

                _logger.LogInformation(
                    "[MCP] Content ID checked | ContentId={ContentId} | IsAdvertisable={IsAdvertisable} | Verdict={Verdict}",
                    contentId,
                    isAdvertisable,
                    decision.Verdict);

                return decision;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MCP] Failed to check content ID | ContentId={ContentId}", contentId);

                // Failsafe: On error, deny advertisement
                return ModerationDecision.Block("check_failed", "failsafe");
            }
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
                    var eventType = report.Severity switch
                    {
                        ReportSeverity.Critical => PeerReputationEventType.MaliciousBehavior,
                        ReportSeverity.High => PeerReputationEventType.PolicyViolation,
                        ReportSeverity.Medium => PeerReputationEventType.SuspiciousActivity,
                        _ => PeerReputationEventType.Other
                    };

                    var reputationEvent = new PeerReputationEvent(
                        peerId,
                        eventType,
                        contentId: null,
                        timestamp: DateTimeOffset.UtcNow,
                        metadata: report.Reason);

                    await _peerReputation.RecordPeerEventAsync(reputationEvent, ct);

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

