// <copyright file="ReleaseGroup.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System;

    /// <summary>
    ///     Primary type of a release group.
    /// </summary>
    public enum ReleaseGroupPrimaryType
    {
        /// <summary>Unknown type.</summary>
        Unknown,

        /// <summary>Full-length album.</summary>
        Album,

        /// <summary>Extended play (EP).</summary>
        EP,

        /// <summary>Single.</summary>
        Single,

        /// <summary>Compilation.</summary>
        Compilation,

        /// <summary>Live album.</summary>
        Live,

        /// <summary>Soundtrack.</summary>
        Soundtrack,

        /// <summary>Other type.</summary>
        Other,
    }

    /// <summary>
    ///     Represents a release group (logical album/EP/single concept).
    /// </summary>
    /// <remarks>
    ///     A release group is the abstract concept of an album/EP/single.
    ///     Multiple releases (editions) belong to one release group.
    /// </remarks>
    public sealed class ReleaseGroup
    {
        /// <summary>
        ///     Gets or initializes the internal release group ID.
        /// </summary>
        public string ReleaseGroupId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the MusicBrainz release group ID (if available).
        /// </summary>
        public string? MusicBrainzId { get; init; }

        /// <summary>
        ///     Gets or initializes the artist ID.
        /// </summary>
        public string ArtistId { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the primary type.
        /// </summary>
        public ReleaseGroupPrimaryType PrimaryType { get; init; }

        /// <summary>
        ///     Gets or initializes the first release year (if known).
        /// </summary>
        public int? Year { get; init; }

        /// <summary>
        ///     Gets or initializes when this release group was added.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Gets or initializes when this release group was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
