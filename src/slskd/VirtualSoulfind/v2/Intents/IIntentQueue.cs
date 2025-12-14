// <copyright file="IIntentQueue.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Queue for managing content acquisition intents.
    /// </summary>
    /// <remarks>
    ///     The Intent Queue tracks what the user wants to acquire,
    ///     separate from the mechanics of how to get it.
    /// </remarks>
    public interface IIntentQueue
    {
        /// <summary>
        ///     Enqueue a release intent.
        /// </summary>
        Task<DesiredRelease> EnqueueReleaseAsync(
            string releaseId,
            IntentPriority priority = IntentPriority.Normal,
            IntentMode mode = IntentMode.Wanted,
            string? notes = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Enqueue a track intent.
        /// </summary>
        Task<DesiredTrack> EnqueueTrackAsync(
            ContentDomain domain,
            string trackId,
            IntentPriority priority = IntentPriority.Normal,
            string? parentDesiredReleaseId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get pending intents (not yet planned or completed).
        /// </summary>
        Task<IReadOnlyList<DesiredTrack>> GetPendingTracksAsync(
            int limit = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Update intent status.
        /// </summary>
        Task UpdateTrackStatusAsync(
            string desiredTrackId,
            IntentStatus newStatus,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get intent by ID.
        /// </summary>
        Task<DesiredTrack?> GetTrackIntentAsync(
            string desiredTrackId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Count intents by status.
        /// </summary>
        Task<int> CountTracksByStatusAsync(
            IntentStatus status,
            CancellationToken cancellationToken = default);
    }
}
