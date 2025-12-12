// <copyright file="Artist.cs" company="slskd Team">
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
    ///     Represents an artist in the virtual catalogue.
    /// </summary>
    /// <remarks>
    ///     Artists are the top-level organizational unit in the music catalogue.
    ///     They map to external IDs (MusicBrainz, etc.) and have normalized metadata.
    /// </remarks>
    public sealed class Artist
    {
        /// <summary>
        ///     Gets or initializes the internal artist ID.
        /// </summary>
        public string ArtistId { get; init; }

        /// <summary>
        ///     Gets or initializes the MusicBrainz artist ID (if available).
        /// </summary>
        public string? MusicBrainzId { get; init; }

        /// <summary>
        ///     Gets or initializes the artist name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     Gets or initializes the sort name (for alphabetization).
        /// </summary>
        /// <remarks>
        ///     Example: "Beatles, The" for "The Beatles"
        /// </remarks>
        public string? SortName { get; init; }

        /// <summary>
        ///     Gets or initializes the genre/tag list (comma-separated).
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        ///     Gets or initializes when this artist was added to the catalogue.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this artist was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
