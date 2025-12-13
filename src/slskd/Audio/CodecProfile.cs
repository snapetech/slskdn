namespace slskd.Audio
{
    using System;
    using System.IO;
    using System.Linq;
    using TagLib;

    /// <summary>
    /// Standardized codec profile for grouping variants.
    /// </summary>
    public class CodecProfile
    {
        public string Codec { get; set; }
        public bool IsLossless { get; set; }
        public int SampleRateHz { get; set; }
        public int? BitDepth { get; set; }
        public int Channels { get; set; }

        /// <summary>
        /// Generate a canonical string key for this profile.
        /// </summary>
        public string ToKey()
        {
            if (IsLossless && BitDepth.HasValue)
            {
                return $"{Codec}-{BitDepth}bit-{SampleRateHz}Hz-{Channels}ch";
            }

            return $"{Codec}-lossy-{SampleRateHz}Hz-{Channels}ch";
        }

        /// <summary>
        /// Parse audio file and derive codec profile.
        /// </summary>
        public static CodecProfile FromFile(string filePath)
        {
            using var file = TagLib.File.Create(filePath);
            var props = file.Properties;

            return new CodecProfile
            {
                Codec = GetCodecName(props, Path.GetExtension(filePath)),
                IsLossless = IsLosslessCodec(props, Path.GetExtension(filePath)),
                SampleRateHz = props.AudioSampleRate,
                BitDepth = GetBitDepth(props),
                Channels = props.AudioChannels,
            };
        }

        /// <summary>
        /// Derive profile from an existing variant.
        /// </summary>
        public static CodecProfile FromVariant(AudioVariant variant)
        {
            if (variant == null)
            {
                throw new ArgumentNullException(nameof(variant));
            }

            return new CodecProfile
            {
                Codec = variant.Codec,
                IsLossless = IsLosslessCodec(variant.Codec),
                SampleRateHz = variant.SampleRateHz,
                BitDepth = variant.BitDepth,
                Channels = variant.Channels,
            };
        }

        private static string GetCodecName(Properties props, string extension)
        {
            // Prefer explicit container/extension mapping; fall back to first codec description.
            switch (extension?.ToLowerInvariant())
            {
                case ".flac": return "FLAC";
                case ".alac":
                case ".m4a": return "ALAC";
                case ".aac": return "AAC";
                case ".mp3": return "MP3";
                case ".opus": return "Opus";
                case ".ogg": return "Vorbis";
                case ".wav": return "WAV";
                default:
                    var desc = props.Codecs.FirstOrDefault()?.Description;
                    return string.IsNullOrWhiteSpace(desc) ? "Unknown" : desc;
            }
        }

        private static bool IsLosslessCodec(Properties props, string extension)
        {
            return IsLosslessCodec(GetCodecName(props, extension));
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

        private static int? GetBitDepth(Properties props)
        {
            // TagLib may return 0 for lossy codecs; treat 0 as null.
            return props.BitsPerSample == 0 ? null : props.BitsPerSample;
        }
    }
}

















