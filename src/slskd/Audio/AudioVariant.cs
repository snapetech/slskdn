namespace slskd.Audio
{
    using System;

    /// <summary>
    /// Represents a specific encoded variant of a recording.
    /// </summary>
    public class AudioVariant
    {
        // Identity
        public string VariantId { get; set; }
        public string MusicBrainzRecordingId { get; set; }
        public string FlacKey { get; set; }

        // Technical Properties
        public string Codec { get; set; }
        public string Container { get; set; }
        public int SampleRateHz { get; set; }
        public int? BitDepth { get; set; }
        public int Channels { get; set; }
        public int DurationMs { get; set; }
        public int BitrateKbps { get; set; }
        public long FileSizeBytes { get; set; }

        // Content Integrity
        public string FileSha256 { get; set; }
        public string AudioFingerprint { get; set; }

        // Quality Assessment
        public double QualityScore { get; set; }
        public bool TranscodeSuspect { get; set; }
        public string TranscodeReason { get; set; }

        // Audio Analysis (optional, computed on-demand)
        public double? DynamicRangeDR { get; set; }
        public double? LoudnessLUFS { get; set; }
        public bool? HasClipping { get; set; }
        public string EncoderSignature { get; set; }

        // Provenance
        public DateTimeOffset FirstSeenAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int SeenCount { get; set; }
    }
}
