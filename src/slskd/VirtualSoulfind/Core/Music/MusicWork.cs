// <copyright file="MusicWork.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Core.Music
{
    using System;
    using slskd.HashDb.Models;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Music domain implementation of <see cref="IContentWork"/> wrapping an album/release.
    /// </summary>
    /// <remarks>
    ///     This adapter allows existing <see cref="AlbumTargetEntry"/> database records
    ///     to be used with the domain-neutral VirtualSoulfind core without rewriting
    ///     the entire music subsystem.
    /// </remarks>
    public sealed class MusicWork : IContentWork
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicWork"/> class.
        /// </summary>
        /// <param name="id">The domain-neutral work ID.</param>
        /// <param name="albumEntry">The underlying album entry from the database.</param>
        public MusicWork(ContentWorkId id, AlbumTargetEntry albumEntry)
        {
            Id = id;
            AlbumEntry = albumEntry ?? throw new ArgumentNullException(nameof(albumEntry));
        }

        /// <inheritdoc/>
        public ContentWorkId Id { get; }

        /// <inheritdoc/>
        public ContentDomain Domain => ContentDomain.Music;

        /// <inheritdoc/>
        public string Title => AlbumEntry.Title;

        /// <inheritdoc/>
        public string? Creator => AlbumEntry.Artist;

        /// <inheritdoc/>
        public int? Year => ParseYearFromReleaseDate(AlbumEntry.ReleaseDate);

        /// <summary>
        ///     Gets the underlying album entry from the database.
        /// </summary>
        /// <remarks>
        ///     This allows music-specific code to access full metadata (label, country, etc.)
        ///     while still implementing the domain-neutral interface.
        /// </remarks>
        public AlbumTargetEntry AlbumEntry { get; }

        /// <summary>
        ///     Gets the MusicBrainz release identifier.
        /// </summary>
        public string ReleaseId => AlbumEntry.ReleaseId;

        /// <summary>
        ///     Gets the Discogs release identifier (if known).
        /// </summary>
        public string? DiscogsReleaseId => AlbumEntry.DiscogsReleaseId;

        /// <summary>
        ///     Gets the label name.
        /// </summary>
        public string? Label => AlbumEntry.Label;

        /// <summary>
        ///     Gets the country of release.
        /// </summary>
        public string? Country => AlbumEntry.Country;

        /// <summary>
        ///     Gets the release status (official, bootleg, etc.).
        /// </summary>
        public string? Status => AlbumEntry.Status;

        /// <summary>
        ///     Creates a <see cref="MusicWork"/> from an <see cref="AlbumTargetEntry"/>.
        /// </summary>
        /// <param name="albumEntry">The album entry.</param>
        /// <returns>A new <see cref="MusicWork"/> instance.</returns>
        public static MusicWork FromAlbumEntry(AlbumTargetEntry albumEntry)
        {
            // Generate a deterministic ContentWorkId from the MusicBrainz Release ID
            var workId = MusicDomainMapping.ReleaseIdToContentWorkId(albumEntry.ReleaseId);
            return new MusicWork(workId, albumEntry);
        }

        /// <summary>
        ///     Parses the year from a release date string (YYYY-MM-DD format).
        /// </summary>
        private static int? ParseYearFromReleaseDate(string? releaseDate)
        {
            if (string.IsNullOrWhiteSpace(releaseDate))
            {
                return null;
            }

            // Release date is stored as YYYY-MM-DD, so just take first 4 chars
            if (releaseDate.Length >= 4 && int.TryParse(releaseDate.Substring(0, 4), out var year))
            {
                return year;
            }

            return null;
        }
    }
}
