// <copyright file="DesiredTrack.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Intents
{
    using System;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Represents a user's intent to acquire a specific track.
    /// </summary>
    /// <remarks>
    ///     Tracks can be part of a DesiredRelease or standalone.
    /// </remarks>
    public sealed class DesiredTrack
    {
        /// <summary>
        ///     Gets or initializes the content domain.
        /// </summary>
        public ContentDomain Domain { get; init; }

        /// <summary>
        ///     Gets or initializes the unique ID for this intent.
        /// </summary>
        public string DesiredTrackId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the track ID from the catalogue.
        /// </summary>
        public string TrackId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the parent DesiredRelease ID (if part of a release).
        /// </summary>
        public string? ParentDesiredReleaseId { get; init; }

        /// <summary>
        ///     Gets or initializes the priority.
        /// </summary>
        public IntentPriority Priority { get; init; }

        /// <summary>
        ///     Gets or initializes the current status.
        /// </summary>
        public IntentStatus Status { get; init; }

        /// <summary>
        ///     Gets or initializes the planned sources (JSON summary).
        /// </summary>
        /// <remarks>
        ///     Optional field that stores a summary of the plan:
        ///     - Which backends will be tried
        ///     - What order
        ///     - Constraints
        ///
        ///     This is for observability/debugging, not execution.
        /// </remarks>
        public string? PlannedSources { get; init; }

        /// <summary>
        ///     Gets or initializes when this intent was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this intent was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
