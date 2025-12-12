// <copyright file="Track.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;

    /// <summary>
    ///     Represents a track (recording) in the virtual catalogue.
    /// </summary>
    /// <remarks>
    ///     A track is the atomic unit of music content:
    ///     - Belongs to a specific release
    ///     - Has a position (disc + track number)
    ///     - Maps to a canonical recording (MusicBrainz recording ID)
    ///     - Has expected duration for matching purposes
    /// </remarks>
    public sealed class Track
    {
        /// <summary>
        ///     Gets or initializes the internal track ID.
        /// </summary>
        public string TrackId { get; init; }

        /// <summary>
        ///     Gets or initializes the MusicBrainz recording ID (if available).
        /// </summary>
        public string? MusicBrainzRecordingId { get; init; }

        /// <summary>
        ///     Gets or initializes the release ID this track belongs to.
        /// </summary>
        public string ReleaseId { get; init; }

        /// <summary>
        ///     Gets or initializes the disc number (1-based).
        /// </summary>
        public int DiscNumber { get; init; } = 1;

        /// <summary>
        ///     Gets or initializes the track number within the disc (1-based).
        /// </summary>
        public int TrackNumber { get; init; }

        /// <summary>
        ///     Gets or initializes the track title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        ///     Gets or initializes the canonical duration in seconds.
        /// </summary>
        /// <remarks>
        ///     Used for matching local files (duration must be within tolerance).
        /// </remarks>
        public int? DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the ISRC (International Standard Recording Code).
        /// </summary>
        public string? Isrc { get; init; }

        /// <summary>
        ///     Gets or initializes genre/tag list (comma-separated).
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        ///     Gets or initializes when this track was added.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this track was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
