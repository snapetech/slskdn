// <copyright file="DesiredRelease.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Intents
{
    using System;

    /// <summary>
    ///     Represents a user's intent to acquire a release.
    /// </summary>
    /// <remarks>
    ///     This is part of the "intent queue" - what the user WANTS, separate from
    ///     what's actually being fetched. The planner + resolver turn intents into plans.
    /// </remarks>
    public sealed class DesiredRelease
    {
        /// <summary>
        ///     Gets or initializes the unique ID for this intent.
        /// </summary>
        public string DesiredReleaseId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the release ID from the catalogue.
        /// </summary>
        public string ReleaseId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the priority.
        /// </summary>
        public IntentPriority Priority { get; init; }

        /// <summary>
        ///     Gets or initializes the acquisition mode.
        /// </summary>
        public IntentMode Mode { get; init; }

        /// <summary>
        ///     Gets or initializes the current status.
        /// </summary>
        public IntentStatus Status { get; init; }

        /// <summary>
        ///     Gets or initializes when this intent was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this intent was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes optional user notes.
        /// </summary>
        public string? Notes { get; init; }
    }
}
