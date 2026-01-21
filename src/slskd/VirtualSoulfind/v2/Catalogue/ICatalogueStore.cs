// <copyright file="ICatalogueStore.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for the virtual catalogue store (metadata brain of VirtualSoulfind).
    /// </summary>
    /// <remarks>
    ///     The catalogue store is the "offline first" metadata layer that allows:
    ///     - Browsing artists/releases/tracks without network calls
    ///     - Planning acquisitions based on canonical metadata
    ///     - Matching local files to catalogue entries
    ///     
    ///     This is separate from:
    ///     - Source registry (where to get content)
    ///     - Intent queue (what we want to fetch)
    ///     - Local library (files on disk)
    /// </remarks>
    public interface ICatalogueStore : IDisposable
    {
        // ========== Artist Methods ==========

        /// <summary>
        ///     Finds an artist by internal ID.
        /// </summary>
        Task<Artist?> FindArtistByIdAsync(string artistId, CancellationToken ct = default);

        /// <summary>
        ///     Finds an artist by MusicBrainz ID.
        /// </summary>
        Task<Artist?> FindArtistByMBIDAsync(string mbid, CancellationToken ct = default);

        /// <summary>
        ///     Searches artists by name (case-insensitive, partial match).
        /// </summary>
        Task<IReadOnlyList<Artist>> SearchArtistsAsync(string query, int limit = 50, CancellationToken ct = default);

        /// <summary>
        ///     Inserts or updates an artist.
        /// </summary>
        Task UpsertArtistAsync(Artist artist, CancellationToken ct = default);

        // ========== Release Group Methods ==========

        /// <summary>
        ///     Finds a release group by internal ID.
        /// </summary>
        Task<ReleaseGroup?> FindReleaseGroupByIdAsync(string releaseGroupId, CancellationToken ct = default);

        /// <summary>
        ///     Finds a release group by MusicBrainz ID.
        /// </summary>
        Task<ReleaseGroup?> FindReleaseGroupByMBIDAsync(string mbid, CancellationToken ct = default);

        /// <summary>
        ///     Lists release groups for an artist.
        /// </summary>
        Task<IReadOnlyList<ReleaseGroup>> ListReleaseGroupsForArtistAsync(string artistId, CancellationToken ct = default);

        /// <summary>
        ///     Inserts or updates a release group.
        /// </summary>
        Task UpsertReleaseGroupAsync(ReleaseGroup releaseGroup, CancellationToken ct = default);

        // ========== Release Methods ==========

        /// <summary>
        ///     Finds a release by internal ID.
        /// </summary>
        Task<Release?> FindReleaseByIdAsync(string releaseId, CancellationToken ct = default);

        /// <summary>
        ///     Finds a release by MusicBrainz ID.
        /// </summary>
        Task<Release?> FindReleaseByMBIDAsync(string mbid, CancellationToken ct = default);

        /// <summary>
        ///     Lists releases for a release group.
        /// </summary>
        Task<IReadOnlyList<Release>> ListReleasesForReleaseGroupAsync(string releaseGroupId, CancellationToken ct = default);

        /// <summary>
        ///     Inserts or updates a release.
        /// </summary>
        Task UpsertReleaseAsync(Release release, CancellationToken ct = default);

        // ========== Track Methods ==========

        /// <summary>
        ///     Finds a track by internal ID.
        /// </summary>
        Task<Track?> FindTrackByIdAsync(string trackId, CancellationToken ct = default);

        /// <summary>
        ///     Finds a track by MusicBrainz recording ID.
        /// </summary>
        Task<Track?> FindTrackByMBIDAsync(string mbid, CancellationToken ct = default);

        /// <summary>
        ///     Lists tracks for a release.
        /// </summary>
        Task<IReadOnlyList<Track>> ListTracksForReleaseAsync(string releaseId, CancellationToken ct = default);

        /// <summary>
        ///     Inserts or updates a track.
        /// </summary>
        Task UpsertTrackAsync(Track track, CancellationToken ct = default);

        // ========== Bulk Operations ==========

        /// <summary>
        ///     Counts total artists in the catalogue.
        /// </summary>
        Task<int> CountArtistsAsync(CancellationToken ct = default);

        /// <summary>
        ///     Counts total releases in the catalogue.
        /// </summary>
        Task<int> CountReleasesAsync(CancellationToken ct = default);

        /// <summary>
        ///     Counts total tracks in the catalogue.
        /// </summary>
        Task<int> CountTracksAsync(CancellationToken ct = default);

        // ========== LocalFile Methods ==========

        /// <summary>
        ///     Finds a local file by its absolute path.
        /// </summary>
        Task<LocalFile?> FindLocalFileByPathAsync(string path, CancellationToken ct = default);

        /// <summary>
        ///     Finds a local file by its internal ID.
        /// </summary>
        Task<LocalFile?> FindLocalFileByIdAsync(string localFileId, CancellationToken ct = default);

        /// <summary>
        ///     Finds local files linked to a specific track (via InferredTrackId or VerifiedCopy).
        /// </summary>
        Task<IReadOnlyList<LocalFile>> ListLocalFilesForTrackAsync(string trackId, CancellationToken ct = default);

        /// <summary>
        ///     Finds local files matching a primary hash.
        /// </summary>
        Task<IReadOnlyList<LocalFile>> FindLocalFilesByHashAsync(string hashPrimary, CancellationToken ct = default);

        /// <summary>
        ///     Inserts or updates a local file.
        /// </summary>
        Task UpsertLocalFileAsync(LocalFile localFile, CancellationToken ct = default);

        /// <summary>
        ///     Counts total local files in the catalogue.
        /// </summary>
        Task<int> CountLocalFilesAsync(CancellationToken ct = default);

        // ========== VerifiedCopy Methods ==========

        /// <summary>
        ///     Finds a verified copy for a specific track.
        /// </summary>
        /// <remarks>
        ///     Returns the most recent verified copy if multiple exist.
        /// </remarks>
        Task<VerifiedCopy?> FindVerifiedCopyForTrackAsync(string trackId, CancellationToken ct = default);

        /// <summary>
        ///     Finds all verified copies for a specific track.
        /// </summary>
        Task<IReadOnlyList<VerifiedCopy>> ListVerifiedCopiesForTrackAsync(string trackId, CancellationToken ct = default);

        /// <summary>
        ///     Finds a verified copy by its internal ID.
        /// </summary>
        Task<VerifiedCopy?> FindVerifiedCopyByIdAsync(string verifiedCopyId, CancellationToken ct = default);

        /// <summary>
        ///     Inserts or updates a verified copy.
        /// </summary>
        Task UpsertVerifiedCopyAsync(VerifiedCopy verifiedCopy, CancellationToken ct = default);

        /// <summary>
        ///     Deletes a verified copy (e.g., if user marks it as incorrect).
        /// </summary>
        Task DeleteVerifiedCopyAsync(string verifiedCopyId, CancellationToken ct = default);

        /// <summary>
        ///     Counts total verified copies in the catalogue.
        /// </summary>
        Task<int> CountVerifiedCopiesAsync(CancellationToken ct = default);
    }
}
