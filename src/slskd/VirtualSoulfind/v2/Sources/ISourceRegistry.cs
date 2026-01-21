// <copyright file="ISourceRegistry.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Sources
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;

    /// <summary>
    ///     Interface for the source registry that tracks where content can be obtained.
    /// </summary>
    /// <remarks>
    ///     The source registry is VirtualSoulfind's "phonebook" - it knows:
    ///     - Where to find content (which backends, which peers, which torrents, etc.)
    ///     - How trustworthy each source is
    ///     - When sources were last validated
    ///     
    ///     This is separate from the catalogue (what content exists) and the intent queue
    ///     (what we want to fetch).
    /// </remarks>
    public interface ISourceRegistry
    {
        /// <summary>
        ///     Finds all source candidates for a given content item.
        /// </summary>
        /// <param name="itemId">The content item ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of candidates (may be empty).</returns>
        Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Finds source candidates for a given content item, filtered by backend type.
        /// </summary>
        /// <param name="itemId">The content item ID.</param>
        /// <param name="backend">The backend type to filter by.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of candidates from the specified backend.</returns>
        Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            ContentBackendType backend,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Inserts or updates a source candidate.
        /// </summary>
        /// <param name="candidate">The candidate to upsert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpsertCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Removes a specific source candidate.
        /// </summary>
        /// <param name="candidateId">The candidate ID to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RemoveCandidateAsync(
            string candidateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Removes stale source candidates (not seen/validated recently).
        /// </summary>
        /// <param name="olderThan">Remove candidates not seen since this timestamp.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of candidates removed.</returns>
        Task<int> RemoveStaleCandidatesAsync(
            DateTimeOffset olderThan,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Counts total source candidates in the registry.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountCandidatesAsync(
            CancellationToken cancellationToken = default);
    }
}
