// <copyright file="IContentBackend.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Backends
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Interface for content backends that can provide sources for content items.
    /// </summary>
    /// <remarks>
    ///     VirtualSoulfind v2 abstracts all content sources behind this interface.
    ///     This allows the planner to work with Soulseek, MeshDHT, Torrent, HTTP,
    ///     LAN, and local library without hardcoding any specific network protocol.
    /// </remarks>
    public interface IContentBackend
    {
        /// <summary>
        ///     Gets the backend type.
        /// </summary>
        ContentBackendType Type { get; }

        /// <summary>
        ///     Gets the content domain this backend supports (null = all domains).
        /// </summary>
        /// <remarks>
        ///     Example: SoulseekBackend returns ContentDomain.Music (only).
        ///     LocalLibrary returns null (supports all domains).
        /// </remarks>
        ContentDomain? SupportedDomain { get; }

        /// <summary>
        ///     Discovers potential sources (candidates) for a given content item.
        /// </summary>
        /// <param name="itemId">The content item to find sources for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of source candidates (may be empty).</returns>
        /// <remarks>
        ///     This method MUST:
        ///     - Respect work budgets (H-02)
        ///     - Respect per-backend caps (H-08 for Soulseek)
        ///     - Return quickly (use caching where appropriate)
        ///     - Never throw on "no results" (return empty list)
        /// </remarks>
        Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Validates a source candidate (checks if it's still available/trustworthy).
        /// </summary>
        /// <param name="candidate">The candidate to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation result with updated trust/quality scores.</returns>
        /// <remarks>
        ///     This is optional/best-effort validation. Backends may:
        ///     - Ping the source to check availability
        ///     - Verify expected quality/size
        ///     - Update trust scores based on reputation
        ///
        ///     This method MUST NOT:
        ///     - Download the full content (that's the resolver's job)
        ///     - Block for extended periods (use short timeouts)
        /// </remarks>
        Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken);
    }
}
