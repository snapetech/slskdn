// <copyright file="DesiredRelease.cs" company="slskd Team">
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
        public string DesiredReleaseId { get; init; }

        /// <summary>
        ///     Gets or initializes the release ID from the catalogue.
        /// </summary>
        public string ReleaseId { get; init; }

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
