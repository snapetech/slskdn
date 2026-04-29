// <copyright file="VerifiedCopy.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;

    /// <summary>
    ///     Represents a "ground truth" link between a Track and a LocalFile.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A VerifiedCopy is a high-confidence assertion that a specific LocalFile
    ///         is a correct, verified copy of a specific Track. This is stronger than
    ///         <see cref="LocalFile.InferredTrackId"/>, which is just a heuristic.
    ///     </para>
    ///     <para>
    ///         <b>Verification Sources</b>:
    ///         - <see cref="VerificationSource.Manual"/>: User confirmed
    ///         - <see cref="VerificationSource.MultiCheck"/>: Multiple heuristics agree
    ///         - <see cref="VerificationSource.Fingerprint"/>: Acoustic fingerprint match
    ///         - <see cref="VerificationSource.Imported"/>: From external library (e.g., iTunes)
    ///     </para>
    ///     <para>
    ///         <b>Usage</b>:
    ///         - The Match Engine uses VerifiedCopy to avoid re-fetching content that already exists.
    ///         - The Planner prioritizes gaps (tracks without VerifiedCopy).
    ///         - The Resolver can skip validation for VerifiedCopy entries.
    ///     </para>
    /// </remarks>
    public sealed class VerifiedCopy
    {
        /// <summary>
        ///     Gets or initializes the unique identifier for this verified copy.
        /// </summary>
        public required string VerifiedCopyId { get; init; }

        /// <summary>
        ///     Gets or initializes the Track ID (foreign key to Track table).
        /// </summary>
        public required string TrackId { get; init; }

        /// <summary>
        ///     Gets or initializes the LocalFile ID (foreign key to LocalFile table).
        /// </summary>
        public required string LocalFileId { get; init; }

        /// <summary>
        ///     Gets or initializes the primary hash of the verified file.
        /// </summary>
        /// <remarks>
        ///     Redundant with LocalFile.HashPrimary, but stored here for integrity checks.
        /// </remarks>
        public required string HashPrimary { get; init; }

        /// <summary>
        ///     Gets or initializes the duration in seconds.
        /// </summary>
        /// <remarks>
        ///     Redundant with LocalFile.DurationSeconds, but stored here for integrity checks.
        /// </remarks>
        public required int DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the verification source.
        /// </summary>
        public required VerificationSource VerificationSource { get; init; }

        /// <summary>
        ///     Gets or initializes the timestamp when this verification was created.
        /// </summary>
        public required DateTimeOffset VerifiedAt { get; init; }

        /// <summary>
        ///     Gets or initializes optional notes about the verification.
        /// </summary>
        public string? Notes { get; init; }
    }

    /// <summary>
    ///     How a VerifiedCopy was established.
    /// </summary>
    public enum VerificationSource
    {
        /// <summary>
        ///     User manually confirmed the match.
        /// </summary>
        Manual = 0,

        /// <summary>
        ///     Multiple heuristics (hash, duration, filename) agree.
        /// </summary>
        MultiCheck = 1,

        /// <summary>
        ///     Acoustic fingerprint (Chromaprint) match.
        /// </summary>
        Fingerprint = 2,

        /// <summary>
        ///     Imported from external library (iTunes, MusicBrainz Picard, etc.).
        /// </summary>
        Imported = 3,
    }
}
