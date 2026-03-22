// <copyright file="AudioVariant.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Audio
{
    using System;

    /// <summary>
    /// Represents a specific encoded variant of a recording.
    /// </summary>
    public class AudioVariant
    {
        // Identity
        public string VariantId { get; set; } = string.Empty;
        public string MusicBrainzRecordingId { get; set; } = string.Empty;
        public string FlacKey { get; set; } = string.Empty;

        // Technical Properties
        public string Codec { get; set; } = string.Empty;
        public string Container { get; set; } = string.Empty;
        public int SampleRateHz { get; set; }
        public int? BitDepth { get; set; }
        public int Channels { get; set; }
        public int DurationMs { get; set; }
        public int BitrateKbps { get; set; }
        public long FileSizeBytes { get; set; }

        // Content Integrity
        public string FileSha256 { get; set; } = string.Empty;
        public string AudioFingerprint { get; set; } = string.Empty;
        public string AudioSketchHash { get; set; } = string.Empty;

        // Quality Assessment
        public double QualityScore { get; set; }
        public bool TranscodeSuspect { get; set; }
        public string TranscodeReason { get; set; } = string.Empty;
        public string AnalyzerVersion { get; set; } = string.Empty;

        // Audio Analysis (optional, computed on-demand)
        public double? DynamicRangeDR { get; set; }
        public double? LoudnessLUFS { get; set; }
        public bool? HasClipping { get; set; }
        public string EncoderSignature { get; set; } = string.Empty;

        // FLAC-specific fingerprints and streaminfo
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

        // Shared lossy spectral features (MP3/Opus/AAC)
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

        // Provenance
        public DateTimeOffset FirstSeenAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int SeenCount { get; set; }
    }
}
