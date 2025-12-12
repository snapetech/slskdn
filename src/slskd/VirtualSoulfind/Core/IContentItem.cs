// <copyright file="IContentItem.cs" company="slskd Team">
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
    ///     Domain-neutral interface for a content item (track, episode, file, etc.).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A "content item" represents a discrete unit of content:
    ///         - Music: A track/recording
    ///         - Movie: A specific cut/version
    ///         - TV: An episode
    ///         - Book: A chapter
    ///         - GenericFile: An arbitrary file
    ///     </para>
    ///     <para>
    ///         Implementations provide domain-specific metadata (MBID, ISRC, hash, etc.)
    ///         while adhering to this common interface.
    ///     </para>
    /// </remarks>
    public interface IContentItem
    {
        /// <summary>
        ///     Gets the unique identifier for this item.
        /// </summary>
        ContentItemId Id { get; }

        /// <summary>
        ///     Gets the content domain (Music, GenericFile, etc.).
        /// </summary>
        ContentDomain Domain { get; }

        /// <summary>
        ///     Gets the parent work ID (album, season, etc.).
        /// </summary>
        /// <remarks>
        ///     For GenericFile domain, this may be null if the file is standalone.
        /// </remarks>
        ContentWorkId? WorkId { get; }

        /// <summary>
        ///     Gets the title of the item.
        /// </summary>
        /// <remarks>
        ///     - Music: Track title
        ///     - Movie: Film title (with variant, e.g., "Director's Cut")
        ///     - TV: Episode title
        ///     - Book: Chapter title
        ///     - GenericFile: Filename
        /// </remarks>
        string Title { get; }

        /// <summary>
        ///     Gets the position/index within the parent work (optional).
        /// </summary>
        /// <remarks>
        ///     - Music: Track number
        ///     - TV: Episode number
        ///     - Book: Chapter number
        ///     - GenericFile: N/A (null)
        /// </remarks>
        int? Position { get; }

        /// <summary>
        ///     Gets the duration of the item (optional).
        /// </summary>
        /// <remarks>
        ///     - Music: Track duration
        ///     - Movie: Film duration
        ///     - TV: Episode duration
        ///     - GenericFile: N/A (null)
        /// </remarks>
        TimeSpan? Duration { get; }
    }
}
