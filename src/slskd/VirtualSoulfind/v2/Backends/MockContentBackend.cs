// <copyright file="MockContentBackend.cs" company="slskd Team">
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
    ///     Mock backend for testing - allows injecting candidates.
    /// </summary>
    /// <remarks>
    ///     This is a test helper that lets you simulate any backend behavior.
    ///     Useful for integration tests without needing real network/storage.
    /// </remarks>
    public sealed class MockContentBackend : IContentBackend
    {
        private readonly Dictionary<ContentItemId, List<SourceCandidate>> _candidatesByItem = new();
        private readonly ContentBackendType _type;
        private readonly ContentDomain? _supportedDomain;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MockContentBackend"/> class.
        /// </summary>
        /// <param name="type">The backend type to simulate.</param>
        /// <param name="supportedDomain">The domain to support (null = all).</param>
        public MockContentBackend(
            ContentBackendType type = ContentBackendType.MeshDht,
            ContentDomain? supportedDomain = null)
        {
            _type = type;
            _supportedDomain = supportedDomain;
        }

        /// <inheritdoc/>
        public ContentBackendType Type => _type;

        /// <inheritdoc/>
        public ContentDomain? SupportedDomain => _supportedDomain;

        /// <summary>
        ///     Adds a candidate for testing.
        /// </summary>
        public void AddCandidate(ContentItemId itemId, SourceCandidate candidate)
        {
            if (!_candidatesByItem.ContainsKey(itemId))
            {
                _candidatesByItem[itemId] = new List<SourceCandidate>();
            }

            _candidatesByItem[itemId].Add(candidate);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            if (_candidatesByItem.TryGetValue(itemId, out var candidates))
            {
                return Task.FromResult<IReadOnlyList<SourceCandidate>>(candidates);
            }

            return Task.FromResult<IReadOnlyList<SourceCandidate>>(Array.Empty<SourceCandidate>());
        }

        /// <inheritdoc/>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            // Mock backend always validates successfully
            return Task.FromResult(SourceCandidateValidationResult.Valid(
                candidate.TrustScore,
                candidate.ExpectedQuality / 100.0f));
        }
    }
}
