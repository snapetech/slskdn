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
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for BitTorrent content discovery.
    /// </summary>
    /// <remarks>
    ///     Phase 2 implementation: Query source registry for torrent candidates.
    ///     Future integration points:
    ///     - ITorrentClient for DHT queries
    ///     - Tracker announces
    ///     - Swarm health checking
    /// </remarks>
    public sealed class TorrentBackend : IContentBackend
    {
        private readonly ISourceRegistry _sourceRegistry;
        private readonly IOptionsMonitor<TorrentBackendOptions> _options;

        public TorrentBackend(
            ISourceRegistry sourceRegistry,
            IOptionsMonitor<TorrentBackendOptions> options)
        {
            _sourceRegistry = sourceRegistry;
            _options = options;
        }

        public ContentBackendType Type => ContentBackendType.Torrent;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find torrent candidates from source registry.
        /// </summary>
        /// <remarks>
        ///     Current: Registry lookup only.
        ///     Future: Active DHT/tracker queries when torrent client is integrated.
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

            // Query source registry for Torrent candidates
            var candidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                ContentBackendType.Torrent,
                cancellationToken);

            // Filter by minimum seeders (stored in ExpectedQuality for torrents)
            var filtered = candidates
                .Where(c => c.ExpectedQuality >= opts.MinimumSeeders)
                .OrderByDescending(c => c.ExpectedQuality) // Higher seeders = better
                .ThenByDescending(c => c.TrustScore)
                .Take(opts.MaxCandidatesPerItem)
                .ToList();

            return filtered;
        }

        /// <summary>
        ///     Validate torrent candidate.
        /// </summary>
        /// <remarks>
        ///     Current: Type, format, and seeder validation.
        ///     Future: Actual torrent health/availability checks via client.
        /// </remarks>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.Torrent)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not a Torrent candidate"));
            }

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Torrent backend disabled"));
            }

            // Validate BackendRef format (should be infohash or magnet link)
            if (string.IsNullOrWhiteSpace(candidate.BackendRef))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Empty BackendRef"));
            }

            // Basic infohash validation (40 hex chars for v1, 64 for v2)
            var refLower = candidate.BackendRef.ToLowerInvariant();
            bool isInfohash = (refLower.Length == 40 || refLower.Length == 64) &&
                              refLower.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
            bool isMagnet = refLower.StartsWith("magnet:", StringComparison.Ordinal);

            if (!isInfohash && !isMagnet)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Invalid infohash or magnet link"));
            }

            // Seeder threshold (stored in ExpectedQuality)
            if (candidate.ExpectedQuality < opts.MinimumSeeders)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid($"Seeders {candidate.ExpectedQuality} below minimum {opts.MinimumSeeders}"));
            }

            // TODO: T-V2-P4-04 - Add actual torrent health check via ITorrentClient when available
            // For now, accept if seeders are adequate
            return Task.FromResult(SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality));
        }
    }

    /// <summary>
    ///     Configuration for Torrent backend.
    /// </summary>
    public sealed class TorrentBackendOptions
    {
        /// <summary>
        ///     Enable torrent backend.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Minimum seeders to consider a torrent viable.
        /// </summary>
        public int MinimumSeeders { get; init; } = 2;

        /// <summary>
        ///     Maximum candidates to return per item.
        /// </summary>
        public int MaxCandidatesPerItem { get; init; } = 10;

        /// <summary>
        ///     Query timeout for DHT/tracker lookups (seconds).
        /// </summary>
        public int QueryTimeoutSeconds { get; init; } = 30;
    }
}
