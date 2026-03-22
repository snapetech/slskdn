// <copyright file="SimpleMatchEngine.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Matching
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;

    /// <summary>
    ///     Simple, conservative match engine for VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     This implementation is intentionally conservative:
    ///     - Prefer false negatives over false positives
    ///     - Clear, predictable matching rules
    ///     - No fuzzy logic, no ML
    ///
    ///     Future: More sophisticated matching (Chromaprint, acoustic fingerprints, etc.)
    /// </remarks>
    public sealed class SimpleMatchEngine : IMatchEngine
    {
        private const int DurationToleranceSeconds = 5; // ±5 seconds is acceptable
        private const double MinimumMatchScore = 0.5;
        private readonly ICatalogueStore _catalogueStore;

        public SimpleMatchEngine(ICatalogueStore catalogueStore)
        {
            _catalogueStore = catalogueStore ?? throw new ArgumentNullException(nameof(catalogueStore));
        }

        public async Task<MatchResult> MatchAsync(
            Track track,
            CandidateFileMetadata candidate,
            CancellationToken cancellationToken = default)
        {
            // Strategy 1: Hash match (Exact)
            if (!string.IsNullOrEmpty(candidate.Hash))
            {
                // For v2 Phase 1: We don't have track hashes in catalogue yet
                // This will be implemented when we add verified copy tracking
                // For now, skip hash matching
            }

            // Strategy 2: Chromaprint match (VeryStrong)
            if (!string.IsNullOrEmpty(candidate.Chromaprint))
            {
                // For v2 Phase 1: Chromaprint matching deferred
                // This requires Chromaprint infrastructure
            }

            // Strategy 3: MBID + duration + size (Strong)
            if (candidate.Embedded?.MusicBrainzRecordingId != null &&
                string.Equals(candidate.Embedded.MusicBrainzRecordingId, track.MusicBrainzRecordingId, StringComparison.OrdinalIgnoreCase))
            {
                if (IsDurationMatch(track.DurationSeconds, candidate.DurationSeconds))
                {
                    return new MatchResult
                    {
                        Confidence = MatchConfidence.Strong,
                        Score = 0.95,
                        Reason = "MBID + duration match",
                    };
                }
                else
                {
                    return new MatchResult
                    {
                        Confidence = MatchConfidence.None,
                        Score = 0.0,
                        Reason = "MBID matches but duration mismatch",
                    };
                }
            }

            var context = await ResolveTrackContextAsync(track, cancellationToken);

            // Strategy 4: Title + artist + duration (Strong/Medium)
            if (candidate.Embedded != null)
            {
                var titleMatch = IsTextMatch(track.Title, candidate.Embedded.Title);
                var durationMatch = IsDurationMatch(track.DurationSeconds, candidate.DurationSeconds);
                var artistMatch = IsTextMatch(context.ArtistName, candidate.Embedded.Artist);
                var albumMatch = IsTextMatch(context.ReleaseTitle, candidate.Embedded.Album) ||
                    IsTextMatch(context.ReleaseGroupTitle, candidate.Embedded.Album);

                if (titleMatch && artistMatch && durationMatch)
                {
                    return new MatchResult
                    {
                        Confidence = MatchConfidence.Strong,
                        Score = 0.90,
                        Reason = "Title + artist + duration match",
                    };
                }

                if (titleMatch && albumMatch && durationMatch)
                {
                    return new MatchResult
                    {
                        Confidence = MatchConfidence.Medium,
                        Score = 0.82,
                        Reason = "Title + album + duration match",
                    };
                }

                if (titleMatch && artistMatch)
                {
                    return new MatchResult
                    {
                        Confidence = MatchConfidence.Medium,
                        Score = 0.78,
                        Reason = "Title + artist match",
                    };
                }

                if (titleMatch && durationMatch)
                {
                    return new MatchResult
                    {
                        Confidence = MatchConfidence.Medium,
                        Score = 0.75,
                        Reason = "Title + duration match",
                    };
                }
            }

            // Strategy 5: Filename heuristics (Weak)
            var filenameScore = GetFilenameMatchScore(track.Title, context.ArtistName, candidate.Filename);
            if (filenameScore > MinimumMatchScore)
            {
                return new MatchResult
                {
                    Confidence = filenameScore >= 0.7 ? MatchConfidence.Medium : MatchConfidence.Weak,
                    Score = filenameScore,
                    Reason = filenameScore >= 0.7 ? "Filename title + artist similarity" : "Filename similarity",
                };
            }

            // No match
            return new MatchResult
            {
                Confidence = MatchConfidence.None,
                Score = 0.0,
                Reason = "No match",
            };
        }

        public async Task<MatchResult> VerifyAsync(
            Track track,
            CandidateFileMetadata candidate,
            CancellationToken cancellationToken = default)
        {
            // Verification is stricter - we want at least Strong confidence
            var matchResult = await MatchAsync(track, candidate, cancellationToken);

            if (matchResult.Confidence < MatchConfidence.Strong)
            {
                return new MatchResult
                {
                    Confidence = MatchConfidence.None,
                    Score = 0.0,
                    Reason = $"Verification failed: only {matchResult.Confidence} confidence",
                };
            }

            return matchResult;
        }

        // ========== Private Helper Methods ==========
        private static bool IsDurationMatch(int? expectedSeconds, int? actualSeconds)
        {
            if (!expectedSeconds.HasValue || !actualSeconds.HasValue)
            {
                return false; // Can't match without duration
            }

            var diff = Math.Abs(expectedSeconds.Value - actualSeconds.Value);
            return diff <= DurationToleranceSeconds;
        }

        private async Task<TrackMatchContext> ResolveTrackContextAsync(Track track, CancellationToken cancellationToken)
        {
            string? releaseTitle = null;
            string? releaseGroupTitle = null;
            string? artistName = null;

            if (!string.IsNullOrWhiteSpace(track.ReleaseId))
            {
                var release = await _catalogueStore.FindReleaseByIdAsync(track.ReleaseId, cancellationToken);
                releaseTitle = release?.Title;

                if (release != null && !string.IsNullOrWhiteSpace(release.ReleaseGroupId))
                {
                    var releaseGroup = await _catalogueStore.FindReleaseGroupByIdAsync(release.ReleaseGroupId, cancellationToken);
                    releaseGroupTitle = releaseGroup?.Title;

                    if (releaseGroup != null && !string.IsNullOrWhiteSpace(releaseGroup.ArtistId))
                    {
                        var artist = await _catalogueStore.FindArtistByIdAsync(releaseGroup.ArtistId, cancellationToken);
                        artistName = artist?.Name;
                    }
                }
            }

            return new TrackMatchContext(artistName, releaseTitle, releaseGroupTitle);
        }

        private static bool IsTextMatch(string? expected, string? actual)
        {
            var normalizedExpected = NormalizeText(expected);
            var normalizedActual = NormalizeText(actual);

            if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedActual))
            {
                return false;
            }

            return string.Equals(normalizedExpected, normalizedActual, StringComparison.OrdinalIgnoreCase);
        }

        private static double GetFilenameMatchScore(string trackTitle, string? artistName, string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(trackTitle))
            {
                return 0.0;
            }

            var cleanFilename = NormalizeText(System.IO.Path.GetFileNameWithoutExtension(filename));
            var cleanTitle = NormalizeText(trackTitle);
            var cleanArtist = NormalizeText(artistName);

            if (cleanFilename.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(cleanArtist) &&
                cleanFilename.Contains(cleanArtist, StringComparison.OrdinalIgnoreCase))
            {
                var coverage = (double)(cleanTitle.Length + cleanArtist.Length) / cleanFilename.Length;
                return Math.Max(0.70, Math.Min(coverage, 0.92));
            }

            if (cleanFilename.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase))
            {
                var ratio = (double)cleanTitle.Length / cleanFilename.Length;
                return Math.Max(0.51, Math.Min(ratio, 0.9));
            }

            return 0.0;
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = value
                .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                .Select(char.ToLowerInvariant)
                .ToArray();

            return string.Join(
                " ",
                new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private sealed record TrackMatchContext(string? ArtistName, string? ReleaseTitle, string? ReleaseGroupTitle);
    }
}
