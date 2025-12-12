// <copyright file="MeshDhtBackend.cs" company="slskd Team">
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
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for mesh/DHT content discovery.
    /// </summary>
    /// <remarks>
    ///     Phase 2 implementation: Query source registry for mesh candidates.
    ///     Future integration points:
    ///     - IMeshClient for actual DHT queries
    ///     - Kademlia overlay for peer discovery
    ///     - Service fabric for content location
    /// </remarks>
    public sealed class MeshDhtBackend : IContentBackend
    {
        private readonly ISourceRegistry _sourceRegistry;
        private readonly IOptionsMonitor<MeshDhtBackendOptions> _options;

        public MeshDhtBackend(
            ISourceRegistry sourceRegistry,
            IOptionsMonitor<MeshDhtBackendOptions> options)
        {
            _sourceRegistry = sourceRegistry;
            _options = options;
        }

        public ContentBackendType Type => ContentBackendType.MeshDht;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find mesh/DHT candidates from source registry.
        /// </summary>
        /// <remarks>
        ///     Current implementation: Registry lookup only.
        ///     Future: Active DHT queries when mesh layer is integrated.
        /// </remarks>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Array.Empty<SourceCandidate>();
            }

            // Query source registry for MeshDht candidates
            var candidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                ContentBackendType.MeshDht,
                cancellationToken);

            // Apply trust score threshold
            var filtered = candidates
                .Where(c => c.TrustScore >= opts.MinimumTrustScore)
                .OrderByDescending(c => c.TrustScore)
                .ThenByDescending(c => c.ExpectedQuality)
                .Take(opts.MaxCandidatesPerItem)
                .ToList();

            return filtered;
        }

        /// <summary>
        ///     Validate mesh/DHT candidate.
        /// </summary>
        /// <remarks>
        ///     Current: Type and format validation.
        ///     Future: Actual node reachability checks via mesh client.
        /// </remarks>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.MeshDht)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not a MeshDht candidate"));
            }

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("MeshDht backend disabled"));
            }

            // Validate BackendRef format (should be mesh node reference)
            if (string.IsNullOrWhiteSpace(candidate.BackendRef))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Empty BackendRef"));
            }

            // Trust score threshold
            if (candidate.TrustScore < opts.MinimumTrustScore)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid($"Trust score {candidate.TrustScore} below minimum {opts.MinimumTrustScore}"));
            }

            // TODO: T-V2-P4-03 - Add actual mesh node reachability check via IMeshClient when available
            // For now, accept if trust score is adequate
            return Task.FromResult(SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality));
        }
    }

    /// <summary>
    ///     Configuration for MeshDht backend.
    /// </summary>
    public sealed class MeshDhtBackendOptions
    {
        /// <summary>
        ///     Enable mesh/DHT backend.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Minimum trust score to consider a mesh candidate (0.0 - 1.0).
        /// </summary>
        public float MinimumTrustScore { get; init; } = 0.3f;

        /// <summary>
        ///     Maximum candidates to return per item.
        /// </summary>
        public int MaxCandidatesPerItem { get; init; } = 20;

        /// <summary>
        ///     Query timeout for DHT lookups (seconds).
        /// </summary>
        public int QueryTimeoutSeconds { get; init; } = 30;
    }
}
