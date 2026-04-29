// <copyright file="AudioFingerprintingOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Fingerprinting
{
    /// <summary>
    ///     Configuration for audio fingerprinting.
    /// </summary>
    public sealed class AudioFingerprintingOptions
    {
        /// <summary>
        ///     Enable audio fingerprinting (Chromaprint).
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Path to fpcalc executable (Chromaprint).
        /// </summary>
        /// <remarks>
        ///     If null, will attempt to find in PATH.
        /// </remarks>
        public string? FpcalcPath { get; init; }

        /// <summary>
        ///     Minimum similarity threshold for fingerprint matching (0.0 - 1.0).
        /// </summary>
        public float MinimumSimilarity { get; init; } = 0.85f;

        /// <summary>
        ///     Timeout for fingerprint computation (seconds).
        /// </summary>
        public int ComputeTimeoutSeconds { get; init; } = 30;
    }
}
