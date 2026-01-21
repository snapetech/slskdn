// <copyright file="QualityScorer.cs" company="slskd Team">
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
