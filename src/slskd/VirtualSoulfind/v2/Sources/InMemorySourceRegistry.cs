// <copyright file="InMemorySourceRegistry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Sources
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;

    /// <summary>
    ///     In-memory implementation of <see cref="ISourceRegistry"/> for testing.
    /// </summary>
    public sealed class InMemorySourceRegistry : ISourceRegistry
    {
        private readonly ConcurrentDictionary<string, SourceCandidate> _candidates = new();

        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            var results = _candidates.Values
                .Where(c => c.ItemId.Equals(itemId))
                .ToList();

            return Task.FromResult<IReadOnlyList<SourceCandidate>>(results);
        }

        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            ContentBackendType backend,
            CancellationToken cancellationToken = default)
        {
            var results = _candidates.Values
                .Where(c => c.ItemId.Equals(itemId) && c.Backend == backend)
                .ToList();

            return Task.FromResult<IReadOnlyList<SourceCandidate>>(results);
        }

        public Task UpsertCandidateAsync(SourceCandidate candidate, CancellationToken cancellationToken = default)
        {
            _candidates[candidate.Id] = candidate;
            return Task.CompletedTask;
        }

        public Task RemoveCandidateAsync(string candidateId, CancellationToken cancellationToken = default)
        {
            _candidates.TryRemove(candidateId, out _);
            return Task.CompletedTask;
        }

        public Task<int> RemoveStaleCandidatesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
        {
            var stale = _candidates.Values
                .Where(c => c.LastSeenAt < olderThan)
                .ToList();

            foreach (var candidate in stale)
            {
                _candidates.TryRemove(candidate.Id, out _);
            }

            return Task.FromResult(stale.Count);
        }

        public Task<int> CountCandidatesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_candidates.Count);
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
