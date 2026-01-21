// <copyright file="InMemorySourceRegistry.cs" company="slskd Team">
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
            CancellationToken ct = default)
        {
            var results = _candidates.Values
                .Where(c => c.ItemId.Equals(itemId))
                .ToList();

            return Task.FromResult<IReadOnlyList<SourceCandidate>>(results);
        }

        public Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            ContentBackendType backend,
            CancellationToken ct = default)
        {
            var results = _candidates.Values
                .Where(c => c.ItemId.Equals(itemId) && c.Backend == backend)
                .ToList();

            return Task.FromResult<IReadOnlyList<SourceCandidate>>(results);
        }

        public Task UpsertCandidateAsync(SourceCandidate candidate, CancellationToken ct = default)
        {
            _candidates[candidate.Id] = candidate;
            return Task.CompletedTask;
        }

        public Task RemoveCandidateAsync(string candidateId, CancellationToken ct = default)
        {
            _candidates.TryRemove(candidateId, out _);
            return Task.CompletedTask;
        }

        public Task<int> RemoveStaleCandidatesAsync(DateTimeOffset olderThan, CancellationToken ct = default)
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

        public Task<int> CountCandidatesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_candidates.Count);
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
