// <copyright file="ModerationDecision.cs" company="slskd Team">
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
    using System.Linq;

    /// <summary>
    ///     Represents a moderation decision for content or a peer.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A moderation decision includes:
    ///         - Verdict (Allowed, Blocked, Quarantined, Unknown)
    ///         - Reason code (e.g., "hash_blocklist", "external_moderation")
    ///         - Evidence keys (opaque identifiers, NOT raw hashes or paths)
    ///     </para>
    ///     <para>
    ///         🔒 SECURITY: Evidence keys MUST NOT contain:
    ///         - Raw content hashes
    ///         - Full filesystem paths
    ///         - External usernames or IP addresses
    ///     </para>
    ///     <para>
    ///         See `MCP-HARDENING.md` Section 1.4 for evidence key requirements.
    ///     </para>
    /// </remarks>
    public sealed class ModerationDecision
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ModerationDecision"/> class.
        /// </summary>
        public ModerationDecision()
        {
            Verdict = ModerationVerdict.Unknown;
            Reason = null;
            EvidenceKeys = Array.Empty<string>();
        }

        /// <summary>
        ///     Gets or initializes the verdict.
        /// </summary>
        public ModerationVerdict Verdict { get; init; }

        /// <summary>
        ///     Gets or initializes the reason code for the decision.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Common reason codes:
        ///         - "hash_blocklist" - Hash matched blocklist
        ///         - "external_moderation" - External service flagged
        ///         - "peer_banned" - Peer reputation triggered
        ///         - "failsafe_block_on_error" - Failsafe mode activated
        ///         - "no_blockers_triggered" - No providers found issues
        ///     </para>
        /// </remarks>
        public string? Reason { get; init; }

        /// <summary>
        ///     Gets or initializes the evidence keys.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Evidence keys are opaque identifiers for auditing and correlation.
        ///         Examples:
        ///         - "internal:guid-here" - Internal tracking ID
        ///         - "provider:blocklist-v1" - Provider identifier
        ///         - "external:trusted-moderator.com" - External service host
        ///     </para>
        ///     <para>
        ///         🔒 CRITICAL: NEVER include:
        ///         - Raw content hashes
        ///         - Full filesystem paths
        ///         - External usernames or IPs
        ///     </para>
        /// </remarks>
        public string[] EvidenceKeys { get; init; }

        /// <summary>
        ///     Creates a decision with Allowed verdict.
        /// </summary>
        /// <param name="reason">Optional reason for allowing.</param>
        /// <returns>A new <see cref="ModerationDecision"/> with Allowed verdict.</returns>
        public static ModerationDecision Allow(string? reason = null)
        {
            return new ModerationDecision
            {
                Verdict = ModerationVerdict.Allowed,
                Reason = reason ?? "no_blockers_triggered",
                EvidenceKeys = Array.Empty<string>()
            };
        }

        /// <summary>
        ///     Creates a decision with Blocked verdict.
        /// </summary>
        /// <param name="reason">Reason for blocking (required).</param>
        /// <param name="evidenceKeys">Evidence keys for auditing (optional).</param>
        /// <returns>A new <see cref="ModerationDecision"/> with Blocked verdict.</returns>
        public static ModerationDecision Block(string reason, params string[] evidenceKeys)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason required for Block decision.", nameof(reason));
            }

            return new ModerationDecision
            {
                Verdict = ModerationVerdict.Blocked,
                Reason = reason,
                EvidenceKeys = evidenceKeys ?? Array.Empty<string>()
            };
        }

        /// <summary>
        ///     Creates a decision with Quarantined verdict.
        /// </summary>
        /// <param name="reason">Reason for quarantine (required).</param>
        /// <param name="evidenceKeys">Evidence keys for auditing (optional).</param>
        /// <returns>A new <see cref="ModerationDecision"/> with Quarantined verdict.</returns>
        public static ModerationDecision Quarantine(string reason, params string[] evidenceKeys)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason required for Quarantine decision.", nameof(reason));
            }

            return new ModerationDecision
            {
                Verdict = ModerationVerdict.Quarantined,
                Reason = reason,
                EvidenceKeys = evidenceKeys ?? Array.Empty<string>()
            };
        }

        /// <summary>
        ///     Creates a decision with Unknown verdict.
        /// </summary>
        /// <param name="reason">Optional reason.</param>
        /// <returns>A new <see cref="ModerationDecision"/> with Unknown verdict.</returns>
        public static ModerationDecision Unknown(string? reason = null)
        {
            return new ModerationDecision
            {
                Verdict = ModerationVerdict.Unknown,
                Reason = reason,
                EvidenceKeys = Array.Empty<string>()
            };
        }

        /// <summary>
        ///     Returns true if the verdict blocks sharing/advertising.
        /// </summary>
        /// <returns>True if Blocked or Quarantined; otherwise false.</returns>
        public bool IsBlocking()
        {
            return Verdict == ModerationVerdict.Blocked ||
                   Verdict == ModerationVerdict.Quarantined;
        }

        /// <summary>
        ///     Returns true if the verdict allows sharing/advertising.
        /// </summary>
        /// <returns>True if Allowed; otherwise false.</returns>
        public bool IsAllowed()
        {
            return Verdict == ModerationVerdict.Allowed;
        }
    }
}

