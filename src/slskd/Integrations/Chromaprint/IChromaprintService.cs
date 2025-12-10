// <copyright file="IChromaprintService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.Chromaprint
{
    using System;

    /// <summary>
    ///     Provides access to the Chromaprint fingerprinting library.
    /// </summary>
    public interface IChromaprintService
    {
        /// <summary>
        ///     Generates a Chromaprint fingerprint for the supplied PCM samples.
        /// </summary>
        /// <param name="samples">Raw PCM samples (16-bit signed).</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <returns>Chromaprint fingerprint string.</returns>
        string GenerateFingerprint(ReadOnlySpan<short> samples, int sampleRate, int channels);
    }
}

