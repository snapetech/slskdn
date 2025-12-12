// <copyright file="SourceCandidate.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Sources
{
    using System;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;

    /// <summary>
    ///     Represents a potential source for obtaining a specific content item.
    /// </summary>
    /// <remarks>
    ///     A SourceCandidate tracks where we might be able to get a track/book/movie/etc.
    ///     It abstracts away backend-specific details while preserving enough info to:
    ///     - Decide if this source is worth trying
    ///     - Actually fetch from it when the planner chooses it
    /// </remarks>
    public sealed class SourceCandidate
    {
        /// <summary>
        ///     Gets or initializes the unique identifier for this candidate.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        ///     Gets or initializes the content item ID this candidate provides.
        /// </summary>
        public ContentItemId ItemId { get; init; }

        /// <summary>
        ///     Gets or initializes the backend type.
        /// </summary>
        public ContentBackendType Backend { get; init; }

        /// <summary>
        ///     Gets or initializes the backend-specific reference.
        /// </summary>
        /// <remarks>
        ///     Examples:
        ///     - Soulseek: Internal peer ID + file path
        ///     - MeshDHT: Service descriptor ID + content key
        ///     - Torrent: Infohash + file index
        ///     - Http: URL
        ///     - LocalLibrary: LocalFileId
        /// </remarks>
        public string BackendRef { get; init; }

        /// <summary>
        ///     Gets or initializes the expected quality score.
        /// </summary>
        /// <remarks>
        ///     Domain-specific quality metric (0.0 - 1.0).
        ///     Higher is better.
        /// </remarks>
        public float ExpectedQuality { get; init; }

        /// <summary>
        ///     Gets or initializes the trust score for this source.
        /// </summary>
        /// <remarks>
        ///     0.0 = untrusted, 1.0 = fully trusted.
        ///     Used by planner to prefer reliable sources.
        /// </remarks>
        public float TrustScore { get; init; }

        /// <summary>
        ///     Gets or initializes when this candidate was last validated.
        /// </summary>
        public DateTimeOffset? LastValidatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this candidate was last seen available.
        /// </summary>
        public DateTimeOffset? LastSeenAt { get; init; }

        /// <summary>
        ///     Gets or initializes whether this candidate is preferred by the user.
        /// </summary>
        public bool IsPreferred { get; init; }
    }
}
