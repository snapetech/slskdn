// <copyright file="Track.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;

    /// <summary>
    ///     Represents a track (recording) in the virtual catalogue.
    /// </summary>
    /// <remarks>
    ///     A track is the atomic unit of music content:
    ///     - Belongs to a specific release
    ///     - Has a position (disc + track number)
    ///     - Maps to a canonical recording (MusicBrainz recording ID)
    ///     - Has expected duration for matching purposes
    /// </remarks>
    public sealed class Track
    {
        /// <summary>
        ///     Gets or initializes the internal track ID.
        /// </summary>
        public string TrackId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the MusicBrainz recording ID (if available).
        /// </summary>
        public string? MusicBrainzRecordingId { get; init; }

        /// <summary>
        ///     Gets or initializes the release ID this track belongs to.
        /// </summary>
        public string ReleaseId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the disc number (1-based).
        /// </summary>
        public int DiscNumber { get; init; } = 1;

        /// <summary>
        ///     Gets or initializes the track number within the disc (1-based).
        /// </summary>
        public int TrackNumber { get; init; }

        /// <summary>
        ///     Gets or initializes the track title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the canonical duration in seconds.
        /// </summary>
        /// <remarks>
        ///     Used for matching local files (duration must be within tolerance).
        /// </remarks>
        public int? DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the ISRC (International Standard Recording Code).
        /// </summary>
        public string? Isrc { get; init; }

        /// <summary>
        ///     Gets or initializes genre/tag list (comma-separated).
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        ///     Gets or initializes when this track was added.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this track was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
