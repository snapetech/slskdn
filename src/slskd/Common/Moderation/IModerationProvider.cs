// <copyright file="IModerationProvider.cs" company="slskd Team">
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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Primary interface for the Moderation / Control Plane (MCP).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         MCP provides centralized moderation decisions for:
    ///         - Local files (before indexing/sharing)
    ///         - Content IDs (before advertising/serving)
    ///         - Peers (reputation and banning)
    ///     </para>
    ///     <para>
    ///         🔒 MANDATORY: Read `MCP-HARDENING.md` before implementing!
    ///     </para>
    ///     <para>
    ///         Implementations typically delegate to sub-providers:
    ///         - <see cref="IHashBlocklistChecker"/>
    ///         - <see cref="IPeerReputationStore"/>
    ///         - <see cref="IExternalModerationClient"/>
    ///     </para>
    /// </remarks>
    public interface IModerationProvider
    {
        /// <summary>
        ///     Checks a local file before it is indexed or shared.
        /// </summary>
        /// <param name="file">Metadata about the local file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A moderation decision.</returns>
        /// <remarks>
        ///     Called by the scanner before a file becomes shareable.
        ///     If Blocked or Quarantined, the file must NOT be added to shareable sets.
        /// </remarks>
        Task<ModerationDecision> CheckLocalFileAsync(
            LocalFileMetadata file,
            CancellationToken ct);

        /// <summary>
        ///     Checks a content ID before it is advertised or served.
        /// </summary>
        /// <param name="contentId">The content identifier (VirtualSoulfind item ID).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A moderation decision.</returns>
        /// <remarks>
        ///     Called before:
        ///     - Advertising content to DHT/mesh
        ///     - Serving chunks via content relay
        ///     If Blocked or Quarantined, content must NOT be advertised or served.
        /// </remarks>
        Task<ModerationDecision> CheckContentIdAsync(
            string contentId,
            CancellationToken ct);

        /// <summary>
        ///     Reports content for moderation (e.g., user flagged).
        /// </summary>
        /// <param name="contentId">The content identifier.</param>
        /// <param name="report">The moderation report.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        Task ReportContentAsync(
            string contentId,
            ModerationReport report,
            CancellationToken ct);

        /// <summary>
        ///     Reports a peer for moderation (e.g., bad behavior, blocked content).
        /// </summary>
        /// <param name="peerId">The internal peer identifier.</param>
        /// <param name="report">The peer report.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        /// <remarks>
        ///     🔒 CRITICAL: peerId must be an INTERNAL identifier, NOT:
        ///     - Soulseek username
        ///     - IP address
        ///     - External network identifier
        /// </remarks>
        Task ReportPeerAsync(
            string peerId,
            PeerReport report,
            CancellationToken ct);
    }

    /// <summary>
    ///     Report for content moderation.
    /// </summary>
    public sealed class ModerationReport
    {
        /// <summary>
        ///     Gets or initializes the reason code for the report.
        /// </summary>
        /// <remarks>
        ///     Examples: "user_flagged", "external_signal", "policy_violation"
        /// </remarks>
        public string ReasonCode { get; init; }

        /// <summary>
        ///     Gets or initializes optional notes.
        /// </summary>
        public string? Notes { get; init; }
    }

    /// <summary>
    ///     Report for peer moderation.
    /// </summary>
    public sealed class PeerReport
    {
        /// <summary>
        ///     Gets or initializes the reason code for the report.
        /// </summary>
        /// <remarks>
        ///     Examples:
        ///     - "associated_with_blocked_content"
        ///     - "requested_blocked_content"
        ///     - "served_bad_copy"
        ///     - "repeated_violations"
        /// </remarks>
        public string ReasonCode { get; init; }

        /// <summary>
        ///     Gets or initializes optional notes.
        /// </summary>
        public string? Notes { get; init; }
    }
}
