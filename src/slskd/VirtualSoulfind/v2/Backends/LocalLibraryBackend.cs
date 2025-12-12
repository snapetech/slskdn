// <copyright file="LocalLibraryBackend.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Shares;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Content backend for the local library (scanned shares).
    /// </summary>
    /// <remarks>
    ///     The LocalLibrary backend is the fastest and most reliable:
    ///     - Instant access (no network)
    ///     - Zero cost (no bandwidth, no work budget)
    ///     - Highest trust (we scanned these files ourselves)
    ///     
    ///     This backend queries the share repository (SQLite) to find local files
    ///     that might match the requested content.
    /// </remarks>
    public sealed class LocalLibraryBackend : IContentBackend
    {
        private readonly IShareRepository _shareRepository;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LocalLibraryBackend"/> class.
        /// </summary>
        /// <param name="shareRepository">The share repository.</param>
        public LocalLibraryBackend(IShareRepository shareRepository)
        {
            _shareRepository = shareRepository ?? throw new ArgumentNullException(nameof(shareRepository));
        }

        /// <inheritdoc/>
        public ContentBackendType Type => ContentBackendType.LocalLibrary;

        /// <inheritdoc/>
        public ContentDomain? SupportedDomain => ContentDomain.Music;

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            // For v2 Phase 1: We need to map ContentItemId to what the share repository expects
            // The share repository uses contentId as string
            var contentIdStr = itemId.ToString();

            // Query the share repository for this content ID
            var contentItem = _shareRepository.FindContentItem(contentIdStr);

            if (contentItem == null || !contentItem.Value.IsAdvertisable)
            {
                // No local copy or not advertisable
                return Array.Empty<SourceCandidate>();
            }

            // Create a single SourceCandidate for this local file
            var candidate = new SourceCandidate
            {
                Id = $"local:{contentIdStr}",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = contentIdStr, // ContentId as reference
                ExpectedQuality = 100, // Local files are highest quality (we scanned them)
                TrustScore = 1.0f, // Maximum trust (our own files)
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                IsPreferred = true, // Always prefer local
            };

            return await Task.FromResult<IReadOnlyList<SourceCandidate>>(new[] { candidate });
        }

        /// <inheritdoc/>
        public async Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.LocalLibrary)
            {
                return SourceCandidateValidationResult.Invalid(
                    "Candidate is not a LocalLibrary candidate");
            }

            // Check if the content item still exists in the share repository
            var contentItem = _shareRepository.FindContentItem(candidate.BackendRef);

            if (contentItem == null)
            {
                return SourceCandidateValidationResult.Invalid(
                    "Content item no longer exists in local library");
            }

            if (!contentItem.Value.IsAdvertisable)
            {
                return SourceCandidateValidationResult.Invalid(
                    "Content item is no longer advertisable (MCP may have blocked it)");
            }

            // Local files are always valid if they exist and are advertisable
            return await Task.FromResult(
                SourceCandidateValidationResult.Valid(1.0f, 1.0f));
        }
    }
}
