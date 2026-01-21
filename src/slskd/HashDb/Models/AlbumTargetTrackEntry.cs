// <copyright file="AlbumTargetTrackEntry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.HashDb.Models
{
    /// <summary>
    ///     Represents a stored track of a MusicBrainz album target.
    /// </summary>
    public sealed class AlbumTargetTrackEntry
    {
        /// <summary>
        ///     Gets or sets the MusicBrainz release identifier.
        /// </summary>
        public string ReleaseId { get; set; }

        /// <summary>
        ///     Gets or sets the position of the track within the release.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        ///     Gets or sets the MusicBrainz recording identifier.
        /// </summary>
        public string RecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the track title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        ///     Gets or sets the credited artist.
        /// </summary>
        public string Artist { get; set; }

        /// <summary>
        ///     Gets or sets the expected duration in milliseconds.
        /// </summary>
        public int? DurationMs { get; set; }

        /// <summary>
        ///     Gets or sets the primary ISRC.
        /// </summary>
        public string Isrc { get; set; }

        /// <summary>
        ///     Gets or sets the genre.
        /// </summary>
        public string? Genre { get; set; }

        /// <summary>
        ///     Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        ///     Gets or sets the Discogs ID.
        /// </summary>
        public string? DiscogsId { get; set; }
    }
}


















