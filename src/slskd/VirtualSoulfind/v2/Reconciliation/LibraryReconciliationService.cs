// <copyright file="LibraryReconciliationService.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Reconciliation
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;

    /// <summary>
    ///     Production implementation of <see cref="ILibraryReconciliationService"/>.
    /// </summary>
    public sealed class LibraryReconciliationService : ILibraryReconciliationService
    {
        private const int PageSize = 250;
        private readonly ICatalogueStore _catalogue;

        public LibraryReconciliationService(ICatalogueStore catalogue)
        {
            _catalogue = catalogue;
        }

        public async Task<IReadOnlyList<string>> FindMissingTracksForReleaseAsync(string releaseId, CancellationToken ct = default)
        {
            // Get all tracks for the release
            var allTracks = await _catalogue.ListTracksForReleaseAsync(releaseId, ct);

            // For each track, check if it has a local copy or verified copy
            var missingTrackIds = new List<string>();

            foreach (var track in allTracks)
            {
                var localFiles = await _catalogue.ListLocalFilesForTrackAsync(track.TrackId, ct);
                var verifiedCopy = await _catalogue.FindVerifiedCopyForTrackAsync(track.TrackId, ct);

                // Missing if no local files AND no verified copy
                if (localFiles.Count == 0 && verifiedCopy == null)
                {
                    missingTrackIds.Add(track.TrackId);
                }
            }

            return missingTrackIds;
        }

        public async Task<IReadOnlyList<ReleaseGapAnalysis>> AnalyzeAllReleasesAsync(CancellationToken ct = default)
        {
            var results = new List<ReleaseGapAnalysis>();

            var releaseCount = await _catalogue.CountReleasesAsync(ct);

            if (releaseCount == 0)
            {
                return results;
            }

            for (var offset = 0; offset < releaseCount; offset += PageSize)
            {
                var releases = await _catalogue.ListReleasesAsync(offset, PageSize, ct);
                foreach (var release in releases)
                {
                    var analysis = await AnalyzeReleaseAsync(release, ct);
                    if (analysis != null)
                    {
                        results.Add(analysis);
                    }
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<UpgradeSuggestion>> FindUpgradeOpportunitiesAsync(
            float minQualityImprovement = 0.2f,
            CancellationToken ct = default)
        {
            var suggestions = new List<UpgradeSuggestion>();
            var fileCount = await _catalogue.CountLocalFilesAsync(ct);

            if (fileCount == 0)
            {
                return suggestions;
            }

            var verifiedByLocalFileId = await GetVerifiedCopiesByLocalFileIdAsync(ct);

            for (var offset = 0; offset < fileCount; offset += PageSize)
            {
                var localFiles = await _catalogue.ListLocalFilesAsync(offset, PageSize, ct);
                foreach (var localFile in localFiles)
                {
                    var trackId = localFile.InferredTrackId;
                    if (string.IsNullOrWhiteSpace(trackId) &&
                        verifiedByLocalFileId.TryGetValue(localFile.LocalFileId, out var verifiedCopy))
                    {
                        trackId = verifiedCopy.TrackId;
                    }

                    if (string.IsNullOrWhiteSpace(trackId))
                    {
                        continue;
                    }

                    var qualityImprovement = 1.0f - localFile.QualityRating;
                    if (qualityImprovement < minQualityImprovement)
                    {
                        continue;
                    }

                    var track = await _catalogue.FindTrackByIdAsync(trackId, ct);
                    suggestions.Add(new UpgradeSuggestion
                    {
                        TrackId = trackId,
                        TrackTitle = track?.Title ?? trackId,
                        LocalFileId = localFile.LocalFileId,
                        CurrentQuality = localFile.QualityRating,
                        TargetQuality = "FLAC",
                        QualityImprovement = qualityImprovement,
                        CurrentCodec = localFile.Codec,
                        CurrentBitrate = localFile.Bitrate,
                    });
                }
            }

            return suggestions;
        }

        public async Task<IReadOnlyList<string>> FindTracksWithoutLocalCopiesAsync(int limit = 100, CancellationToken ct = default)
        {
            var tracksWithoutCopies = new List<string>();
            var trackCount = await _catalogue.CountTracksAsync(ct);

            if (trackCount == 0)
            {
                return tracksWithoutCopies;
            }

            for (var offset = 0; offset < trackCount && tracksWithoutCopies.Count < limit; offset += PageSize)
            {
                var tracks = await _catalogue.ListTracksAsync(offset, PageSize, ct);
                foreach (var track in tracks)
                {
                    var localFiles = await _catalogue.ListLocalFilesForTrackAsync(track.TrackId, ct);
                    if (localFiles.Count == 0)
                    {
                        tracksWithoutCopies.Add(track.TrackId);
                        if (tracksWithoutCopies.Count >= limit)
                        {
                            break;
                        }
                    }
                }
            }

            return tracksWithoutCopies;
        }

        public async Task<IReadOnlyList<string>> FindOrphanedLocalFilesAsync(CancellationToken ct = default)
        {
            var orphanedFiles = new List<string>();
            var fileCount = await _catalogue.CountLocalFilesAsync(ct);

            if (fileCount == 0)
            {
                return orphanedFiles;
            }

            var verifiedByLocalFileId = await GetVerifiedCopiesByLocalFileIdAsync(ct);

            for (var offset = 0; offset < fileCount; offset += PageSize)
            {
                var localFiles = await _catalogue.ListLocalFilesAsync(offset, PageSize, ct);
                foreach (var localFile in localFiles)
                {
                    if (string.IsNullOrWhiteSpace(localFile.InferredTrackId) &&
                        !verifiedByLocalFileId.ContainsKey(localFile.LocalFileId))
                    {
                        orphanedFiles.Add(localFile.LocalFileId);
                    }
                }
            }

            return orphanedFiles;
        }

        private async Task<ReleaseGapAnalysis?> AnalyzeReleaseAsync(Release release, CancellationToken ct)
        {
            var tracks = await _catalogue.ListTracksForReleaseAsync(release.ReleaseId, ct);
            if (tracks.Count == 0)
            {
                return null;
            }

            var releaseGroup = await _catalogue.FindReleaseGroupByIdAsync(release.ReleaseGroupId, ct);
            var artist = releaseGroup == null
                ? null
                : await _catalogue.FindArtistByIdAsync(releaseGroup.ArtistId, ct);

            var localCopyCount = 0;
            var verifiedCopyCount = 0;
            var missingTrackIds = new List<string>();

            foreach (var track in tracks)
            {
                var localFiles = await _catalogue.ListLocalFilesForTrackAsync(track.TrackId, ct);
                var verifiedCopy = await _catalogue.FindVerifiedCopyForTrackAsync(track.TrackId, ct);

                if (localFiles.Count > 0)
                {
                    localCopyCount++;
                }

                if (verifiedCopy != null)
                {
                    verifiedCopyCount++;
                }

                if (localFiles.Count == 0 && verifiedCopy == null)
                {
                    missingTrackIds.Add(track.TrackId);
                }
            }

            if (localCopyCount == 0 || missingTrackIds.Count == 0)
            {
                return null;
            }

            return new ReleaseGapAnalysis
            {
                ReleaseId = release.ReleaseId,
                ReleaseTitle = release.Title,
                ArtistName = artist?.Name ?? "Unknown Artist",
                TotalTracks = tracks.Count,
                TracksWithLocalCopies = localCopyCount,
                TracksWithVerifiedCopies = verifiedCopyCount,
                MissingTrackIds = missingTrackIds,
            };
        }

        private async Task<Dictionary<string, VerifiedCopy>> GetVerifiedCopiesByLocalFileIdAsync(CancellationToken ct)
        {
            var verifiedCopies = new Dictionary<string, VerifiedCopy>();
            var verifiedCopyCount = await _catalogue.CountVerifiedCopiesAsync(ct);

            for (var offset = 0; offset < verifiedCopyCount; offset += PageSize)
            {
                var page = await _catalogue.ListVerifiedCopiesAsync(offset, PageSize, ct);
                foreach (var verifiedCopy in page)
                {
                    if (!verifiedCopies.ContainsKey(verifiedCopy.LocalFileId))
                    {
                        verifiedCopies[verifiedCopy.LocalFileId] = verifiedCopy;
                    }
                }
            }

            return verifiedCopies;
        }
    }
}
