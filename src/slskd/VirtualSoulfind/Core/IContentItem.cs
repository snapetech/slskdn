// <copyright file="IContentItem.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Core
{
    using System;

    /// <summary>
    ///     Domain-neutral interface for a content item (track, episode, file, etc.).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A "content item" represents a discrete unit of content:
    ///         - Music: A track/recording
    ///         - Movie: A specific cut/version
    ///         - TV: An episode
    ///         - Book: A chapter
    ///         - GenericFile: An arbitrary file
    ///     </para>
    ///     <para>
    ///         Implementations provide domain-specific metadata (MBID, ISRC, hash, etc.)
    ///         while adhering to this common interface.
    ///     </para>
    /// </remarks>
    public interface IContentItem
    {
        /// <summary>
        ///     Gets the unique identifier for this item.
        /// </summary>
        ContentItemId Id { get; }

        /// <summary>
        ///     Gets the content domain (Music, GenericFile, etc.).
        /// </summary>
        ContentDomain Domain { get; }

        /// <summary>
        ///     Gets the parent work ID (album, season, etc.).
        /// </summary>
        /// <remarks>
        ///     For GenericFile domain, this may be null if the file is standalone.
        /// </remarks>
        ContentWorkId? WorkId { get; }

        /// <summary>
        ///     Gets the title of the item.
        /// </summary>
        /// <remarks>
        ///     - Music: Track title
        ///     - Movie: Film title (with variant, e.g., "Director's Cut")
        ///     - TV: Episode title
        ///     - Book: Chapter title
        ///     - GenericFile: Filename
        /// </remarks>
        string Title { get; }

        /// <summary>
        ///     Gets the primary name for the item (typically same as Title).
        /// </summary>
        string? PrimaryName => Title;

        /// <summary>
        ///     Gets the position/index within the parent work (optional).
        /// </summary>
        /// <remarks>
        ///     - Music: Track number
        ///     - TV: Episode number
        ///     - Book: Chapter number
        ///     - GenericFile: N/A (null)
        /// </remarks>
        int? Position { get; }

        /// <summary>
        ///     Gets the duration of the item (optional).
        /// </summary>
        /// <remarks>
        ///     - Music: Track duration
        ///     - Movie: Film duration
        ///     - TV: Episode duration
        ///     - GenericFile: N/A (null)
        /// </remarks>
        TimeSpan? Duration { get; }

        /// <summary>
        ///     Gets whether this content item is advertisable (T-MCP03).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This flag gates whether content can be:
        ///         - Advertised via mesh/DHT
        ///         - Served via content relay
        ///         - Included in VirtualSoulfind planner results
        ///     </para>
        ///     <para>
        ///         Set to false for:
        ///         - Blocked content (MCP verdict: Blocked)
        ///         - Quarantined content (MCP verdict: Quarantined)
        ///         - Content not yet checked by MCP
        ///     </para>
        ///     <para>
        ///         Set to true only for:
        ///         - Allowed content (MCP verdict: Allowed)
        ///     </para>
        ///     <para>
        ///         Default: false (conservative - require explicit approval)
        ///     </para>
        /// </remarks>
        bool IsAdvertisable { get; }
    }
}
