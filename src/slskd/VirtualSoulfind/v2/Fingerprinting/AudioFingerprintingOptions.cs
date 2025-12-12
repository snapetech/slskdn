// <copyright file="AudioFingerprintingOptions.cs" company="slskd Team">
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
