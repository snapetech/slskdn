// <copyright file="ContentDomain.cs" company="slskd Team">
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
    ///     Represents a content domain supported by VirtualSoulfind.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         VirtualSoulfind supports multiple content domains, each with domain-specific
    ///         matching logic, metadata, and backend selection rules.
    ///     </para>
    ///     <para>
    ///         <b>Music Domain</b> is the primary and fully supported domain, backed by:
    ///         - MusicBrainz for metadata
    ///         - Soulseek for peer-to-peer acquisition
    ///         - Mesh/DHT for distributed discovery
    ///         - Local library for owned content
    ///     </para>
    ///     <para>
    ///         <b>GenericFile Domain</b> supports arbitrary files without specific metadata,
    ///         using hash-based matching. Soulseek is NOT available for this domain.
    ///     </para>
    ///     <para>
    ///         Future domains (Movies, TV, Books, etc.) will have their own metadata providers
    ///         and matching logic.
    ///     </para>
    /// </remarks>
    public enum ContentDomain
    {
        /// <summary>
        ///     Music domain (albums, tracks, artists).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Primary and fully supported domain with:
        ///         - MBID-based matching (MusicBrainz)
        ///         - Duration, bitrate, codec matching
        ///         - Soulseek backend support
        ///         - Mesh/DHT discovery
        ///     </para>
        ///     <para>
        ///         Works correspond to Albums/Releases.
        ///         Items correspond to Tracks/Recordings.
        ///     </para>
        /// </remarks>
        Music = 0,

        /// <summary>
        ///     Generic file domain (arbitrary files).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Supports arbitrary files without specific metadata:
        ///         - SHA256 hash-based matching
        ///         - File size matching
        ///         - NO Soulseek backend (mesh/DHT/torrent/local only)
        ///     </para>
        ///     <para>
        ///         Works and Items are both generic file entries.
        ///     </para>
        /// </remarks>
        GenericFile = 1,

        /// <summary>
        ///     Image domain (T-911 placeholder). Placeholder for future image-specific variants.
        /// </summary>
        Image = 2,

        /// <summary>
        ///     Video domain (T-911 placeholder). Placeholder for future video-specific variants.
        /// </summary>
        Video = 3,

        /// <summary>
        ///     Movie domain (films, feature films).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Supports movie content with:
        ///         - IMDB ID-based matching
        ///         - Hash-based matching (SHA256)
        ///         - File size matching
        ///         - NO Soulseek backend (mesh/DHT/torrent/HTTP/local only)
        ///     </para>
        ///     <para>
        ///         Works correspond to Movies.
        ///         Items correspond to specific encodings/editions.
        ///     </para>
        /// </remarks>
        Movie = 4,

        /// <summary>
        ///     TV domain (television shows, series, episodes).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Supports TV content with:
        ///         - TVDB ID-based matching (series, season, episode)
        ///         - Hash-based matching (SHA256)
        ///         - File size matching
        ///         - NO Soulseek backend (mesh/DHT/torrent/HTTP/local only)
        ///     </para>
        ///     <para>
        ///         Works correspond to TV Series.
        ///         Items correspond to Episodes.
        ///     </para>
        /// </remarks>
        Tv = 5,

        /// <summary>
        ///     Book domain (books, documents, ebooks).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Supports book content with:
        ///         - ISBN-based matching
        ///         - Hash-based matching (SHA256)
        ///         - Format detection (PDF, EPUB, etc.)
        ///         - NO Soulseek backend (mesh/DHT/torrent/HTTP/local only)
        ///     </para>
        ///     <para>
        ///         Works correspond to Books.
        ///         Items correspond to specific editions/formats.
        ///     </para>
        /// </remarks>
        Book = 6,

        // Future: Game, Software
    }
}

