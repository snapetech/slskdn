// <copyright file="AlbumTarget.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Represents an album-level target derived from MusicBrainz metadata.
    /// </summary>
    public sealed record AlbumTarget
    {
        /// <summary>
        ///     Gets or sets the MusicBrainz release identifier.
        /// </summary>
        public string MusicBrainzReleaseId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or sets the Discogs release identifier, if one can be inferred.
        /// </summary>
        public string? DiscogsReleaseId { get; init; }

        /// <summary>
        ///     Gets or sets the release title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or sets the primary artist string.
        /// </summary>
        public string Artist { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or sets the configured metadata for the release.
        /// </summary>
        public ReleaseMetadata Metadata { get; init; } = new();

        /// <summary>
        ///     Gets or sets the ordered list of track targets.
        /// </summary>
        public IReadOnlyList<TrackTarget> Tracks { get; init; } = Array.Empty<TrackTarget>();
    }

    /// <summary>
    ///     Represents a track target inside an album.
    /// </summary>
    public sealed record TrackTarget
    {
        /// <summary>
        ///     Gets or sets the MusicBrainz recording identifier.
        /// </summary>
        public string MusicBrainzRecordingId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or sets the position within the release.
        /// </summary>
        public int Position { get; init; }

        /// <summary>
        ///     Gets or sets the title of the track.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or sets the artists credited for the track.
        /// </summary>
        public string Artist { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or sets the duration of the track.
        /// </summary>
        public TimeSpan Duration { get; init; }

        /// <summary>
        ///     Gets or sets the primary ISRC if this is available.
        /// </summary>
        public string? Isrc { get; init; }
    }

    /// <summary>
    ///     Release metadata that supplements an album target.
    /// </summary>
    public sealed record ReleaseMetadata
    {
        /// <summary>
        ///     Gets or sets the release date, if MusicBrainz exposed it.
        /// </summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>
        ///     Gets or sets the country of release.
        /// </summary>
        public string? Country { get; init; }

        /// <summary>
        ///     Gets or sets the label name, if any.
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        ///     Gets or sets the release status (official, bootleg, etc).
        /// </summary>
        public string? Status { get; init; }
    }
}



