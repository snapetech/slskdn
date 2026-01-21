// <copyright file="VirtualTrack.cs" company="slskd Team">
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
    ///     Represents a track in the virtual catalogue.
    /// </summary>
    /// <remarks>
    ///     T-V2-P1-03: Virtual Catalogue Store.
    ///     Tracks belong to a release and map to ContentItemId.
    /// </remarks>
    public sealed class VirtualTrack
    {
        /// <summary>
        ///     Gets or sets the unique track identifier.
        /// </summary>
        /// <remarks>
        ///     Typically a hash of release + track number + normalized title,
        ///     or external ID (e.g., MBID recording).
        /// </remarks>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the release ID.
        /// </summary>
        /// <remarks>
        ///     Foreign key to VirtualRelease.Id.
        /// </remarks>
        public string ReleaseId { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the normalized track title.
        /// </summary>
        /// <remarks>
        ///     Used for matching and deduplication.
        /// </remarks>
        public string NormalizedTitle { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the display title.
        /// </summary>
        public string DisplayTitle { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the track number.
        /// </summary>
        public int? TrackNumber { get; set; }

        /// <summary>
        ///     Gets or sets the disc number (for multi-disc releases).
        /// </summary>
        public int? DiscNumber { get; set; }

        /// <summary>
        ///     Gets or sets the duration in seconds.
        /// </summary>
        /// <remarks>
        ///     Used for matching and quality verification.
        /// </remarks>
        public int? DurationSeconds { get; set; }

        /// <summary>
        ///     Gets or sets the optional MusicBrainz recording ID.
        /// </summary>
        public string? MusicBrainzRecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the optional ISRC (International Standard Recording Code).
        /// </summary>
        public string? Isrc { get; set; }

        /// <summary>
        ///     Gets or sets the optional AcoustID fingerprint.
        /// </summary>
        /// <remarks>
        ///     Used for acoustic matching via Chromaprint.
        /// </remarks>
        public string? AcoustIdFingerprint { get; set; }

        /// <summary>
        ///     Gets or sets the content item ID.
        /// </summary>
        /// <remarks>
        ///     Links this virtual track to the domain-neutral ContentItemId.
        ///     This allows the track to be associated with one or more source candidates.
        /// </remarks>
        public string? ContentItemId { get; set; }

        /// <summary>
        ///     Gets or sets the date this track was first added to the catalogue.
        /// </summary>
        public DateTimeOffset AddedAt { get; set; }

        /// <summary>
        ///     Gets or sets the date this track's metadata was last updated.
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
