// <copyright file="InMemoryCatalogueStore.cs" company="slskd Team">
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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     In-memory implementation of <see cref="ICatalogueStore"/> for testing.
    /// </summary>
    /// <remarks>
    ///     This is NOT for production use. Use SqliteCatalogueStore instead.
    ///     Provided for unit testing and rapid prototyping.
    /// </remarks>
    public sealed class InMemoryCatalogueStore : ICatalogueStore
    {
        private readonly ConcurrentDictionary<string, Artist> _artists = new();
        private readonly ConcurrentDictionary<string, ReleaseGroup> _releaseGroups = new();
        private readonly ConcurrentDictionary<string, Release> _releases = new();
        private readonly ConcurrentDictionary<string, Track> _tracks = new();
        private readonly ConcurrentDictionary<string, LocalFile> _localFiles = new();
        private readonly ConcurrentDictionary<string, VerifiedCopy> _verifiedCopies = new();

        // Indexes for efficient lookup
        private readonly ConcurrentDictionary<string, string> _artistMbidToId = new();
        private readonly ConcurrentDictionary<string, string> _releaseGroupMbidToId = new();
        private readonly ConcurrentDictionary<string, string> _releaseMbidToId = new();
        private readonly ConcurrentDictionary<string, string> _trackMbidToId = new();
        private readonly ConcurrentDictionary<string, string> _localFilePathToId = new();

        // ========== Artist Methods ==========

        public Task<Artist?> FindArtistByIdAsync(string artistId, CancellationToken ct = default)
        {
            _artists.TryGetValue(artistId, out var artist);
            return Task.FromResult(artist);
        }

        public Task<Artist?> FindArtistByMBIDAsync(string mbid, CancellationToken ct = default)
        {
            if (_artistMbidToId.TryGetValue(mbid, out var id))
            {
                return FindArtistByIdAsync(id, ct);
            }

            return Task.FromResult<Artist?>(null);
        }

        public Task<IReadOnlyList<Artist>> SearchArtistsAsync(string query, int limit = 50, CancellationToken ct = default)
        {
            var results = _artists.Values
                .Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<Artist>>(results);
        }

        public Task UpsertArtistAsync(Artist artist, CancellationToken ct = default)
        {
            _artists[artist.ArtistId] = artist;

            if (!string.IsNullOrEmpty(artist.MusicBrainzId))
            {
                _artistMbidToId[artist.MusicBrainzId] = artist.ArtistId;
            }

            return Task.CompletedTask;
        }

        // ========== Release Group Methods ==========

        public Task<ReleaseGroup?> FindReleaseGroupByIdAsync(string releaseGroupId, CancellationToken ct = default)
        {
            _releaseGroups.TryGetValue(releaseGroupId, out var rg);
            return Task.FromResult(rg);
        }

        public Task<ReleaseGroup?> FindReleaseGroupByMBIDAsync(string mbid, CancellationToken ct = default)
        {
            if (_releaseGroupMbidToId.TryGetValue(mbid, out var id))
            {
                return FindReleaseGroupByIdAsync(id, ct);
            }

            return Task.FromResult<ReleaseGroup?>(null);
        }

        public Task<IReadOnlyList<ReleaseGroup>> ListReleaseGroupsForArtistAsync(string artistId, CancellationToken ct = default)
        {
            var results = _releaseGroups.Values
                .Where(rg => rg.ArtistId == artistId)
                .ToList();

            return Task.FromResult<IReadOnlyList<ReleaseGroup>>(results);
        }

        public Task UpsertReleaseGroupAsync(ReleaseGroup releaseGroup, CancellationToken ct = default)
        {
            _releaseGroups[releaseGroup.ReleaseGroupId] = releaseGroup;

            if (!string.IsNullOrEmpty(releaseGroup.MusicBrainzId))
            {
                _releaseGroupMbidToId[releaseGroup.MusicBrainzId] = releaseGroup.ReleaseGroupId;
            }

            return Task.CompletedTask;
        }

        // ========== Release Methods ==========

        public Task<Release?> FindReleaseByIdAsync(string releaseId, CancellationToken ct = default)
        {
            _releases.TryGetValue(releaseId, out var release);
            return Task.FromResult(release);
        }

        public Task<Release?> FindReleaseByMBIDAsync(string mbid, CancellationToken ct = default)
        {
            if (_releaseMbidToId.TryGetValue(mbid, out var id))
            {
                return FindReleaseByIdAsync(id, ct);
            }

            return Task.FromResult<Release?>(null);
        }

        public Task<IReadOnlyList<Release>> ListReleasesForReleaseGroupAsync(string releaseGroupId, CancellationToken ct = default)
        {
            var results = _releases.Values
                .Where(r => r.ReleaseGroupId == releaseGroupId)
                .ToList();

            return Task.FromResult<IReadOnlyList<Release>>(results);
        }

        public Task UpsertReleaseAsync(Release release, CancellationToken ct = default)
        {
            _releases[release.ReleaseId] = release;

            if (!string.IsNullOrEmpty(release.MusicBrainzId))
            {
                _releaseMbidToId[release.MusicBrainzId] = release.ReleaseId;
            }

            return Task.CompletedTask;
        }

        // ========== Track Methods ==========

        public Task<Track?> FindTrackByIdAsync(string trackId, CancellationToken ct = default)
        {
            _tracks.TryGetValue(trackId, out var track);
            return Task.FromResult(track);
        }

        public Task<Track?> FindTrackByMBIDAsync(string mbid, CancellationToken ct = default)
        {
            if (_trackMbidToId.TryGetValue(mbid, out var id))
            {
                return FindTrackByIdAsync(id, ct);
            }

            return Task.FromResult<Track?>(null);
        }

        public Task<IReadOnlyList<Track>> ListTracksForReleaseAsync(string releaseId, CancellationToken ct = default)
        {
            var results = _tracks.Values
                .Where(t => t.ReleaseId == releaseId)
                .OrderBy(t => t.DiscNumber)
                .ThenBy(t => t.TrackNumber)
                .ToList();

            return Task.FromResult<IReadOnlyList<Track>>(results);
        }

        public Task UpsertTrackAsync(Track track, CancellationToken ct = default)
        {
            _tracks[track.TrackId] = track;

            if (!string.IsNullOrEmpty(track.MusicBrainzRecordingId))
            {
                _trackMbidToId[track.MusicBrainzRecordingId] = track.TrackId;
            }

            return Task.CompletedTask;
        }

        // ========== Bulk Operations ==========

        public Task<int> CountArtistsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_artists.Count);
        }

        public Task<int> CountReleasesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_releases.Count);
        }

        public Task<int> CountTracksAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_tracks.Count);
        }

        // ========== LocalFile Methods ==========

        public Task<LocalFile?> FindLocalFileByPathAsync(string path, CancellationToken ct = default)
        {
            if (_localFilePathToId.TryGetValue(path, out var id))
            {
                return FindLocalFileByIdAsync(id, ct);
            }

            return Task.FromResult<LocalFile?>(null);
        }

        public Task<LocalFile?> FindLocalFileByIdAsync(string localFileId, CancellationToken ct = default)
        {
            _localFiles.TryGetValue(localFileId, out var localFile);
            return Task.FromResult(localFile);
        }

        public Task<IReadOnlyList<LocalFile>> ListLocalFilesForTrackAsync(string trackId, CancellationToken ct = default)
        {
            var results = _localFiles.Values
                .Where(lf => lf.InferredTrackId == trackId)
                .ToList();

            return Task.FromResult<IReadOnlyList<LocalFile>>(results);
        }

        public Task<IReadOnlyList<LocalFile>> FindLocalFilesByHashAsync(string hashPrimary, CancellationToken ct = default)
        {
            var results = _localFiles.Values
                .Where(lf => lf.HashPrimary == hashPrimary)
                .ToList();

            return Task.FromResult<IReadOnlyList<LocalFile>>(results);
        }

        public Task UpsertLocalFileAsync(LocalFile localFile, CancellationToken ct = default)
        {
            _localFiles[localFile.LocalFileId] = localFile;
            _localFilePathToId[localFile.Path] = localFile.LocalFileId;
            return Task.CompletedTask;
        }

        public Task<int> CountLocalFilesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_localFiles.Count);
        }

        // ========== VerifiedCopy Methods ==========

        public Task<VerifiedCopy?> FindVerifiedCopyForTrackAsync(string trackId, CancellationToken ct = default)
        {
            var verifiedCopy = _verifiedCopies.Values
                .Where(vc => vc.TrackId == trackId)
                .OrderByDescending(vc => vc.VerifiedAt)
                .FirstOrDefault();

            return Task.FromResult(verifiedCopy);
        }

        public Task<IReadOnlyList<VerifiedCopy>> ListVerifiedCopiesForTrackAsync(string trackId, CancellationToken ct = default)
        {
            var results = _verifiedCopies.Values
                .Where(vc => vc.TrackId == trackId)
                .OrderByDescending(vc => vc.VerifiedAt)
                .ToList();

            return Task.FromResult<IReadOnlyList<VerifiedCopy>>(results);
        }

        public Task<VerifiedCopy?> FindVerifiedCopyByIdAsync(string verifiedCopyId, CancellationToken ct = default)
        {
            _verifiedCopies.TryGetValue(verifiedCopyId, out var verifiedCopy);
            return Task.FromResult(verifiedCopy);
        }

        public Task UpsertVerifiedCopyAsync(VerifiedCopy verifiedCopy, CancellationToken ct = default)
        {
            _verifiedCopies[verifiedCopy.VerifiedCopyId] = verifiedCopy;
            return Task.CompletedTask;
        }

        public Task DeleteVerifiedCopyAsync(string verifiedCopyId, CancellationToken ct = default)
        {
            _verifiedCopies.TryRemove(verifiedCopyId, out _);
            return Task.CompletedTask;
        }

        public Task<int> CountVerifiedCopiesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_verifiedCopies.Count);
        }

        public void Dispose()
        {
            // Nothing to dispose for in-memory store
        }
    }
}
