// <copyright file="ILibraryReconciliationService.cs" company="slskd Team">
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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for analyzing library gaps and suggesting improvements.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The reconciliation service helps users understand what's missing from their library
    ///         and what could be improved. It analyzes:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>Partial releases (some tracks missing)</item>
    ///         <item>Low-quality files that could be upgraded</item>
    ///         <item>Tracks with no local copies</item>
    ///         <item>Tracks with no verified copies</item>
    ///     </list>
    /// </remarks>
    public interface ILibraryReconciliationService
    {
        /// <summary>
        ///     Finds missing tracks for a specific release.
        /// </summary>
        /// <param name="releaseId">The release ID to analyze.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of missing track IDs.</returns>
        Task<IReadOnlyList<string>> FindMissingTracksForReleaseAsync(string releaseId, CancellationToken ct = default);

        /// <summary>
        ///     Analyzes all releases in the catalogue to find partial releases.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of gap analysis results.</returns>
        Task<IReadOnlyList<ReleaseGapAnalysis>> AnalyzeAllReleasesAsync(CancellationToken ct = default);

        /// <summary>
        ///     Finds tracks that could be upgraded to better quality.
        /// </summary>
        /// <param name="minQualityImprovement">Minimum quality improvement threshold (0.0-1.0). Default: 0.2</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of upgrade suggestions.</returns>
        Task<IReadOnlyList<UpgradeSuggestion>> FindUpgradeOpportunitiesAsync(
            float minQualityImprovement = 0.2f,
            CancellationToken ct = default);

        /// <summary>
        ///     Finds tracks in the catalogue that have no local copies at all.
        /// </summary>
        /// <param name="limit">Maximum number of results. Default: 100</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of track IDs with no local copies.</returns>
        Task<IReadOnlyList<string>> FindTracksWithoutLocalCopiesAsync(int limit = 100, CancellationToken ct = default);

        /// <summary>
        ///     Finds local files that are not linked to any track (orphans).
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of orphaned local file IDs.</returns>
        Task<IReadOnlyList<string>> FindOrphanedLocalFilesAsync(CancellationToken ct = default);
    }

    /// <summary>
    ///     Gap analysis result for a release.
    /// </summary>
    public sealed class ReleaseGapAnalysis
    {
        /// <summary>
        ///     Gets or initializes the release ID.
        /// </summary>
        public required string ReleaseId { get; init; }

        /// <summary>
        ///     Gets or initializes the release title.
        /// </summary>
        public required string ReleaseTitle { get; init; }

        /// <summary>
        ///     Gets or initializes the artist name.
        /// </summary>
        public required string ArtistName { get; init; }

        /// <summary>
        ///     Gets or initializes the total number of tracks in the release.
        /// </summary>
        public required int TotalTracks { get; init; }

        /// <summary>
        ///     Gets or initializes the number of tracks with local copies.
        /// </summary>
        public required int TracksWithLocalCopies { get; init; }

        /// <summary>
        ///     Gets or initializes the number of tracks with verified copies.
        /// </summary>
        public required int TracksWithVerifiedCopies { get; init; }

        /// <summary>
        ///     Gets or initializes the list of missing track IDs.
        /// </summary>
        public required IReadOnlyList<string> MissingTrackIds { get; init; }

        /// <summary>
        ///     Gets the completion percentage (0.0-1.0).
        /// </summary>
        public float CompletionPercentage => TotalTracks > 0
            ? (float)TracksWithLocalCopies / TotalTracks
            : 0.0f;

        /// <summary>
        ///     Gets whether this is a partial release (some tracks missing).
        /// </summary>
        public bool IsPartial => TracksWithLocalCopies > 0 && TracksWithLocalCopies < TotalTracks;
    }

    /// <summary>
    ///     Suggestion for upgrading a track to better quality.
    /// </summary>
    public sealed class UpgradeSuggestion
    {
        /// <summary>
        ///     Gets or initializes the track ID.
        /// </summary>
        public required string TrackId { get; init; }

        /// <summary>
        ///     Gets or initializes the track title.
        /// </summary>
        public required string TrackTitle { get; init; }

        /// <summary>
        ///     Gets or initializes the local file ID.
        /// </summary>
        public required string LocalFileId { get; init; }

        /// <summary>
        ///     Gets or initializes the current quality rating (0.0-1.0).
        /// </summary>
        public required float CurrentQuality { get; init; }

        /// <summary>
        ///     Gets or initializes the target quality (e.g., "FLAC", "MP3 320").
        /// </summary>
        public required string TargetQuality { get; init; }

        /// <summary>
        ///     Gets or initializes the expected quality improvement (0.0-1.0).
        /// </summary>
        public required float QualityImprovement { get; init; }

        /// <summary>
        ///     Gets or initializes the current codec (e.g., "MP3", "AAC").
        /// </summary>
        public required string CurrentCodec { get; init; }

        /// <summary>
        ///     Gets or initializes the current bitrate.
        /// </summary>
        public required int CurrentBitrate { get; init; }
    }
}
