namespace slskd.Integrations.MusicBrainz
{
    using System;
    using System.Collections.Generic;
    using slskd.Integrations.MusicBrainz.Models;

    public enum DiscographyProfile
    {
        CoreDiscography,
        ExtendedDiscography,
        AllReleases,
    }

    public class DiscographyProfileFilter
    {
        public bool IncludeAlbums { get; set; } = true;

        public bool IncludeEPs { get; set; }

        public bool IncludeSingles { get; set; }

        public bool IncludeCompilations { get; set; }

        public bool IncludeLive { get; set; }

        public int? MinYear { get; set; }

        public int? MaxYear { get; set; }

        public List<string> PreferredCountries { get; set; } = new();

        public static DiscographyProfileFilter FromProfile(DiscographyProfile profile)
        {
            return profile switch
            {
                DiscographyProfile.CoreDiscography => new()
                {
                    IncludeAlbums = true,
                    IncludeEPs = false,
                    IncludeSingles = false,
                    IncludeCompilations = false,
                    IncludeLive = false,
                },
                DiscographyProfile.ExtendedDiscography => new()
                {
                    IncludeAlbums = true,
                    IncludeEPs = true,
                    IncludeSingles = false,
                    IncludeCompilations = false,
                    IncludeLive = true,
                },
                DiscographyProfile.AllReleases => new()
                {
                    IncludeAlbums = true,
                    IncludeEPs = true,
                    IncludeSingles = true,
                    IncludeCompilations = true,
                    IncludeLive = true,
                },
                _ => throw new ArgumentException($"Unknown profile: {profile}", nameof(profile)),
            };
        }
    }
}
