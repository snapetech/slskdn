// <copyright file="VirtualArtist.cs" company="slskd Team">
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
    ///     T-V2-P1-03: Virtual Catalogue Store.
    ///     Artists are the top level of the catalogue hierarchy.
    /// </remarks>
    public sealed class VirtualArtist
    {
        /// <summary>
        ///     Gets or sets the unique artist identifier.
        /// </summary>
        /// <remarks>
        ///     Typically a hash of normalized artist name or external ID (e.g., MBID).
        /// </remarks>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the normalized artist name.
        /// </summary>
        /// <remarks>
        ///     Used for matching and deduplication.
        ///     Should be lowercase, stripped of punctuation, etc.
        /// </remarks>
        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the optional MusicBrainz ID.
        /// </summary>
        public string? MusicBrainzId { get; set; }

        /// <summary>
        ///     Gets or sets the optional sort name.
        /// </summary>
        /// <remarks>
        ///     Used for proper alphabetical sorting (e.g., "Beatles, The").
        /// </remarks>
        public string? SortName { get; set; }

        /// <summary>
        ///     Gets or sets the date this artist was first added to the catalogue.
        /// </summary>
        public DateTimeOffset AddedAt { get; set; }

        /// <summary>
        ///     Gets or sets the date this artist's metadata was last updated.
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
