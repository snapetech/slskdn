// <copyright file="MusicDomainMapping.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.Core.Music
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Utilities for mapping between MusicBrainz identifiers and domain-neutral Content IDs.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This provides deterministic, bidirectional mapping between:
    ///         - MusicBrainz Release IDs → ContentWorkId
    ///         - MusicBrainz Recording IDs → ContentItemId
    ///     </para>
    ///     <para>
    ///         The mapping uses a namespace-based UUID v5 approach to ensure:
    ///         - Same MBID always produces same ContentId (deterministic)
    ///         - Different MBIDs produce different ContentIds (no collisions)
    ///         - Reverse mapping is possible (store MBID separately, use as key)
    ///     </para>
    /// </remarks>
    public static class MusicDomainMapping
    {
        // Namespace UUID for MusicBrainz Release IDs (v5 UUID namespace)
        private static readonly Guid ReleaseNamespace = new Guid("a3c5f8d2-4e1b-5a7c-9f2e-3d6b8c1a4f5e");

        // Namespace UUID for MusicBrainz Recording IDs
        private static readonly Guid RecordingNamespace = new Guid("b7d9e3f1-6c2a-4b8d-a5f3-7e9c1d4b6a8f");

        /// <summary>
        ///     Converts a MusicBrainz Release ID to a <see cref="ContentWorkId"/>.
        /// </summary>
        /// <param name="releaseId">The MusicBrainz Release ID (GUID format).</param>
        /// <returns>A deterministic <see cref="ContentWorkId"/>.</returns>
        /// <exception cref="ArgumentException">If the release ID is invalid.</exception>
        public static ContentWorkId ReleaseIdToContentWorkId(string releaseId)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
            {
                throw new ArgumentException("Release ID cannot be null or empty.", nameof(releaseId));
            }

            // Validate it's a valid GUID
            if (!Guid.TryParse(releaseId, out var mbid))
            {
                throw new ArgumentException($"Invalid MusicBrainz Release ID format: {releaseId}", nameof(releaseId));
            }

            // Generate deterministic UUID v5 (namespace + name)
            var deterministicGuid = GenerateUuidV5(ReleaseNamespace, releaseId);
            return new ContentWorkId(deterministicGuid);
        }

        /// <summary>
        ///     Converts a MusicBrainz Recording ID to a <see cref="ContentItemId"/>.
        /// </summary>
        /// <param name="recordingId">The MusicBrainz Recording ID (GUID format).</param>
        /// <returns>A deterministic <see cref="ContentItemId"/>.</returns>
        /// <exception cref="ArgumentException">If the recording ID is invalid.</exception>
        public static ContentItemId RecordingIdToContentItemId(string recordingId)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                throw new ArgumentException("Recording ID cannot be null or empty.", nameof(recordingId));
            }

            // Validate it's a valid GUID
            if (!Guid.TryParse(recordingId, out var mbid))
            {
                throw new ArgumentException($"Invalid MusicBrainz Recording ID format: {recordingId}", nameof(recordingId));
            }

            // Generate deterministic UUID v5 (namespace + name)
            var deterministicGuid = GenerateUuidV5(RecordingNamespace, recordingId);
            return new ContentItemId(deterministicGuid);
        }

        /// <summary>
        ///     Generates a UUID v5 (namespace + name) for deterministic ID generation.
        /// </summary>
        /// <param name="namespaceId">The namespace UUID.</param>
        /// <param name="name">The name (MBID) to hash.</param>
        /// <returns>A deterministic UUID.</returns>
        /// <remarks>
        ///     This implements RFC 4122 UUID v5 (SHA-1 based).
        ///     Reference: https://tools.ietf.org/html/rfc4122#section-4.3
        /// </remarks>
        private static Guid GenerateUuidV5(Guid namespaceId, string name)
        {
            // Convert namespace to bytes (big-endian)
            var namespaceBytes = namespaceId.ToByteArray();
            SwapByteOrder(namespaceBytes);

            // Convert name to UTF-8 bytes
            var nameBytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());

            // Concatenate namespace + name
            var combined = new byte[namespaceBytes.Length + nameBytes.Length];
            Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);

            // Hash with SHA-1
            byte[] hash;
            using (var sha1 = SHA1.Create())
            {
                hash = sha1.ComputeHash(combined);
            }

            // Take first 16 bytes for UUID
            var uuidBytes = new byte[16];
            Array.Copy(hash, 0, uuidBytes, 0, 16);

            // Set version (v5 = 0101) and variant (10xx) bits per RFC 4122
            uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | 0x50); // Version 5
            uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80); // Variant 10xx

            // Convert back to big-endian for Guid constructor
            SwapByteOrder(uuidBytes);

            return new Guid(uuidBytes);
        }

        /// <summary>
        ///     Swaps byte order for Guid conversion (handles big-endian/little-endian).
        /// </summary>
        private static void SwapByteOrder(byte[] bytes)
        {
            // Swap bytes for Data1 (first 4 bytes)
            Array.Reverse(bytes, 0, 4);
            // Swap bytes for Data2 (next 2 bytes)
            Array.Reverse(bytes, 4, 2);
            // Swap bytes for Data3 (next 2 bytes)
            Array.Reverse(bytes, 6, 2);
            // Data4 (last 8 bytes) stay in network byte order
        }
    }
}
