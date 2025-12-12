// <copyright file="MusicItem.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Music
{
    using System;
    using slskd.HashDb.Models;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Music domain implementation of <see cref="IContentItem"/> wrapping a track/recording.
    /// </summary>
    /// <remarks>
    ///     This adapter allows existing <see cref="AlbumTargetTrackEntry"/> database records
    ///     to be used with the domain-neutral VirtualSoulfind core without rewriting
    ///     the entire music subsystem.
    /// </remarks>
    public sealed class MusicItem : IContentItem
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicItem"/> class.
        /// </summary>
        /// <param name="id">The domain-neutral item ID.</param>
        /// <param name="workId">The parent work ID (album).</param>
        /// <param name="trackEntry">The underlying track entry from the database.</param>
        /// <param name="isAdvertisable">Whether this item is advertisable (T-MCP03).</param>
        public MusicItem(ContentItemId id, ContentWorkId workId, AlbumTargetTrackEntry trackEntry, bool isAdvertisable = false)
        {
            Id = id;
            WorkId = workId;
            TrackEntry = trackEntry ?? throw new ArgumentNullException(nameof(trackEntry));
            IsAdvertisable = isAdvertisable;
        }

        /// <inheritdoc/>
        public ContentItemId Id { get; }

        /// <inheritdoc/>
        public ContentDomain Domain => ContentDomain.Music;

        /// <inheritdoc/>
        public ContentWorkId? WorkId { get; }

        /// <inheritdoc/>
        public string Title => TrackEntry.Title;

        /// <inheritdoc/>
        public int? Position => TrackEntry.Position;

        /// <inheritdoc/>
        public TimeSpan? Duration => TrackEntry.DurationMs.HasValue
            ? TimeSpan.FromMilliseconds(TrackEntry.DurationMs.Value)
            : null;

        /// <inheritdoc/>
        /// <remarks>
        ///     T-MCP03: This must be set explicitly based on MCP check results.
        ///     Default is false (conservative - require explicit MCP approval).
        /// </remarks>
        public bool IsAdvertisable { get; }

        /// <summary>
        ///     Gets the underlying track entry from the database.
        /// </summary>
        /// <remarks>
        ///     This allows music-specific code to access full metadata (ISRC, RecordingId, etc.)
        ///     while still implementing the domain-neutral interface.
        /// </remarks>
        public AlbumTargetTrackEntry TrackEntry { get; }

        /// <summary>
        ///     Gets the MusicBrainz recording identifier.
        /// </summary>
        public string RecordingId => TrackEntry.RecordingId;

        /// <summary>
        ///     Gets the MusicBrainz release identifier (album).
        /// </summary>
        public string ReleaseId => TrackEntry.ReleaseId;

        /// <summary>
        ///     Gets the credited artist for this track.
        /// </summary>
        public string? Artist => TrackEntry.Artist;

        /// <summary>
        ///     Gets the ISRC (International Standard Recording Code).
        /// </summary>
        public string? Isrc => TrackEntry.Isrc;

        /// <summary>
        ///     Gets the duration in milliseconds (raw value from database).
        /// </summary>
        public int? DurationMs => TrackEntry.DurationMs;

        /// <summary>
        ///     Creates a <see cref="MusicItem"/> from an <see cref="AlbumTargetTrackEntry"/>.
        /// </summary>
        /// <param name="trackEntry">The track entry.</param>
        /// <param name="isAdvertisable">Whether this item is advertisable (default: false).</param>
        /// <returns>A new <see cref="MusicItem"/> instance.</returns>
        public static MusicItem FromTrackEntry(AlbumTargetTrackEntry trackEntry, bool isAdvertisable = false)
        {
            // Generate deterministic IDs from MusicBrainz identifiers
            var itemId = MusicDomainMapping.RecordingIdToContentItemId(trackEntry.RecordingId);
            var workId = MusicDomainMapping.ReleaseIdToContentWorkId(trackEntry.ReleaseId);

            return new MusicItem(itemId, workId, trackEntry, isAdvertisable);
        }
    }
}

