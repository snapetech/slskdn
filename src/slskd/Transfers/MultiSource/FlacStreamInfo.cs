// <copyright file="FlacStreamInfo.cs" company="slskdn Team">
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

namespace slskd.Transfers.MultiSource
{
    using System;

    /// <summary>
    ///     Parsed FLAC STREAMINFO metadata block.
    /// </summary>
    public class FlacStreamInfo
    {
        /// <summary>
        ///     Gets or sets the minimum block size in samples.
        /// </summary>
        public int MinBlockSize { get; set; }

        /// <summary>
        ///     Gets or sets the maximum block size in samples.
        /// </summary>
        public int MaxBlockSize { get; set; }

        /// <summary>
        ///     Gets or sets the minimum frame size in bytes.
        /// </summary>
        public int MinFrameSize { get; set; }

        /// <summary>
        ///     Gets or sets the maximum frame size in bytes.
        /// </summary>
        public int MaxFrameSize { get; set; }

        /// <summary>
        ///     Gets or sets the sample rate in Hz.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        ///     Gets or sets the number of channels.
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        ///     Gets or sets the bits per sample.
        /// </summary>
        public int BitsPerSample { get; set; }

        /// <summary>
        ///     Gets or sets the total number of samples.
        /// </summary>
        public long TotalSamples { get; set; }

        /// <summary>
        ///     Gets or sets the MD5 signature of the unencoded audio data.
        /// </summary>
        public byte[] AudioMd5 { get; set; }

        /// <summary>
        ///     Gets the MD5 signature as a hex string.
        /// </summary>
        public string AudioMd5Hex => AudioMd5 != null ? BitConverter.ToString(AudioMd5).Replace("-", string.Empty).ToLowerInvariant() : null;
    }

    /// <summary>
    ///     Parser for FLAC STREAMINFO metadata blocks.
    /// </summary>
    public static class FlacStreamInfoParser
    {
        /// <summary>
        ///     The FLAC magic number "fLaC".
        /// </summary>
        public static readonly byte[] FlacMagic = { 0x66, 0x4C, 0x61, 0x43 }; // "fLaC"

        /// <summary>
        ///     Minimum bytes needed to parse STREAMINFO (4 magic + 4 header + 34 streaminfo).
        /// </summary>
        public const int MinimumBytesNeeded = 42;

        /// <summary>
        ///     Attempts to parse FLAC STREAMINFO from the given bytes.
        /// </summary>
        /// <param name="data">The first 42+ bytes of a FLAC file.</param>
        /// <param name="streamInfo">The parsed STREAMINFO if successful.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        public static bool TryParse(byte[] data, out FlacStreamInfo streamInfo)
        {
            streamInfo = null;

            if (data == null || data.Length < MinimumBytesNeeded)
            {
                return false;
            }

            // Check magic number "fLaC"
            if (data[0] != FlacMagic[0] || data[1] != FlacMagic[1] ||
                data[2] != FlacMagic[2] || data[3] != FlacMagic[3])
            {
                return false;
            }

            // Byte 4: METADATA_BLOCK_HEADER
            // Bit 7: Last-metadata-block flag
            // Bits 6-0: Block type (0 = STREAMINFO)
            var blockType = data[4] & 0x7F;
            if (blockType != 0)
            {
                // First block should always be STREAMINFO
                return false;
            }

            // Bytes 5-7: Length of metadata block (24 bits, big-endian)
            var blockLength = (data[5] << 16) | (data[6] << 8) | data[7];
            if (blockLength != 34)
            {
                // STREAMINFO is always exactly 34 bytes
                return false;
            }

            // Parse STREAMINFO (34 bytes starting at offset 8)
            // Bytes 0-1: Minimum block size (16 bits)
            // Bytes 2-3: Maximum block size (16 bits)
            // Bytes 4-6: Minimum frame size (24 bits)
            // Bytes 7-9: Maximum frame size (24 bits)
            // Bytes 10-13 + bits: Sample rate (20 bits), channels (3 bits), bps (5 bits), total samples (36 bits)
            // Bytes 18-33: MD5 signature (128 bits / 16 bytes)

            var offset = 8;

            streamInfo = new FlacStreamInfo
            {
                MinBlockSize = (data[offset] << 8) | data[offset + 1],
                MaxBlockSize = (data[offset + 2] << 8) | data[offset + 3],
                MinFrameSize = (data[offset + 4] << 16) | (data[offset + 5] << 8) | data[offset + 6],
                MaxFrameSize = (data[offset + 7] << 16) | (data[offset + 8] << 8) | data[offset + 9],
            };

            // Bytes 10-13 contain sample rate, channels, bps packed together
            // Sample rate: 20 bits (bytes 10-11 + upper 4 bits of byte 12)
            // Channels: 3 bits (bits 3-1 of byte 12) + 1
            // Bits per sample: 5 bits (bit 0 of byte 12 + upper 4 bits of byte 13) + 1
            // Total samples: 36 bits (lower 4 bits of byte 13 + bytes 14-17)

            streamInfo.SampleRate = (data[offset + 10] << 12) | (data[offset + 11] << 4) | (data[offset + 12] >> 4);
            streamInfo.Channels = ((data[offset + 12] >> 1) & 0x07) + 1;
            streamInfo.BitsPerSample = (((data[offset + 12] & 0x01) << 4) | (data[offset + 13] >> 4)) + 1;
            streamInfo.TotalSamples = ((long)(data[offset + 13] & 0x0F) << 32) |
                                      ((long)data[offset + 14] << 24) |
                                      ((long)data[offset + 15] << 16) |
                                      ((long)data[offset + 16] << 8) |
                                      data[offset + 17];

            // MD5 signature: bytes 18-33 (16 bytes)
            streamInfo.AudioMd5 = new byte[16];
            Array.Copy(data, offset + 18, streamInfo.AudioMd5, 0, 16);

            return true;
        }

        /// <summary>
        ///     Checks if the given data starts with the FLAC magic number.
        /// </summary>
        /// <param name="data">The data to check.</param>
        /// <returns>True if this is a FLAC file.</returns>
        public static bool IsFlac(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return false;
            }

            return data[0] == FlacMagic[0] && data[1] == FlacMagic[1] &&
                   data[2] == FlacMagic[2] && data[3] == FlacMagic[3];
        }
    }
}
