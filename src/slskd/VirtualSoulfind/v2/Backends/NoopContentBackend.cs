// <copyright file="NoopContentBackend.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
