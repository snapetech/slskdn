// <copyright file="HashDbEntry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
        public string FlacKey { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the SHA256 hash of the first 32KB bytes.
        ///     This is used for byte-identical verification in multi-source downloads.
        /// </summary>
        public string ByteHash { get; set; } = string.Empty;

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
        ///     Gets or sets the SHA256 hash of the entire file (for complete post-download verification).
        /// </summary>
        public string FullFileHash { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the audio fingerprint (AcoustID/Chromaprint) for future music identification.
        /// </summary>
        public string AudioFingerprint { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the MusicBrainz recording ID for future music identification.
        /// </summary>
        public string MusicBrainzId { get; set; } = string.Empty;

        // Variant metadata (canonical scoring)
        public string VariantId { get; set; } = string.Empty;
        public string Codec { get; set; } = string.Empty;
        public string Container { get; set; } = string.Empty;
        public int? SampleRateHz { get; set; }
        public int? BitDepth { get; set; }
        public int? Channels { get; set; }
        public int? DurationMs { get; set; }
        public int? BitrateKbps { get; set; }
        public double? QualityScore { get; set; }
        public bool? TranscodeSuspect { get; set; }
        public string TranscodeReason { get; set; } = string.Empty;
        public double? DynamicRangeDr { get; set; }
        public double? LoudnessLufs { get; set; }
        public bool? HasClipping { get; set; }
        public string EncoderSignature { get; set; } = string.Empty;
        public int? SeenCount { get; set; }
        public string FileSha256 { get; set; } = string.Empty;
        public string AudioSketchHash { get; set; } = string.Empty;
        public string AnalyzerVersion { get; set; } = string.Empty;

        // FLAC-specific
        public string FlacStreamInfoHash42 { get; set; } = string.Empty;
        public string FlacPcmMd5 { get; set; } = string.Empty;
        public int? FlacMinBlockSize { get; set; }
        public int? FlacMaxBlockSize { get; set; }
        public int? FlacMinFrameSize { get; set; }
        public int? FlacMaxFrameSize { get; set; }
        public long? FlacTotalSamples { get; set; }

        // MP3-specific
        public string Mp3StreamHash { get; set; } = string.Empty;
        public string Mp3Encoder { get; set; } = string.Empty;
        public string Mp3EncoderPreset { get; set; } = string.Empty;
        public int? Mp3FramesAnalyzed { get; set; }

        // Shared lossy spectral features
        public double? EffectiveBandwidthHz { get; set; }
        public double? NominalLowpassHz { get; set; }
        public double? SpectralFlatnessScore { get; set; }
        public double? HfEnergyRatio { get; set; }

        // Opus-specific
        public string OpusStreamHash { get; set; } = string.Empty;
        public int? OpusNominalBitrateKbps { get; set; }
        public string OpusApplication { get; set; } = string.Empty;
        public string OpusBandwidthMode { get; set; } = string.Empty;

        // AAC-specific
        public string AacStreamHash { get; set; } = string.Empty;
        public string AacProfile { get; set; } = string.Empty;
        public bool? AacSbrPresent { get; set; }
        public bool? AacPsPresent { get; set; }
        public int? AacNominalBitrateKbps { get; set; }

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
