namespace slskd.Audio
{
    using System;

    /// <summary>
    /// Heuristics for lossy-to-lossless transcode detection.
    /// </summary>
    public class TranscodeDetector
    {
        public (bool isSuspect, string reason) DetectTranscode(AudioVariant variant)
        {
            if (variant == null)
            {
                throw new ArgumentNullException(nameof(variant));
            }

            // Rule 1: FLAC with impossibly low bitrate
            if (variant.Codec == "FLAC")
            {
                var minExpected = EstimateMinFLACBitrate(variant);
                if (variant.BitrateKbps < minExpected)
                {
                    return (true, $"FLAC bitrate {variant.BitrateKbps}kbps too low for {variant.SampleRateHz}Hz/{variant.BitDepth}bit (expected ≥{minExpected}kbps)");
                }
            }

            // Rule 2: Lossless with poor dynamic range
            if (IsLosslessCodec(variant.Codec) && variant.DynamicRangeDR.HasValue)
            {
                if (variant.DynamicRangeDR < 5)
                {
                    return (true, $"Lossless file with DR={variant.DynamicRangeDR:F1} suggests lossy source");
                }
            }

            // Rule 3: Upsampled audio (48kHz or 96kHz from 44.1kHz source)
            if (variant.SampleRateHz == 48000 || variant.SampleRateHz == 96000)
            {
                // Check encoder signature for clues
                if (variant.EncoderSignature != null &&
                    variant.EncoderSignature.Contains("LAME", StringComparison.OrdinalIgnoreCase))  // MP3 encoder in FLAC
                {
                    return (true, "Lossy encoder signature in lossless file");
                }
            }

            // Rule 4: Spectral analysis (approximation)
            if (variant.BitrateKbps > 0)
            {
                var effectiveBandwidth = EstimateSpectralBandwidth(variant);
                var expectedBandwidth = variant.SampleRateHz / 2.0;

                if (effectiveBandwidth < expectedBandwidth * 0.6)
                {
                    return (true, $"Spectral bandwidth {effectiveBandwidth / 1000:F1}kHz suggests {EstimateOriginalLossyBitrate(effectiveBandwidth)}kbps lossy source");
                }
            }

            return (false, null);
        }

        private double EstimateSpectralBandwidth(AudioVariant variant)
        {
            // Placeholder: estimate from bitrate for lossy codecs
            return variant.Codec switch
            {
                "MP3" => variant.BitrateKbps switch
                {
                    >= 320 => 20000,
                    >= 192 => 18000,
                    >= 128 => 16000,
                    _ => 14000,
                },
                _ => variant.SampleRateHz / 2.0,  // Nyquist frequency
            };
        }

        private int EstimateOriginalLossyBitrate(double bandwidth)
        {
            if (bandwidth >= 19000) return 320;
            if (bandwidth >= 17000) return 192;
            if (bandwidth >= 15000) return 160;
            return 128;
        }

        private int EstimateMinFLACBitrate(AudioVariant variant)
        {
            // Rough lower-bound for lossless bitrate (50% of raw PCM)
            var raw = (variant.SampleRateHz * (variant.BitDepth ?? 16) * variant.Channels) / 1000;
            return (int)(raw * 0.5);
        }

        private static bool IsLosslessCodec(string codec)
        {
            return codec switch
            {
                "FLAC" => true,
                "ALAC" => true,
                "WAV" => true,
                "AIFF" => true,
                _ => false,
            };
        }
    }
}

