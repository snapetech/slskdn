// <copyright file="Release.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;

    /// <summary>
    ///     Represents a specific release (edition) of a release group.
    /// </summary>
    /// <remarks>
    ///     A release is a concrete edition of an album/EP/single:
    ///     - 2009 US CD release
    ///     - 2010 UK vinyl remaster
    ///     - 2015 digital remaster
    ///
    ///     Multiple releases belong to one release group.
    /// </remarks>
    public sealed class Release
    {
        /// <summary>
        ///     Gets or initializes the internal release ID.
        /// </summary>
        public string ReleaseId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the MusicBrainz release ID (if available).
        /// </summary>
        public string? MusicBrainzId { get; init; }

        /// <summary>
        ///     Gets or initializes the release group ID.
        /// </summary>
        public string ReleaseGroupId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the release title.
        /// </summary>
        /// <remarks>
        ///     Usually same as release group title, but may differ for special editions.
        /// </remarks>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the release year.
        /// </summary>
        public int? Year { get; init; }

        /// <summary>
        ///     Gets or initializes the country code (e.g., "US", "GB", "JP").
        /// </summary>
        public string? Country { get; init; }

        /// <summary>
        ///     Gets or initializes the label name.
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        ///     Gets or initializes the catalogue number (label-specific ID).
        /// </summary>
        public string? CatalogNumber { get; init; }

        /// <summary>
        ///     Gets or initializes the number of discs/media in this release.
        /// </summary>
        public int MediaCount { get; init; } = 1;

        /// <summary>
        ///     Gets or initializes when this release was added.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this release was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
