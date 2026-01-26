// <copyright file="IMetadataFacade.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MetadataFacade
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Single facade over MusicBrainz, AcoustID, file tags, and Soulseek metadata.
    /// </summary>
    /// <remarks>
    ///     T-912: Centralizes MB/tag caching and adapter fallback. Order of fallback is policy.
    /// </remarks>
    public interface IMetadataFacade
    {
        /// <summary>
        ///     Gets metadata by MusicBrainz recording ID. Uses MusicBrainz adapter.
        /// </summary>
        Task<MetadataResult?> GetByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets metadata by AcoustID fingerprint (Chromaprint). Uses AcoustID lookup then MusicBrainz for full metadata.
        /// </summary>
        Task<MetadataResult?> GetByFingerprintAsync(string fingerprint, int sampleRate, int durationSeconds, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets metadata from a local file. Tries file tags first; optionally fingerprint→AcoustID→MB if MBIDs missing.
        /// </summary>
        Task<MetadataResult?> GetByFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Searches for recordings (e.g. by artist/title). Uses MusicBrainz search when available.
        /// </summary>
        IAsyncEnumerable<MetadataResult> SearchAsync(string query, int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets best-effort metadata from a Soulseek filename (e.g. "Artist - Title.mp3").
        ///     T-912 Soulseek adapter: parsing only; no network call. Use when only username and filename are available.
        /// </summary>
        Task<MetadataResult?> GetBySoulseekFilenameAsync(string username, string filename, CancellationToken cancellationToken = default);
    }
}
