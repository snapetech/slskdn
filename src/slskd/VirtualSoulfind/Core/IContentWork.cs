// <copyright file="IContentWork.cs" company="slskd Team">
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
    /// <summary>
    ///     Domain-neutral interface for a content work (album, movie, book, etc.).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A "work" represents a logical grouping of content items:
    ///         - Music: An album/release containing tracks
    ///         - Movie: A film (possibly with variants)
    ///         - TV: A season containing episodes
    ///         - Book: An edition containing chapters
    ///         - GenericFile: A collection or archive
    ///     </para>
    ///     <para>
    ///         Implementations provide domain-specific metadata while adhering
    ///         to this common interface for VirtualSoulfind's core logic.
    ///     </para>
    /// </remarks>
    public interface IContentWork
    {
        /// <summary>
        ///     Gets the unique identifier for this work.
        /// </summary>
        ContentWorkId Id { get; }

        /// <summary>
        ///     Gets the content domain (Music, GenericFile, etc.).
        /// </summary>
        ContentDomain Domain { get; }

        /// <summary>
        ///     Gets the title of the work.
        /// </summary>
        /// <remarks>
        ///     - Music: Album title
        ///     - Movie: Film title
        ///     - TV: Season title (e.g., "Breaking Bad - Season 1")
        ///     - Book: Book title
        ///     - GenericFile: Collection name or filename
        /// </remarks>
        string Title { get; }

        /// <summary>
        ///     Gets the primary creator/artist of the work (optional).
        /// </summary>
        /// <remarks>
        ///     - Music: Artist name
        ///     - Movie: Director
        ///     - TV: Series creator
        ///     - Book: Author
        ///     - GenericFile: N/A (null)
        /// </remarks>
        string? Creator { get; }

        /// <summary>
        ///     Gets the year of release/publication (optional).
        /// </summary>
        /// <remarks>
        ///     Used for disambiguation and matching quality.
        /// </remarks>
        int? Year { get; }
    }
}
