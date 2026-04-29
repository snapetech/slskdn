// <copyright file="ReleaseGraph.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Models
{
    using System;
    using System.Collections.Generic;

    public class ArtistReleaseGraph
    {
        public string ArtistId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string SortName { get; set; } = string.Empty;

        public List<ReleaseGroup> ReleaseGroups { get; set; } = new();

        public DateTimeOffset CachedAt { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public class ReleaseGroup
    {
        public string ReleaseGroupId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public ReleaseGroupType Type { get; set; }

        public string FirstReleaseDate { get; set; } = string.Empty;

        public List<Release> Releases { get; set; } = new();
    }

    public class Release
    {
        public string ReleaseId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string ReleaseDate { get; set; } = string.Empty;
    }

    public enum ReleaseGroupType
    {
        Album,
        Single,
        EP,
        Compilation,
        Soundtrack,
        Live,
        Remix,
        Other,
    }
}
