// <copyright file="LocalFile.cs" company="slskd Team">
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
    ///     Represents a physical audio file in the local library.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         LocalFile bridges the virtual catalogue (Artist/Release/Track) to actual files on disk.
    ///         It stores technical metadata (codec, bitrate, hashes) and can be linked to a Track
    ///         via <see cref="InferredTrackId"/> or via a <see cref="VerifiedCopy"/>.
    ///     </para>
    ///     <para>
    ///         <b>Key Properties</b>:
    ///         - Path: Absolute file path
    ///         - Hashes: HashPrimary (SHA-256) and HashSecondary (MD5) for matching
    ///         - AudioFingerprintId: Foreign key to AudioFingerprint for acoustic matching
    ///         - InferredTrackId: Nullable link to Track (if match confidence is high)
    ///         - QualityRating: Derived score (0.0-1.0) based on codec/bitrate/channels
    ///     </para>
    /// </remarks>
    public sealed class LocalFile
    {
        /// <summary>
        ///     Gets or initializes the unique identifier for this local file.
        /// </summary>
        public required string LocalFileId { get; init; }

        /// <summary>
        ///     Gets or initializes the absolute file path.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        ///     Gets or initializes the file size in bytes.
        /// </summary>
        public required long SizeBytes { get; init; }

        /// <summary>
        ///     Gets or initializes the duration in seconds.
        /// </summary>
        /// <remarks>
        ///     Used for matching (tolerance: ±2 seconds).
        /// </remarks>
        public required int DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the audio codec (e.g., "FLAC", "MP3", "AAC").
        /// </summary>
        public required string Codec { get; init; }

        /// <summary>
        ///     Gets or initializes the bitrate in kbps (e.g., 320, 1411 for FLAC).
        /// </summary>
        public required int Bitrate { get; init; }

        /// <summary>
        ///     Gets or initializes the number of audio channels (1=mono, 2=stereo).
        /// </summary>
        public required int Channels { get; init; }

        /// <summary>
        ///     Gets or initializes the primary hash (SHA-256 hex).
        /// </summary>
        /// <remarks>
        ///     Used for high-confidence matching against remote sources.
        /// </remarks>
        public required string HashPrimary { get; init; }

        /// <summary>
        ///     Gets or initializes the secondary hash (MD5 hex).
        /// </summary>
        /// <remarks>
        ///     Fallback hash for sources that only provide MD5.
        /// </remarks>
        public required string HashSecondary { get; init; }

        /// <summary>
        ///     Gets or initializes the foreign key to the AudioFingerprint table.
        /// </summary>
        /// <remarks>
        ///     Null if fingerprinting is disabled or not yet computed.
        /// </remarks>
        public string? AudioFingerprintId { get; init; }

        /// <summary>
        ///     Gets or initializes the inferred Track ID (nullable).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Set when the match engine has high confidence that this file
        ///         corresponds to a specific Track in the virtual catalogue.
        ///     </para>
        ///     <para>
        ///         If null, the file is unmatched or ambiguous.
        ///     </para>
        /// </remarks>
        public string? InferredTrackId { get; init; }

        /// <summary>
        ///     Gets or initializes the timestamp when this local file was added to the catalogue.
        /// </summary>
        public required DateTimeOffset AddedAt { get; init; }

        /// <summary>
        ///     Gets or initializes the timestamp when this local file was last updated.
        /// </summary>
        public required DateTimeOffset UpdatedAt { get; init; }

        /// <summary>
        ///     Gets the quality rating (0.0-1.0) based on codec, bitrate, and channels.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>Scoring</b>:
        ///         - FLAC: 1.0
        ///         - MP3 ≥320kbps: 0.9
        ///         - MP3 256-319kbps: 0.8
        ///         - MP3 192-255kbps: 0.7
        ///         - MP3 128-191kbps: 0.6
        ///         - AAC ≥256kbps: 0.85
        ///         - Other: 0.5
        ///     </para>
        ///     <para>
        ///         This is used by the planner to prefer higher-quality sources.
        ///     </para>
        /// </remarks>
        public float QualityRating
        {
            get
            {
                if (Codec.Equals("FLAC", StringComparison.OrdinalIgnoreCase))
                {
                    return 1.0f;
                }

                if (Codec.Equals("MP3", StringComparison.OrdinalIgnoreCase))
                {
                    if (Bitrate >= 320)
                    {
                        return 0.9f;
                    }

                    if (Bitrate >= 256)
                    {
                        return 0.8f;
                    }

                    if (Bitrate >= 192)
                    {
                        return 0.7f;
                    }

                    if (Bitrate >= 128)
                    {
                        return 0.6f;
                    }

                    return 0.5f;
                }

                if (Codec.Equals("AAC", StringComparison.OrdinalIgnoreCase) && Bitrate >= 256)
                {
                    return 0.85f;
                }

                return 0.5f;
            }
        }
    }
}
