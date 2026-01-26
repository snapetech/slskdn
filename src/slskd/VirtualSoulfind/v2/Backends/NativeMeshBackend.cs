// <copyright file="NativeMeshBackend.cs" company="slskd Team">
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
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd.MediaCore;
    using slskd.Mesh;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for native mesh overlay content (no Soulseek, no BitTorrent).
    /// </summary>
    /// <remarks>
    ///     Find candidates via mesh/DHT only (IMeshDirectory.FindPeersByContentAsync).
    ///     Fetch is done by the resolver; the mesh "get content by ContentId / hash" RPC
    ///     and resolver support for BackendRef "mesh:{peerId}:{contentId}" are follow-ups.
    ///     Use case: mesh-only, disaster mode, closed communities.
    /// </remarks>
    public sealed class NativeMeshBackend : IContentBackend
    {
        private readonly IMeshDirectory _meshDirectory;
        private readonly IContentIdRegistry _contentIdRegistry;
        private readonly IOptionsMonitor<NativeMeshBackendOptions> _options;
        private readonly ILogger<NativeMeshBackend> _logger;

        public NativeMeshBackend(
            IMeshDirectory meshDirectory,
            IContentIdRegistry contentIdRegistry,
            IOptionsMonitor<NativeMeshBackendOptions> options,
            ILogger<NativeMeshBackend> logger)
        {
            _meshDirectory = meshDirectory ?? throw new ArgumentNullException(nameof(meshDirectory));
            _contentIdRegistry = contentIdRegistry ?? throw new ArgumentNullException(nameof(contentIdRegistry));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ContentBackendType Type => ContentBackendType.NativeMesh;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find native mesh candidates via IMeshDirectory (DHT content-peers lookup).
        /// </summary>
        /// <remarks>
        ///     Resolves ContentItemId to ContentId via IContentIdRegistry (mb:recording:{guid})
        ///     or conventional fallback (content:audio:track:mb-{guid}). Does not use ISourceRegistry.
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

            var contentId = await ResolveContentIdAsync(itemId, cancellationToken);
            if (string.IsNullOrEmpty(contentId))
            {
                _logger.LogDebug("[NativeMesh] No ContentId for item {ItemId}, skipping", itemId);
                return Array.Empty<SourceCandidate>();
            }

            IReadOnlyList<MeshPeerDescriptor> peers;
            try
            {
                peers = await _meshDirectory.FindPeersByContentAsync(contentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NativeMesh] FindPeersByContentAsync failed for {ContentId}", contentId);
                return Array.Empty<SourceCandidate>();
            }

            if (peers == null || peers.Count == 0)
            {
                return Array.Empty<SourceCandidate>();
            }

            var candidates = new List<SourceCandidate>();
            foreach (var p in peers)
            {
                if (string.IsNullOrEmpty(p.PeerId))
                    continue;

                var backendRef = $"mesh:{p.PeerId}:{contentId}";
                candidates.Add(new SourceCandidate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = itemId,
                    Backend = ContentBackendType.NativeMesh,
                    BackendRef = backendRef,
                    ExpectedQuality = 0.5f,
                    TrustScore = opts.MinimumTrustScore,
                    LastSeenAt = DateTimeOffset.UtcNow,
                });
            }

            return candidates
                .Where(c => c.TrustScore >= opts.MinimumTrustScore)
                .OrderByDescending(c => c.TrustScore)
                .ThenByDescending(c => c.ExpectedQuality)
                .Take(opts.MaxCandidatesPerItem)
                .ToList();
        }

        /// <summary>
        ///     Validate NativeMesh candidate: type, BackendRef format, and options.
        /// </summary>
        /// <remarks>
        ///     Does not perform mesh reachability checks; format-only for now.
        /// </remarks>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.NativeMesh)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not a NativeMesh candidate"));
            }

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("NativeMesh backend disabled"));
            }

            if (string.IsNullOrWhiteSpace(candidate.BackendRef))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Empty BackendRef"));
            }

            if (!TryParseBackendRef(candidate.BackendRef, out _, out _))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Invalid BackendRef format; expected mesh:{peerId}:{contentId}"));
            }

            if (candidate.TrustScore < opts.MinimumTrustScore)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid(
                    $"Trust score {candidate.TrustScore} below minimum {opts.MinimumTrustScore}"));
            }

            return Task.FromResult(SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality));
        }

        private async Task<string?> ResolveContentIdAsync(ContentItemId itemId, CancellationToken ct)
        {
            var externalId = "mb:recording:" + itemId.Value.ToString();
            var resolved = await _contentIdRegistry.ResolveAsync(externalId, ct);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;

            return ContentIdParser.Create("audio", "track", "mb-" + itemId.Value.ToString("N"));
        }

        private static bool TryParseBackendRef(string backendRef, out string? peerId, out string? contentId)
        {
            peerId = null;
            contentId = null;
            if (string.IsNullOrWhiteSpace(backendRef) || !backendRef.StartsWith("mesh:", StringComparison.OrdinalIgnoreCase))
                return false;

            var rest = backendRef.Substring(5);
            var idx = rest.IndexOf(':');
            if (idx <= 0 || idx >= rest.Length - 1)
                return false;

            peerId = rest.Substring(0, idx);
            contentId = rest.Substring(idx + 1);
            return !string.IsNullOrEmpty(peerId) && !string.IsNullOrEmpty(contentId);
        }
    }

    /// <summary>
    ///     Configuration for NativeMesh backend.
    /// </summary>
    public sealed class NativeMeshBackendOptions
    {
        /// <summary>
        ///     Enable native mesh backend.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Minimum trust score (0.0 - 1.0).
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
