// <copyright file="MatchConfidence.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Matching
{
    /// <summary>
    ///     Confidence level for a match between a candidate file and catalogue track.
    /// </summary>
    /// <remarks>
    ///     Match confidence combines multiple signals:
    ///     - Hash match (strongest)
    ///     - Chromaprint/fingerprint match (very strong)
    ///     - MBID + duration + size (strong)
    ///     - Title + artist + duration (medium)
    ///     - Filename heuristics (weak)
    /// </remarks>
    public enum MatchConfidence
    {
        /// <summary>No match / mismatch.</summary>
        None = 0,

        /// <summary>Weak match (filename heuristics only).</summary>
        Weak = 1,

        /// <summary>Medium match (title + artist + duration within tolerance).</summary>
        Medium = 2,

        /// <summary>Strong match (MBID + duration + size match).</summary>
        Strong = 3,

        /// <summary>Very strong match (Chromaprint/fingerprint match).</summary>
        VeryStrong = 4,

        /// <summary>Exact match (hash match).</summary>
        Exact = 5,
    }

    /// <summary>
    ///     Result of matching a candidate file against a catalogue track.
    /// </summary>
    public sealed class MatchResult
    {
        /// <summary>
        ///     Gets or initializes the confidence level.
        /// </summary>
        public MatchConfidence Confidence { get; init; }

        /// <summary>
        ///     Gets or initializes the match score (0.0 to 1.0).
        /// </summary>
        /// <remarks>
        ///     Higher is better. Used for ranking candidates within same confidence level.
        /// </remarks>
        public double Score { get; init; }

        /// <summary>
        ///     Gets or initializes the reason/explanation.
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>
        ///     Gets whether this is a usable match (Medium or better).
        /// </summary>
        public bool IsUsable => Confidence >= MatchConfidence.Medium;

        /// <summary>
        ///     Gets whether this is a strong match (Strong or better).
        /// </summary>
        public bool IsStrong => Confidence >= MatchConfidence.Strong;
    }
}
