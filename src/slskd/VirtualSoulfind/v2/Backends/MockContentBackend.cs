// <copyright file="MockContentBackend.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
