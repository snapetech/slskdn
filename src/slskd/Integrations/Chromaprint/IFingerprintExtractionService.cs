// <copyright file="IFingerprintExtractionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.Chromaprint
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Extracts audio fingerprints using Chromaprint.
    /// </summary>
    public interface IFingerprintExtractionService
    {
        /// <summary>
        ///     Extracts a Chromaprint fingerprint from the supplied file.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The fingerprint string, or <c>null</c> if Chromaprint is disabled.</returns>
        Task<string?> ExtractFingerprintAsync(string filePath, CancellationToken cancellationToken = default);
    }
}

