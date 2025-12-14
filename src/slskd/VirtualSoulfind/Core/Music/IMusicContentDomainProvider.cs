// <copyright file="IMusicContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Music
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Common.Moderation;

    /// <summary>
    ///     Interface for the Music domain provider in VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     T-VC02: Music Domain Provider implementation.
    ///     This interface wraps music-specific identity and matching logic,
    ///     providing domain-neutral ContentWorkId/ContentItemId mappings.
    /// </remarks>
    public interface IMusicContentDomainProvider
    {
        /// <summary>
        ///     Attempts to resolve a work by MusicBrainz Release ID.
        /// </summary>
        /// <param name="releaseId">The MusicBrainz Release ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved work, or null if not found.</returns>
        Task<MusicWork?> TryGetWorkByReleaseIdAsync(string releaseId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Attempts to resolve a work by title and artist.
        /// </summary>
        /// <param name="title">The release title.</param>
        /// <param name="artist">The artist name.</param>
        /// <param name="year">Optional year for disambiguation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved work, or null if not found.</returns>
        Task<MusicWork?> TryGetWorkByTitleArtistAsync(string title, string artist, int? year = null, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Attempts to resolve an item by MusicBrainz Recording ID.
        /// </summary>
        /// <param name="recordingId">The MusicBrainz Recording ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved item, or null if not found.</returns>
        Task<MusicItem?> TryGetItemByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Attempts to resolve an item by local file metadata and tags.
        /// </summary>
        /// <param name="fileMetadata">The local file metadata.</param>
        /// <param name="tags">The extracted audio tags.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved item, or null if not found.</returns>
        Task<MusicItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, AudioTags tags, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Attempts to match a track by Chromaprint fingerprint.
        /// </summary>
        /// <param name="fingerprint">The Chromaprint fingerprint.</param>
        /// <param name="durationSeconds">The track duration in seconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matched item, or null if no match found.</returns>
        Task<MusicItem?> TryMatchTrackByFingerprintAsync(string fingerprint, int durationSeconds, CancellationToken cancellationToken = default);
    }
}
