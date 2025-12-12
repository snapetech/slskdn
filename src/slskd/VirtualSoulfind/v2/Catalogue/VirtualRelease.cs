// <copyright file="VirtualRelease.cs" company="slskd Team">
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
    ///     Represents a release (album/EP/single) in the virtual catalogue.
    /// </summary>
    /// <remarks>
    ///     T-V2-P1-03: Virtual Catalogue Store.
    ///     Releases belong to one or more artists and contain tracks.
    /// </remarks>
    public sealed class VirtualRelease
    {
        /// <summary>
        ///     Gets or sets the unique release identifier.
        /// </summary>
        /// <remarks>
        ///     Typically a hash of artist + normalized title + year,
        ///     or external ID (e.g., MBID release group).
        /// </remarks>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the artist ID.
        /// </summary>
        /// <remarks>
        ///     Foreign key to VirtualArtist.Id.
        ///     For compilations/various artists, use a special artist ID.
        /// </remarks>
        public string ArtistId { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the normalized release title.
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
        ///     Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        ///     Gets or sets the optional MusicBrainz release group ID.
        /// </summary>
        public string? MusicBrainzReleaseGroupId { get; set; }

        /// <summary>
        ///     Gets or sets the release type.
        /// </summary>
        /// <remarks>
        ///     E.g., "Album", "EP", "Single", "Compilation".
        /// </remarks>
        public string? ReleaseType { get; set; }

        /// <summary>
        ///     Gets or sets the total number of tracks expected in this release.
        /// </summary>
        /// <remarks>
        ///     Used for reconciliation to determine completeness.
        /// </remarks>
        public int? TotalTracks { get; set; }

        /// <summary>
        ///     Gets or sets the date this release was first added to the catalogue.
        /// </summary>
        public DateTimeOffset AddedAt { get; set; }

        /// <summary>
        ///     Gets or sets the date this release's metadata was last updated.
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
