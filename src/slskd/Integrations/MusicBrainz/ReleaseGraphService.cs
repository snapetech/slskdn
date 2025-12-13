namespace slskd.Integrations.MusicBrainz
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd.HashDb;
    using slskd.Integrations.MusicBrainz.Models;

    public interface IArtistReleaseGraphService
    {
        Task<ArtistReleaseGraph?> GetArtistReleaseGraphAsync(string artistId, bool forceRefresh = false, CancellationToken ct = default);
    }

    /// <summary>
    ///     Fetches and caches artist release graphs from MusicBrainz.
    /// </summary>
    public class ReleaseGraphService : IArtistReleaseGraphService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
        private readonly IHashDbService hashDb;
        private readonly ILogger<ReleaseGraphService> log;

        public ReleaseGraphService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<slskd.Options> optionsMonitor,
            IHashDbService hashDb,
            ILogger<ReleaseGraphService> log)
        {
            this.httpClientFactory = httpClientFactory;
            this.optionsMonitor = optionsMonitor;
            this.hashDb = hashDb;
            this.log = log;
        }

        public async Task<ArtistReleaseGraph?> GetArtistReleaseGraphAsync(string artistId, bool forceRefresh = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(artistId))
            {
                throw new ArgumentException("artistId is required", nameof(artistId));
            }

            log.LogInformation("[MusicBrainz] Fetching release graph for artist {ArtistId} (forceRefresh={ForceRefresh})", 
                artistId, forceRefresh);

            var cached = await hashDb.GetArtistReleaseGraphAsync(artistId, ct).ConfigureAwait(false);
            if (!forceRefresh && cached != null && (cached.ExpiresAt == null || cached.ExpiresAt > DateTimeOffset.UtcNow))
            {
                log.LogInformation("[MusicBrainz] Using cached release graph for artist {ArtistId} ({Count} release groups, expires {ExpiresAt})", 
                    artistId, cached.ReleaseGroups?.Count ?? 0, cached.ExpiresAt);
                return cached;
            }

            var options = optionsMonitor.CurrentValue.Integration.MusicBrainz;
            var baseUrl = options.BaseUrl.TrimEnd('/');
            var userAgent = options.UserAgent;

            var http = httpClientFactory.CreateClient();
            http.Timeout = options.Timeout;

            log.LogInformation("[MusicBrainz] Fetching artist metadata from MusicBrainz API: {ArtistId}", artistId);
            var artist = await GetAsync<ArtistResponse>($"{baseUrl}/artist/{artistId}?fmt=json", http, userAgent, ct).ConfigureAwait(false);
            if (artist == null)
            {
                log.LogWarning("[MusicBrainz] Artist {ArtistId} not found", artistId);
                return cached; // fallback to stale if exists
            }

            log.LogInformation("[MusicBrainz] Fetching release groups for artist {ArtistName} ({ArtistId})", 
                artist.Name, artistId);
            var releaseGroups = await FetchReleaseGroupsAsync(artistId, http, baseUrl, userAgent, ct).ConfigureAwait(false);

            var graph = new ArtistReleaseGraph
            {
                ArtistId = artist.Id,
                Name = artist.Name ?? string.Empty,
                SortName = artist.SortName ?? artist.Name ?? string.Empty,
                ReleaseGroups = releaseGroups,
                CachedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(CacheTtl),
            };

            log.LogInformation("[MusicBrainz] Cached release graph for artist {ArtistName}: {Count} release groups, expires in {Hours} hours", 
                artist.Name, releaseGroups.Count, CacheTtl.TotalHours);

            await hashDb.UpsertArtistReleaseGraphAsync(graph, ct).ConfigureAwait(false);
            return graph;
        }

        private async Task<List<ReleaseGroup>> FetchReleaseGroupsAsync(string artistId, HttpClient http, string baseUrl, string userAgent, CancellationToken ct)
        {
            var groups = new List<ReleaseGroup>();
            var offset = 0;
            const int limit = 100;

            log.LogDebug("[MusicBrainz] Fetching release groups for artist {ArtistId} (pagination: limit={Limit})", 
                artistId, limit);

            while (true)
            {
                var url = $"{baseUrl}/release-group?artist={artistId}&fmt=json&limit={limit}&offset={offset}";
                var page = await GetAsync<ReleaseGroupSearchResponse>(url, http, userAgent, ct).ConfigureAwait(false);
                if (page?.ReleaseGroups == null || page.ReleaseGroups.Length == 0)
                {
                    break;
                }

                log.LogDebug("[MusicBrainz] Fetched {Count} release groups at offset {Offset}", 
                    page.ReleaseGroups.Length, offset);

                foreach (var rg in page.ReleaseGroups)
                {
                    var type = MapReleaseGroupType(rg.PrimaryType);
                    var group = new ReleaseGroup
                    {
                        ReleaseGroupId = rg.Id,
                        Title = rg.Title ?? string.Empty,
                        Type = type,
                        FirstReleaseDate = rg.FirstReleaseDate,
                    };

                    // Fetch releases for this group (rate-limit 1 req/sec)
                    await Task.Delay(TimeSpan.FromMilliseconds(1100), ct).ConfigureAwait(false);
                    var rgDetail = await GetAsync<ReleaseGroupResponse>($"{baseUrl}/release-group/{rg.Id}?inc=releases&fmt=json", http, userAgent, ct).ConfigureAwait(false);
                    if (rgDetail?.Releases != null)
                    {
                        group.Releases = rgDetail.Releases
                            .Select(r => new Models.Release
                            {
                                ReleaseId = r.Id,
                                Title = r.Title ?? string.Empty,
                                Country = r.Country,
                                Status = r.Status,
                                ReleaseDate = r.Date,
                            })
                            .ToList();
                        
                        log.LogDebug("[MusicBrainz] Release group '{Title}' has {Count} releases", 
                            group.Title, group.Releases.Count);
                    }

                    groups.Add(group);
                }

                offset += limit;
                if (page.ReleaseGroups.Length < limit)
                {
                    break;
                }

                // Respect 1 req/sec
                await Task.Delay(TimeSpan.FromMilliseconds(1100), ct).ConfigureAwait(false);
            }

            log.LogInformation("[MusicBrainz] Fetched total of {Count} release groups for artist {ArtistId}", 
                groups.Count, artistId);

            return groups;
        }

        private static ReleaseGroupType MapReleaseGroupType(string? primaryType)
        {
            return primaryType?.ToLowerInvariant() switch
            {
                "album" => ReleaseGroupType.Album,
                "single" => ReleaseGroupType.Single,
                "ep" => ReleaseGroupType.EP,
                "compilation" => ReleaseGroupType.Compilation,
                "soundtrack" => ReleaseGroupType.Soundtrack,
                "live" => ReleaseGroupType.Live,
                "remix" => ReleaseGroupType.Remix,
                _ => ReleaseGroupType.Other,
            };
        }

        private static async Task<T?> GetAsync<T>(string uri, HttpClient http, string userAgent, CancellationToken ct) where T : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(userAgent);

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct).ConfigureAwait(false);
        }

        private class ArtistResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("sort-name")]
            public string SortName { get; set; }
        }

        private class ReleaseGroupSearchResponse
        {
            [JsonPropertyName("release-group-count")]
            public int Count { get; set; }

            [JsonPropertyName("release-groups")]
            public ReleaseGroupItem[] ReleaseGroups { get; set; } = Array.Empty<ReleaseGroupItem>();
        }

        private class ReleaseGroupItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("primary-type")]
            public string PrimaryType { get; set; }

            [JsonPropertyName("first-release-date")]
            public string FirstReleaseDate { get; set; }
        }

        private class ReleaseGroupResponse
        {
            [JsonPropertyName("releases")]
            public ReleaseItem[] Releases { get; set; } = Array.Empty<ReleaseItem>();
        }

        private class ReleaseItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("country")]
            public string Country { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; }

            [JsonPropertyName("date")]
            public string Date { get; set; }
        }
    }
}
















