// <copyright file="HashDbEntry.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.HashDb.Models
{
    using System;

    /// <summary>
    ///     Represents an entry in the content-addressed hash database.
    ///     This is the core DHT data structure for sharing verified FLAC hashes.
    /// </summary>
    public class HashDbEntry
    {
        /// <summary>
        ///     Gets or sets the FLAC key (content-addressable identifier).
        ///     Format: SHA256 of (normalized_filename + ':' + size) truncated to 64 bits.
        /// </summary>
        public string FlacKey { get; set; }

        /// <summary>
        ///     Gets or sets the SHA256 hash of the first 32KB bytes.
        ///     This is used for byte-identical verification in multi-source downloads.
        /// </summary>
        public string ByteHash { get; set; }

        /// <summary>
        ///     Gets or sets the file size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets packed metadata flags (sample_rate/channels/bit_depth).
        /// </summary>
        public int? MetaFlags { get; set; }

        /// <summary>
        ///     Gets or sets when this entry was first seen (Unix timestamp).
        /// </summary>
        public long FirstSeenAt { get; set; }

        /// <summary>
        ///     Gets or sets when this entry was last updated (Unix timestamp).
        /// </summary>
        public long LastUpdatedAt { get; set; }

        /// <summary>
        ///     Gets or sets the monotonic sequence ID for delta sync.
        /// </summary>
        public long SeqId { get; set; }

        /// <summary>
        ///     Gets or sets the number of times this hash has been seen/verified.
        /// </summary>
        public int UseCount { get; set; } = 1;

        /// <summary>
        ///     Gets the first seen time as DateTime.
        /// </summary>
        public DateTime FirstSeenAtUtc => DateTimeOffset.FromUnixTimeSeconds(FirstSeenAt).UtcDateTime;

        /// <summary>
        ///     Gets the last updated time as DateTime.
        /// </summary>
        public DateTime LastUpdatedAtUtc => DateTimeOffset.FromUnixTimeSeconds(LastUpdatedAt).UtcDateTime;

        /// <summary>
        ///     Generates a FLAC key from filename and size.
        /// </summary>
        /// <param name="filename">The filename (will be normalized).</param>
        /// <param name="size">The file size in bytes.</param>
        /// <returns>64-bit truncated hash as hex string.</returns>
        public static string GenerateFlacKey(string filename, long size)
        {
            // Normalize filename: lowercase, extract just the filename part
            var normalized = System.IO.Path.GetFileName(filename)?.ToLowerInvariant() ?? filename.ToLowerInvariant();
            var input = $"{normalized}:{size}";

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));

            // Truncate to 64 bits (8 bytes) for compact keys
            return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        ///     Packs audio metadata into a single integer.
        /// </summary>
        /// <param name="sampleRate">Sample rate (e.g., 44100, 48000, 96000).</param>
        /// <param name="channels">Number of channels (1-8).</param>
        /// <param name="bitDepth">Bits per sample (16, 24, 32).</param>
        /// <returns>Packed metadata flags.</returns>
        public static int PackMetaFlags(int sampleRate, int channels, int bitDepth)
        {
            // Pack: bits 0-3 = channels, bits 4-7 = bitDepth/8, bits 8-23 = sampleRate/100
            return (channels & 0xF) |
                   ((bitDepth / 8) & 0xF) << 4 |
                   ((sampleRate / 100) & 0xFFFF) << 8;
        }

        /// <summary>
        ///     Unpacks audio metadata from flags.
        /// </summary>
        /// <param name="flags">Packed metadata flags.</param>
        /// <returns>Tuple of (sampleRate, channels, bitDepth).</returns>
        public static (int SampleRate, int Channels, int BitDepth) UnpackMetaFlags(int flags)
        {
            var channels = flags & 0xF;
            var bitDepth = ((flags >> 4) & 0xF) * 8;
            var sampleRate = ((flags >> 8) & 0xFFFF) * 100;
            return (sampleRate, channels, bitDepth);
        }
    }
}


