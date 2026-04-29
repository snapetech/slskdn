// <copyright file="IAudioFingerprintService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
        public string FingerprintData { get; init; } = string.Empty;

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
