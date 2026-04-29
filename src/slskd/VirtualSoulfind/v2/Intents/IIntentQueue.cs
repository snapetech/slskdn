// <copyright file="IIntentQueue.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Intents
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;

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
        ///     Get release intent by ID.
        /// </summary>
        Task<DesiredRelease?> GetReleaseIntentAsync(
            string desiredReleaseId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Count intents by status.
        /// </summary>
        Task<int> CountTracksByStatusAsync(
            IntentStatus status,
            CancellationToken cancellationToken = default);
    }
}
