// <copyright file="ReleaseGroup.cs" company="slskd Team">
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
    ///     Primary type of a release group.
    /// </summary>
    public enum ReleaseGroupPrimaryType
    {
        /// <summary>Unknown type.</summary>
        Unknown,

        /// <summary>Full-length album.</summary>
        Album,

        /// <summary>Extended play (EP).</summary>
        EP,

        /// <summary>Single.</summary>
        Single,

        /// <summary>Compilation.</summary>
        Compilation,

        /// <summary>Live album.</summary>
        Live,

        /// <summary>Soundtrack.</summary>
        Soundtrack,

        /// <summary>Other type.</summary>
        Other,
    }

    /// <summary>
    ///     Represents a release group (logical album/EP/single concept).
    /// </summary>
    /// <remarks>
    ///     A release group is the abstract concept of an album/EP/single.
    ///     Multiple releases (editions) belong to one release group.
    /// </remarks>
    public sealed class ReleaseGroup
    {
        /// <summary>
        ///     Gets or initializes the internal release group ID.
        /// </summary>
        public string ReleaseGroupId { get; init; }

        /// <summary>
        ///     Gets or initializes the MusicBrainz release group ID (if available).
        /// </summary>
        public string? MusicBrainzId { get; init; }

        /// <summary>
        ///     Gets or initializes the artist ID.
        /// </summary>
        public string ArtistId { get; init; }

        /// <summary>
        ///     Gets or initializes the title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        ///     Gets or initializes the primary type.
        /// </summary>
        public ReleaseGroupPrimaryType PrimaryType { get; init; }

        /// <summary>
        ///     Gets or initializes the first release year (if known).
        /// </summary>
        public int? Year { get; init; }

        /// <summary>
        ///     Gets or initializes when this release group was added.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this release group was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
