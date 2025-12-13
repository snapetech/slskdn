namespace slskd.Audio
{
    using System;

    /// <summary>
    /// Computes quality scores for audio variants.
    /// </summary>
    public class QualityScorer
    {
        public double ComputeQualityScore(AudioVariant variant)
        {
            if (variant == null)
            {
                throw new ArgumentNullException(nameof(variant));
            }

            double score = 0.0;

            // Component 1: Codec fidelity (0.4 weight)
            score += 0.4 * ComputeCodecScore(variant);

            // Component 2: Sample rate plausibility (0.2 weight)
            score += 0.2 * ComputeSampleRateScore(variant);

            // Component 3: Bitrate adequacy (0.2 weight)
            score += 0.2 * ComputeBitrateScore(variant);

            // Component 4: Audio analysis (0.2 weight, if available)
            score += 0.2 * ComputeAnalysisScore(variant);

            return Math.Clamp(score, 0.0, 1.0);
        }

        private double ComputeCodecScore(AudioVariant v)
        {
            // Lossless codecs get full score
            if (v.Codec == "FLAC" && v.BitDepth >= 16) return 1.0;
            if (v.Codec == "ALAC" && v.BitDepth >= 16) return 0.98;
            if (v.Codec == "WAV" && v.BitDepth >= 16) return 0.95;

            // High-quality lossy
            if (v.Codec == "MP3" && v.BitrateKbps >= 320) return 0.75;
            if (v.Codec == "AAC" && v.BitrateKbps >= 256) return 0.73;
            if (v.Codec == "Opus" && v.BitrateKbps >= 160) return 0.70;
            if (v.Codec == "Vorbis" && v.BitrateKbps >= 256) return 0.68;

            // Low-quality lossy
            if (v.BitrateKbps >= 192) return 0.5;
            if (v.BitrateKbps >= 128) return 0.3;
            return 0.1;
        }

        private double ComputeSampleRateScore(AudioVariant v)
        {
            // Standard rates get full score
            if (v.SampleRateHz == 44100 || v.SampleRateHz == 48000) return 1.0;

            // High-res audio
            if (v.SampleRateHz == 88200 || v.SampleRateHz == 96000) return 0.95;
            if (v.SampleRateHz == 176400 || v.SampleRateHz == 192000) return 0.90;

            // Unusual/suspicious rates (possible upsampling)
            if (v.SampleRateHz == 22050 || v.SampleRateHz == 11025) return 0.4;

            return 0.5;  // Unknown rate
        }

        private double ComputeBitrateScore(AudioVariant v)
        {
            // For lossless, check if bitrate is reasonable for format
            if (v.Codec == "FLAC")
            {
                var expectedMin = EstimateFLACBitrate(v.SampleRateHz, v.BitDepth ?? 16, v.Channels);
                if (v.BitrateKbps < expectedMin * 0.5)
                {
                    // Suspiciously low bitrate for lossless
                    return 0.3;
                }

                return 1.0;
            }

            // For lossy, higher bitrate is better (up to a point)
            if (v.BitrateKbps >= 320) return 1.0;
            if (v.BitrateKbps >= 256) return 0.9;
            if (v.BitrateKbps >= 192) return 0.7;
            if (v.BitrateKbps >= 128) return 0.5;
            return 0.2;
        }

        private double ComputeAnalysisScore(AudioVariant v)
        {
            if (!v.DynamicRangeDR.HasValue) return 0.5;  // No data, neutral

            double score = 0.5;

            // Good dynamic range
            if (v.DynamicRangeDR >= 10) score += 0.3;
            else if (v.DynamicRangeDR >= 7) score += 0.1;
            else score -= 0.2;  // Poor DR suggests over-compression or transcode

            // Clipping penalty
            if (v.HasClipping == true) score -= 0.3;

            return Math.Clamp(score, 0.0, 1.0);
        }

        private int EstimateFLACBitrate(int sampleRate, int bitDepth, int channels)
        {
            // FLAC typically compresses to 50-60% of raw PCM
            var rawBitrate = (sampleRate * bitDepth * channels) / 1000;
            return (int)(rawBitrate * 0.55);
        }
    }
}

















