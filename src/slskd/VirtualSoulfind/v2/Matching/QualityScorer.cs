// <copyright file="QualityScorer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Matching
{
    /// <summary>
    ///     Scores the quality of audio files.
    /// </summary>
    public static class QualityScorer
    {
        /// <summary>
        ///     Scores a music file's quality (0-100).
        /// </summary>
        public static int ScoreMusicQuality(string extension, long size, int? bitrate = null)
        {
            var score = 50; // Base score

            // Format scoring
            score += extension.ToLowerInvariant() switch
            {
                ".flac" => 30,
                ".ape" => 28,
                ".wav" => 25,
                ".alac" => 27,
                ".m4a" => 15,
                ".mp3" => 10,
                ".ogg" => 12,
                ".opus" => 14,
                _ => 0,
            };

            // Bitrate scoring (if available)
            if (bitrate.HasValue)
            {
                score += bitrate.Value switch
                {
                    >= 320 => 20,
                    >= 256 => 15,
                    >= 192 => 10,
                    >= 128 => 5,
                    _ => 0,
                };
            }

            return System.Math.Min(score, 100);
        }
    }
}
