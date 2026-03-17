// <copyright file="ContentIdentifiers.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.Core
{
    using System;

    /// <summary>
    ///     Unique identifier for a content work (e.g., album, movie, book).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A "work" is a logical grouping of content items. Examples:
    ///         - Music: An album/release containing multiple tracks
    ///         - Movie: A film with potential variants (theatrical, director's cut)
    ///         - TV: A season containing episodes
    ///         - Book: An edition containing chapters
    ///     </para>
    ///     <para>
    ///         In the Music domain, ContentWorkId maps to MusicBrainz Release IDs.
    ///     </para>
    /// </remarks>
    public readonly record struct ContentWorkId(Guid Value)
    {
        /// <summary>
        ///     Creates a new random ContentWorkId.
        /// </summary>
        /// <returns>A new unique ContentWorkId.</returns>
        public static ContentWorkId NewId() => new(Guid.NewGuid());

        /// <summary>
        ///     Parses a ContentWorkId from a string.
        /// </summary>
        /// <param name="value">The string representation of the GUID.</param>
        /// <returns>A ContentWorkId.</returns>
        /// <exception cref="FormatException">If the string is not a valid GUID.</exception>
        public static ContentWorkId Parse(string value) => new(Guid.Parse(value));

        /// <summary>
        ///     Tries to parse a ContentWorkId from a string.
        /// </summary>
        /// <param name="value">The string representation of the GUID.</param>
        /// <param name="result">The parsed ContentWorkId if successful.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        public static bool TryParse(string value, out ContentWorkId result)
        {
            if (Guid.TryParse(value, out var guid))
            {
                result = new ContentWorkId(guid);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        ///     Returns the string representation of the ContentWorkId.
        /// </summary>
        /// <returns>The GUID as a string.</returns>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    ///     Unique identifier for a content item (e.g., track, episode, chapter).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A "content item" is a discrete unit of content within a work. Examples:
    ///         - Music: A track/recording within an album
    ///         - Movie: A specific version/cut of a film
    ///         - TV: An episode within a season
    ///         - Book: A chapter within an edition
    ///         - GenericFile: An arbitrary file
    ///     </para>
    ///     <para>
    ///         In the Music domain, ContentItemId maps to MusicBrainz Recording IDs.
    ///     </para>
    /// </remarks>
    public readonly record struct ContentItemId(Guid Value)
    {
        /// <summary>
        ///     Creates a new random ContentItemId.
        /// </summary>
        /// <returns>A new unique ContentItemId.</returns>
        public static ContentItemId NewId() => new(Guid.NewGuid());

        /// <summary>
        ///     Parses a ContentItemId from a string.
        /// </summary>
        /// <param name="value">The string representation of the GUID.</param>
        /// <returns>A ContentItemId.</returns>
        /// <exception cref="FormatException">If the string is not a valid GUID.</exception>
        public static ContentItemId Parse(string value) => new(Guid.Parse(value));

        /// <summary>
        ///     Tries to parse a ContentItemId from a string.
        /// </summary>
        /// <param name="value">The string representation of the GUID.</param>
        /// <param name="result">The parsed ContentItemId if successful.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        public static bool TryParse(string value, out ContentItemId result)
        {
            if (Guid.TryParse(value, out var guid))
            {
                result = new ContentItemId(guid);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        ///     Returns the string representation of the ContentItemId.
        /// </summary>
        /// <returns>The GUID as a string.</returns>
        public override string ToString() => Value.ToString();
    }
}
