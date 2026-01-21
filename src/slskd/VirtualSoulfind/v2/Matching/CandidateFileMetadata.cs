// <copyright file="CandidateFileMetadata.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Matching
{
    /// <summary>
    ///     Metadata about a candidate file for matching purposes.
    /// </summary>
    /// <remarks>
    ///     This is a sanitized DTO containing only the information needed for matching.
    ///     No full paths, no peer IDs, no sensitive data.
    /// </remarks>
    public sealed class CandidateFileMetadata
    {
        /// <summary>
        ///     Gets or initializes the filename (basename only, no path).
        /// </summary>
        public string Filename { get; init; }

        /// <summary>
        ///     Gets or initializes the file extension (e.g., ".flac", ".mp3").
        /// </summary>
        public string Extension { get; init; }

        /// <summary>
        ///     Gets or initializes the file size in bytes.
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        ///     Gets or initializes the duration in seconds (if audio file).
        /// </summary>
        public int? DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the SHA256 hash (if available).
        /// </summary>
        public string? Hash { get; init; }

        /// <summary>
        ///     Gets or initializes the Chromaprint fingerprint (if available).
        /// </summary>
        public string? Chromaprint { get; init; }

        /// <summary>
        ///     Gets or initializes embedded metadata (artist, title, album from tags).
        /// </summary>
        public EmbeddedMetadata? Embedded { get; init; }
    }

    /// <summary>
    ///     Embedded metadata from file tags.
    /// </summary>
    public sealed class EmbeddedMetadata
    {
        /// <summary>
        ///     Gets or initializes the artist name.
        /// </summary>
        public string? Artist { get; init; }

        /// <summary>
        ///     Gets or initializes the title.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        ///     Gets or initializes the album name.
        /// </summary>
        public string? Album { get; init; }

        /// <summary>
        ///     Gets or initializes the MusicBrainz recording ID (if present in tags).
        /// </summary>
        public string? MusicBrainzRecordingId { get; init; }
    }
}
