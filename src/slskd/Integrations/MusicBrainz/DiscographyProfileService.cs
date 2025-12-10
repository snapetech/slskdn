namespace slskd.Integrations.MusicBrainz
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Integrations.MusicBrainz.Models;

    public interface IDiscographyProfileService
    {
        List<string> ApplyProfile(ArtistReleaseGraph graph, DiscographyProfileFilter filter);

        Task<List<string>> GetReleaseIdsForProfileAsync(string artistId, DiscographyProfile profile, CancellationToken ct = default);
    }

    public class DiscographyProfileService : IDiscographyProfileService
    {
        private readonly IArtistReleaseGraphService releaseGraphService;

        public DiscographyProfileService(IArtistReleaseGraphService releaseGraphService)
        {
            this.releaseGraphService = releaseGraphService;
        }

        public async Task<List<string>> GetReleaseIdsForProfileAsync(string artistId, DiscographyProfile profile, CancellationToken ct = default)
        {
            var graph = await releaseGraphService.GetArtistReleaseGraphAsync(artistId, forceRefresh: false, ct).ConfigureAwait(false);
            if (graph == null)
            {
                return new List<string>();
            }

            var filter = DiscographyProfileFilter.FromProfile(profile);
            return ApplyProfile(graph, filter);
        }

        public List<string> ApplyProfile(ArtistReleaseGraph graph, DiscographyProfileFilter filter)
        {
            if (graph == null || graph.ReleaseGroups == null)
            {
                return new List<string>();
            }

            var allowedTypes = new HashSet<ReleaseGroupType>();
            if (filter.IncludeAlbums) allowedTypes.Add(ReleaseGroupType.Album);
            if (filter.IncludeEPs) allowedTypes.Add(ReleaseGroupType.EP);
            if (filter.IncludeSingles) allowedTypes.Add(ReleaseGroupType.Single);
            if (filter.IncludeCompilations) allowedTypes.Add(ReleaseGroupType.Compilation);
            if (filter.IncludeLive) allowedTypes.Add(ReleaseGroupType.Live);
            if (filter.IncludeSoundtracks) allowedTypes.Add(ReleaseGroupType.Soundtrack);
            if (filter.IncludeRemixes) allowedTypes.Add(ReleaseGroupType.Remix);
            if (filter.IncludeOther) allowedTypes.Add(ReleaseGroupType.Other);

            bool CountryAllowed(string country)
            {
                if (filter.PreferredCountries == null || filter.PreferredCountries.Count == 0)
                {
                    return true;
                }

                return !string.IsNullOrWhiteSpace(country) && filter.PreferredCountries.Contains(country, StringComparer.OrdinalIgnoreCase);
            }

            int? ParseYear(string date)
            {
                if (string.IsNullOrWhiteSpace(date))
                {
                    return null;
                }

                if (date.Length >= 4 && int.TryParse(date[..4], out var y))
                {
                    return y;
                }

                return null;
            }

            var releaseIds = new List<string>();

            foreach (var group in graph.ReleaseGroups)
            {
                if (!allowedTypes.Contains(group.Type))
                {
                    continue;
                }

                var year = ParseYear(group.FirstReleaseDate);
                if (filter.MinYear.HasValue && (!year.HasValue || year.Value < filter.MinYear.Value))
                {
                    continue;
                }

                if (filter.MaxYear.HasValue && (!year.HasValue || year.Value > filter.MaxYear.Value))
                {
                    continue;
                }

                // Choose best release within group: prefer country match then earliest date
                var best = group.Releases
                    .Where(r => CountryAllowed(r.Country))
                    .OrderBy(r => ParseYear(r.ReleaseDate) ?? int.MaxValue)
                    .ThenBy(r => r.Title)
                    .FirstOrDefault()
                    ?? group.Releases.FirstOrDefault();

                if (best != null && !string.IsNullOrWhiteSpace(best.ReleaseId))
                {
                    releaseIds.Add(best.ReleaseId);
                }
            }

            return releaseIds;
        }
    }
}
