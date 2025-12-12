// <copyright file="HttpBackend.cs" company="slskd Team">
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
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for HTTP/HTTPS content discovery.
    /// </summary>
    /// <remarks>
    ///     Stub implementation. Will use SSRF-safe HTTP client.
    ///     Supports allowlisted domains only.
    /// </remarks>
    public sealed class HttpBackend : IContentBackend
    {
        public ContentBackendType Type => ContentBackendType.Http;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find HTTP candidates (stub).
        /// </summary>
        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            // TODO: T-V2-P4-05 - Implement HTTP content discovery
            // Must use SSRF-safe client and domain allowlist
            IReadOnlyList<SourceCandidate> empty = Array.Empty<SourceCandidate>();
            return Task.FromResult(empty);
        }

        /// <summary>
        ///     Validate HTTP candidate (stub).
        /// </summary>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.Http)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not an HTTP candidate"));
            }

            // TODO: T-V2-P4-05 - Validate URL against allowlist, check HEAD request
            return Task.FromResult(SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality));
        }
    }
}
