// <copyright file="ModerationSubProviders.cs" company="slskd Team">
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
    ///     Sub-provider for checking hashes against blocklists.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         🔒 MANDATORY: See `MCP-HARDENING.md` Section 2.2 for:
    ///         - Timing attack mitigation (bloom filters)
    ///         - Constant-time comparisons
    ///         - Secure blocklist loading
    ///         - Hash logging restrictions (8-char prefix max)
    ///     </para>
    /// </remarks>
    public interface IHashBlocklistChecker
    {
        /// <summary>
        ///     Checks if a hash is in the blocklist.
        /// </summary>
        /// <param name="hash">The content hash to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the hash is blocked; otherwise false.</returns>
        Task<bool> IsBlockedHashAsync(string hash, CancellationToken ct);
    }

    /// <summary>
    ///     Sub-provider for peer reputation tracking and banning.
    /// </summary>

    /// <summary>
    ///     Sub-provider for external moderation services (optional, opt-in).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         🔒 MANDATORY: See `MCP-HARDENING.md` Section 2.1 for:
    ///         - SSRF protection (domain allowlist, HTTPS only)
    ///         - Request size limits (max 10KB metadata, max 100MB files)
    ///         - Timeouts (default 5s)
    ///         - Work budget integration
    ///         - No sensitive data in requests (no raw hashes, no full paths)
    ///     </para>
    /// </remarks>
    public interface IExternalModerationClient
    {
        /// <summary>
        ///     Analyzes a file using an external moderation service.
        /// </summary>
        /// <param name="file">Metadata about the file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A moderation decision.</returns>
        /// <remarks>
        ///     <para>
        ///         This is OPTIONAL and DISABLED by default.
        ///         Operators must explicitly configure:
        ///         - External service endpoint
        ///         - Domain allowlist
        ///         - API credentials (if needed)
        ///     </para>
        ///     <para>
        ///         🔒 SSRF PROTECTION: Only call endpoints in configured AllowedDomains.
        ///     </para>
        /// </remarks>
        Task<ModerationDecision> AnalyzeFileAsync(
            LocalFileMetadata file,
            CancellationToken ct);
    }
}

