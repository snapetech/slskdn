// <copyright file="NoopAudioFingerprintService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
