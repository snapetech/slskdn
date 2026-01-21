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

        public Task<MatchResult> MatchAsync(
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
                candidate.Embedded.MusicBrainzRecordingId == track.MusicBrainzRecordingId)
            {
                if (IsDurationMatch(track.DurationSeconds, candidate.DurationSeconds))
                {
                    return Task.FromResult(new MatchResult
                    {
                        Confidence = MatchConfidence.Strong,
                        Score = 0.95,
                        Reason = "MBID + duration match",
                    });
                }
                else
                {
                    // MBID matches but duration doesn't = likely wrong file
                    return Task.FromResult(new MatchResult
                    {
                        Confidence = MatchConfidence.None,
                        Score = 0.0,
                        Reason = "MBID matches but duration mismatch",
                    });
                }
            }

            // Strategy 4: Title + artist + duration (Medium)
            if (candidate.Embedded != null)
            {
                var titleMatch = IsTextMatch(track.Title, candidate.Embedded.Title);
                var durationMatch = IsDurationMatch(track.DurationSeconds, candidate.DurationSeconds);

                if (titleMatch && durationMatch)
                {
                    // For v2 Phase 1: We don't have artist name in Track entity yet
                    // (Track links to Release which links to Artist)
                    // For now, accept title + duration as Medium
                    return Task.FromResult(new MatchResult
                    {
                        Confidence = MatchConfidence.Medium,
                        Score = 0.75,
                        Reason = "Title + duration match",
                    });
                }
            }

            // Strategy 5: Filename heuristics (Weak)
            var filenameScore = GetFilenameMatchScore(track.Title, candidate.Filename);
            if (filenameScore > MinimumMatchScore)
            {
                return Task.FromResult(new MatchResult
                {
                    Confidence = MatchConfidence.Weak,
                    Score = filenameScore,
                    Reason = "Filename similarity",
                });
            }

            // No match
            return Task.FromResult(new MatchResult
            {
                Confidence = MatchConfidence.None,
                Score = 0.0,
                Reason = "No match",
            });
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

        private static bool IsTextMatch(string expected, string? actual)
        {
            if (string.IsNullOrWhiteSpace(actual))
            {
                return false;
            }

            // Simple case-insensitive exact match
            // Future: Levenshtein distance, fuzzy matching, etc.
            return string.Equals(
                expected?.Trim(),
                actual?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static double GetFilenameMatchScore(string trackTitle, string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(trackTitle))
            {
                return 0.0;
            }

            // Remove extension and common separators
            var cleanFilename = System.IO.Path.GetFileNameWithoutExtension(filename)
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace(".", " ")
                .ToLowerInvariant()
                .Trim();

            var cleanTitle = trackTitle
                .ToLowerInvariant()
                .Trim();

            // Simple containment check
            if (cleanFilename.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase))
            {
                // Score based on how much of the filename is the title
                var ratio = (double)cleanTitle.Length / cleanFilename.Length;
                return Math.Max(0.51, Math.Min(ratio, 0.9)); // At least 0.51 to pass threshold
            }

            return 0.0;
        }
    }
}
