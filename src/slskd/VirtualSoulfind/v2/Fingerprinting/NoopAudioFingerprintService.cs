// <copyright file="NoopAudioFingerprintService.cs" company="slskd Team">
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
    ///     No-op implementation of <see cref="IAudioFingerprintService"/>.
    /// </summary>
    /// <remarks>
    ///     Used when fingerprinting is disabled or not available.
    ///     Future: Integrate with Chromaprint/fpcalc for actual fingerprinting.
    /// </remarks>
    public sealed class NoopAudioFingerprintService : IAudioFingerprintService
    {
        /// <summary>
        ///     Always returns null (no fingerprint computed).
        /// </summary>
        public Task<AudioFingerprint?> ComputeFingerprintAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AudioFingerprint?>(null);
        }

        /// <summary>
        ///     Always returns 0.0 (no similarity).
        /// </summary>
        public float CompareFingerprintsSimilarity(AudioFingerprint fingerprint1, AudioFingerprint fingerprint2)
        {
            return 0.0f;
        }
    }
}
