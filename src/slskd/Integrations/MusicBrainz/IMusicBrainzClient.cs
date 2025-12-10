// <copyright file="IMusicBrainzClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Integrations.MusicBrainz.Models;

    /// <summary>
    ///     Client for querying MusicBrainz release and recording metadata.
    /// </summary>
    public interface IMusicBrainzClient
    {
        /// <summary>
        ///     Loads metadata for a MusicBrainz release.
        /// </summary>
        /// <param name="releaseId">The MusicBrainz release identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An album target or <c>null</c> if the release was not found.</returns>
        Task<AlbumTarget?> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Loads metadata for a MusicBrainz recording.
        /// </summary>
        /// <param name="recordingId">The MusicBrainz recording identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A track target or <c>null</c> if the recording was not found.</returns>
        Task<TrackTarget?> GetRecordingAsync(string recordingId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Resolves a release using a Discogs release/master identifier.
        /// </summary>
        Task<AlbumTarget?> GetReleaseByDiscogsReleaseIdAsync(string discogsReleaseId, CancellationToken cancellationToken = default);
    }
}


