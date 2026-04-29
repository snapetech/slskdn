// <copyright file="IMatchEngine.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Matching
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;

    /// <summary>
    ///     Interface for the match and verification engine.
    /// </summary>
    /// <remarks>
    ///     The match engine determines if a candidate file is the right file for a catalogue track.
    ///
    ///     Match strategies (in order of strength):
    ///     1. Hash match (SHA256) - EXACT
    ///     2. Chromaprint match - VERY STRONG
    ///     3. MBID + duration + size - STRONG
    ///     4. Title + artist + duration tolerance - MEDIUM
    ///     5. Filename heuristics - WEAK
    ///
    ///     The engine is conservative:
    ///     - Prefer false negatives over false positives
    ///     - Medium confidence is minimum for auto-download
    ///     - Weak matches require user confirmation
    /// </remarks>
    public interface IMatchEngine
    {
        /// <summary>
        ///     Matches a candidate file against a catalogue track.
        /// </summary>
        /// <param name="track">The track from the catalogue.</param>
        /// <param name="candidate">Metadata about the candidate file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Match result with confidence level and score.</returns>
        /// <remarks>
        ///     This is pure matching logic - no network calls, no file I/O.
        ///     All necessary data must be provided in the parameters.
        /// </remarks>
        Task<MatchResult> MatchAsync(
            Track track,
            CandidateFileMetadata candidate,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Verifies that a downloaded file matches the expected track.
        /// </summary>
        /// <param name="track">The expected track from the catalogue.</param>
        /// <param name="candidate">Metadata about the downloaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Match result (should be Strong or better for success).</returns>
        /// <remarks>
        ///     Verification is stricter than matching:
        ///     - Requires at least Strong confidence
        ///     - Used after download to confirm we got the right file
        ///     - If verification fails, file is quarantined/deleted
        /// </remarks>
        Task<MatchResult> VerifyAsync(
            Track track,
            CandidateFileMetadata candidate,
            CancellationToken cancellationToken = default);
    }
}
