// <copyright file="InMemoryIntentQueue.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Intents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     In-memory implementation of <see cref="IIntentQueue"/>.
    /// </summary>
    public sealed class InMemoryIntentQueue : IIntentQueue
    {
        private readonly ConcurrentDictionary<string, DesiredRelease> _releases = new();
        private readonly ConcurrentDictionary<string, DesiredTrack> _tracks = new();

        public Task<DesiredRelease> EnqueueReleaseAsync(
            string releaseId,
            IntentPriority priority = IntentPriority.Normal,
            IntentMode mode = IntentMode.Wanted,
            string? notes = null,
            CancellationToken cancellationToken = default)
        {
            var desiredRelease = new DesiredRelease
            {
                DesiredReleaseId = Guid.NewGuid().ToString(),
                ReleaseId = releaseId,
                Priority = priority,
                Mode = mode,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Notes = notes,
            };

            _releases[desiredRelease.DesiredReleaseId] = desiredRelease;
            return Task.FromResult(desiredRelease);
        }

        public Task<DesiredTrack> EnqueueTrackAsync(
            string trackId,
            IntentPriority priority = IntentPriority.Normal,
            string? parentDesiredReleaseId = null,
            CancellationToken cancellationToken = default)
        {
            var desiredTrack = new DesiredTrack
            {
                DesiredTrackId = Guid.NewGuid().ToString(),
                TrackId = trackId,
                ParentDesiredReleaseId = parentDesiredReleaseId,
                Priority = priority,
                Status = IntentStatus.Pending,
                PlannedSources = null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _tracks[desiredTrack.DesiredTrackId] = desiredTrack;
            return Task.FromResult(desiredTrack);
        }

        public Task<IReadOnlyList<DesiredTrack>> GetPendingTracksAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var pending = _tracks.Values
                .Where(t => t.Status == IntentStatus.Pending)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<DesiredTrack>>(pending);
        }

        public Task UpdateTrackStatusAsync(
            string desiredTrackId,
            IntentStatus newStatus,
            CancellationToken cancellationToken = default)
        {
            if (_tracks.TryGetValue(desiredTrackId, out var track))
            {
                var updated = new DesiredTrack
                {
                    DesiredTrackId = track.DesiredTrackId,
                    TrackId = track.TrackId,
                    ParentDesiredReleaseId = track.ParentDesiredReleaseId,
                    Priority = track.Priority,
                    Status = newStatus,
                    PlannedSources = track.PlannedSources,
                    CreatedAt = track.CreatedAt,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };

                _tracks[desiredTrackId] = updated;
            }

            return Task.CompletedTask;
        }

        public Task<DesiredTrack?> GetTrackIntentAsync(
            string desiredTrackId,
            CancellationToken cancellationToken = default)
        {
            _tracks.TryGetValue(desiredTrackId, out var track);
            return Task.FromResult(track);
        }

        public Task<int> CountTracksByStatusAsync(
            IntentStatus status,
            CancellationToken cancellationToken = default)
        {
            var count = _tracks.Values.Count(t => t.Status == status);
            return Task.FromResult(count);
        }
    }
}
