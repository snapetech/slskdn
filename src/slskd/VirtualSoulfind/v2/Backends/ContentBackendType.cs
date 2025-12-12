// <copyright file="ContentBackendType.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Backends
{
    /// <summary>
    ///     Enum representing the type of content backend.
    /// </summary>
    /// <remarks>
    ///     VirtualSoulfind v2 supports multiple backends for content acquisition.
    ///     Different content domains (Music, Video, Book) may restrict which backends are allowed.
    /// </remarks>
    public enum ContentBackendType
    {
        /// <summary>
        ///     Local library (content already on disk).
        /// </summary>
        LocalLibrary,

        /// <summary>
        ///     Soulseek network (Music domain only).
        /// </summary>
        /// <remarks>
        ///     Subject to strict per-backend caps (H-08).
        ///     NOT allowed for Video, Book, or GenericFile domains.
        /// </remarks>
        Soulseek,

        /// <summary>
        ///     Mesh/DHT overlay (multi-source swarm).
        /// </summary>
        MeshDht,

        /// <summary>
        ///     BitTorrent / multi-swarm.
        /// </summary>
        Torrent,

        /// <summary>
        ///     HTTP/HTTPS sources (catalogues, CDN, etc.).
        /// </summary>
        /// <remarks>
        ///     Must use SSRF-safe HTTP client.
        ///     Domain allowlists required.
        /// </remarks>
        Http,

        /// <summary>
        ///     LAN sources (local network shares, etc.).
        /// </summary>
        Lan,
    }
}
