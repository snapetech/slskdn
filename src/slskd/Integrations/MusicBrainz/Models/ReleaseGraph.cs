// <copyright file="ReleaseGraph.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz.Models
{
    using System;
    using System.Collections.Generic;

    public class ArtistReleaseGraph
    {
        public string ArtistId { get; set; }

        public string Name { get; set; }

        public string SortName { get; set; }

        public List<ReleaseGroup> ReleaseGroups { get; set; } = new();

        public DateTimeOffset CachedAt { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public class ReleaseGroup
    {
        public string ReleaseGroupId { get; set; }

        public string Title { get; set; }

        public ReleaseGroupType Type { get; set; }

        public string FirstReleaseDate { get; set; }

        public List<Release> Releases { get; set; } = new();
    }

    public class Release
    {
        public string ReleaseId { get; set; }

        public string Title { get; set; }

        public string Country { get; set; }

        public string Status { get; set; }

        public string ReleaseDate { get; set; }
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
