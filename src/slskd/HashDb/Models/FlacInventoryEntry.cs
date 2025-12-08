// <copyright file="FlacInventoryEntry.cs" company="slskdn Team">
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
    ///     Hash status for a FLAC inventory entry.
    /// </summary>
    public enum HashStatus
    {
        /// <summary>Hash not yet determined.</summary>
        None,

        /// <summary>Hash is known and verified.</summary>
        Known,

        /// <summary>Hash verification is pending/in progress.</summary>
        Pending,

        /// <summary>Hash verification failed.</summary>
        Failed,
    }

    /// <summary>
    ///     Source of the hash value.
    /// </summary>
    public enum HashSource
    {
        /// <summary>Unknown source.</summary>
        Unknown,

        /// <summary>Hash obtained from local scan/download.</summary>
        LocalScan,

        /// <summary>Hash received from peer DHT.</summary>
        PeerDht,

        /// <summary>Hash obtained via backfill header sniffing.</summary>
        BackfillSniff,

        /// <summary>Hash received via mesh sync.</summary>
        MeshSync,
    }

    /// <summary>
    ///     Represents a FLAC file in the inventory.
    /// </summary>
    public class FlacInventoryEntry
    {
        /// <summary>
        ///     Gets or sets the file ID (sha256 of peer_id + path + size).
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        ///     Gets or sets the owner's username.
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        ///     Gets or sets the full remote path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        ///     Gets or sets the file size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets when this entry was discovered (Unix timestamp).
        /// </summary>
        public long DiscoveredAt { get; set; }

        /// <summary>
        ///     Gets or sets the hash status.
        /// </summary>
        public string HashStatusStr { get; set; } = "none";

        /// <summary>
        ///     Gets or sets the SHA256 hash of the first 32KB (for byte-identical verification).
        /// </summary>
        public string HashValue { get; set; }

        /// <summary>
        ///     Gets or sets the source of the hash.
        /// </summary>
        public string HashSourceStr { get; set; }

        /// <summary>
        ///     Gets or sets the FLAC audio MD5 (from STREAMINFO, for reference only).
        /// </summary>
        public string FlacAudioMd5 { get; set; }

        /// <summary>
        ///     Gets or sets the audio sample rate.
        /// </summary>
        public int? SampleRate { get; set; }

        /// <summary>
        ///     Gets or sets the number of channels.
        /// </summary>
        public int? Channels { get; set; }

        /// <summary>
        ///     Gets or sets the bits per sample.
        /// </summary>
        public int? BitDepth { get; set; }

        /// <summary>
        ///     Gets or sets the total duration in samples.
        /// </summary>
        public long? DurationSamples { get; set; }

        /// <summary>
        ///     Gets the hash status as enum.
        /// </summary>
        public HashStatus HashStatus => HashStatusStr?.ToLowerInvariant() switch
        {
            "known" => HashStatus.Known,
            "pending" => HashStatus.Pending,
            "failed" => HashStatus.Failed,
            _ => HashStatus.None,
        };

        /// <summary>
        ///     Gets the hash source as enum.
        /// </summary>
        public HashSource HashSource => HashSourceStr?.ToLowerInvariant() switch
        {
            "local_scan" => HashSource.LocalScan,
            "peer_dht" => HashSource.PeerDht,
            "backfill_sniff" => HashSource.BackfillSniff,
            "mesh_sync" => HashSource.MeshSync,
            _ => HashSource.Unknown,
        };

        /// <summary>
        ///     Gets the discovered time as DateTime.
        /// </summary>
        public DateTime DiscoveredAtUtc => DateTimeOffset.FromUnixTimeSeconds(DiscoveredAt).UtcDateTime;

        /// <summary>
        ///     Gets the duration in seconds (if available).
        /// </summary>
        public double? DurationSeconds => SampleRate > 0 && DurationSamples.HasValue
            ? (double)DurationSamples.Value / SampleRate.Value
            : null;

        /// <summary>
        ///     Generates the file ID from components.
        /// </summary>
        public static string GenerateFileId(string peerId, string path, long size)
        {
            var input = $"{peerId}|{path}|{size}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}


