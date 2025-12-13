// <copyright file="AlbumTargetEntry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.HashDb.Models
{
    /// <summary>
    ///     Represents a stored MusicBrainz album target.
    /// </summary>
    public sealed class AlbumTargetEntry
    {
        /// <summary>
        ///     Gets or sets the MusicBrainz release identifier.
        /// </summary>
        public string ReleaseId { get; set; }

        /// <summary>
        ///     Gets or sets the Discogs release identifier (if known).
        /// </summary>
        public string DiscogsReleaseId { get; set; }

        /// <summary>
        ///     Gets or sets the album title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        ///     Gets or sets the main artist credit.
        /// </summary>
        public string Artist { get; set; }

        /// <summary>
        ///     Gets or sets the release date as stored (YYYY-MM-DD).
        /// </summary>
        public string ReleaseDate { get; set; }

        /// <summary>
        ///     Gets or sets the country of release.
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        ///     Gets or sets the label name.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        ///     Gets or sets the release status (e.g., official, bootleg).
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        ///     Gets or sets the timestamp when this entry was created.
        /// </summary>
        public long CreatedAt { get; set; }
    }
}

















