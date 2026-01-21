// <copyright file="IAudioFingerprintService.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Fingerprinting
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for computing and comparing audio fingerprints (Chromaprint/AcoustID).
    /// </summary>
    public interface IAudioFingerprintService
    {
        /// <summary>
        ///     Compute audio fingerprint for a file.
        /// </summary>
        /// <param name="filePath">Path to audio file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Fingerprint data, or null if unable to compute.</returns>
        Task<AudioFingerprint?> ComputeFingerprintAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Compare two fingerprints and return similarity score.
        /// </summary>
        /// <param name="fingerprint1">First fingerprint.</param>
        /// <param name="fingerprint2">Second fingerprint.</param>
        /// <returns>Similarity score (0.0 - 1.0), where 1.0 is identical.</returns>
        float CompareFingerprintsSimilarity(AudioFingerprint fingerprint1, AudioFingerprint fingerprint2);
    }

    /// <summary>
    ///     Audio fingerprint data.
    /// </summary>
    public sealed class AudioFingerprint
    {
        /// <summary>
        ///     Gets or initializes the raw fingerprint data (Chromaprint format).
        /// </summary>
        public string FingerprintData { get; init; }

        /// <summary>
        ///     Gets or initializes the duration of the audio in seconds.
        /// </summary>
        public int DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the AcoustID (if looked up).
        /// </summary>
        public string? AcoustId { get; init; }
    }
}
