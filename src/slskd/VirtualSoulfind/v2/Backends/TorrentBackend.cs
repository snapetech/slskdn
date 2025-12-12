// <copyright file="TorrentBackend.cs" company="slskd Team">
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
    ///     Backend for BitTorrent content discovery.
    /// </summary>
    /// <remarks>
    ///     Stub implementation until torrent integration is ready.
    ///     Will search DHT and trackers for relevant torrents.
    /// </remarks>
    public sealed class TorrentBackend : IContentBackend
    {
        public ContentBackendType Type => ContentBackendType.Torrent;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find torrent candidates (stub).
        /// </summary>
        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            // TODO: T-V2-P4-04 - Implement torrent DHT/tracker search
            IReadOnlyList<SourceCandidate> empty = Array.Empty<SourceCandidate>();
            return Task.FromResult(empty);
        }

        /// <summary>
        ///     Validate torrent candidate (stub).
        /// </summary>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.Torrent)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not a Torrent candidate"));
            }

            // TODO: T-V2-P4-04 - Validate torrent health/seeders
            return Task.FromResult(SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality));
        }
    }
}
