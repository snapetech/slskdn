// <copyright file="Artist.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;

    /// <summary>
    ///     Represents an artist in the virtual catalogue.
    /// </summary>
    /// <remarks>
    ///     Artists are the top-level organizational unit in the music catalogue.
    ///     They map to external IDs (MusicBrainz, etc.) and have normalized metadata.
    /// </remarks>
    public sealed class Artist
    {
        /// <summary>
        ///     Gets or initializes the internal artist ID.
        /// </summary>
        public string ArtistId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the MusicBrainz artist ID (if available).
        /// </summary>
        public string? MusicBrainzId { get; init; }

        /// <summary>
        ///     Gets or initializes the artist name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the sort name (for alphabetization).
        /// </summary>
        /// <remarks>
        ///     Example: "Beatles, The" for "The Beatles"
        /// </remarks>
        public string? SortName { get; init; }

        /// <summary>
        ///     Gets or initializes the genre/tag list (comma-separated).
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        ///     Gets or initializes when this artist was added to the catalogue.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this artist was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
