// <copyright file="Release.cs" company="slskd Team">
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
    ///     Represents a specific release (edition) of a release group.
    /// </summary>
    /// <remarks>
    ///     A release is a concrete edition of an album/EP/single:
    ///     - 2009 US CD release
    ///     - 2010 UK vinyl remaster
    ///     - 2015 digital remaster
    ///     
    ///     Multiple releases belong to one release group.
    /// </remarks>
    public sealed class Release
    {
        /// <summary>
        ///     Gets or initializes the internal release ID.
        /// </summary>
        public string ReleaseId { get; init; }

        /// <summary>
        ///     Gets or initializes the MusicBrainz release ID (if available).
        /// </summary>
        public string? MusicBrainzId { get; init; }

        /// <summary>
        ///     Gets or initializes the release group ID.
        /// </summary>
        public string ReleaseGroupId { get; init; }

        /// <summary>
        ///     Gets or initializes the release title.
        /// </summary>
        /// <remarks>
        ///     Usually same as release group title, but may differ for special editions.
        /// </remarks>
        public string Title { get; init; }

        /// <summary>
        ///     Gets or initializes the release year.
        /// </summary>
        public int? Year { get; init; }

        /// <summary>
        ///     Gets or initializes the country code (e.g., "US", "GB", "JP").
        /// </summary>
        public string? Country { get; init; }

        /// <summary>
        ///     Gets or initializes the label name.
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        ///     Gets or initializes the catalogue number (label-specific ID).
        /// </summary>
        public string? CatalogNumber { get; init; }

        /// <summary>
        ///     Gets or initializes the number of discs/media in this release.
        /// </summary>
        public int MediaCount { get; init; } = 1;

        /// <summary>
        ///     Gets or initializes when this release was added.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this release was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
