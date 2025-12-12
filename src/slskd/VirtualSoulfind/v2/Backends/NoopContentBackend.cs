// <copyright file="NoopContentBackend.cs" company="slskd Team">
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
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     No-op content backend for testing and as a base implementation.
    /// </summary>
    public sealed class NoopContentBackend : IContentBackend
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NoopContentBackend"/> class.
        /// </summary>
        /// <param name="type">The backend type to report.</param>
        /// <param name="supportedDomain">The supported domain (null = all).</param>
        public NoopContentBackend(ContentBackendType type, ContentDomain? supportedDomain = null)
        {
            Type = type;
            SupportedDomain = supportedDomain;
        }

        /// <inheritdoc/>
        public ContentBackendType Type { get; }

        /// <inheritdoc/>
        public ContentDomain? SupportedDomain { get; }

        /// <inheritdoc/>
        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken)
        {
            // Return empty list (no candidates)
            return Task.FromResult<IReadOnlyList<SourceCandidate>>(Array.Empty<SourceCandidate>());
        }

        /// <inheritdoc/>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken)
        {
            // Always return invalid
            return Task.FromResult(SourceCandidateValidationResult.Invalid("noop_backend"));
        }
    }
}
