// <copyright file="SourceFeedImportService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

public sealed class SourceFeedImportService : ISourceFeedImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex SpotifyUrlRegex = new(
        @"open\.spotify\.com/(?<type>playlist|album|track|artist|user|collection)/(?<id>[A-Za-z0-9._-]+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SpotifyUriRegex = new(
        @"spotify:(?<type>playlist|album|track|artist|user|liked|saved-tracks|saved-albums|followed-artists|playlists):?(?<id>[A-Za-z0-9._-]+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SourceFeedImportService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<global::slskd.Options> optionsMonitor,
        ISpotifyConnectionService spotifyConnectionService)
    {
        HttpClientFactory = httpClientFactory;
        OptionsMonitor = optionsMonitor;
        SpotifyConnectionService = spotifyConnectionService;
    }

    private IHttpClientFactory HttpClientFactory { get; }

    private IOptionsMonitor<global::slskd.Options> OptionsMonitor { get; }

    private ISpotifyConnectionService SpotifyConnectionService { get; }

    public async Task<SourceFeedImportResult> PreviewAsync(
        SourceFeedImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceText = request.SourceText.Trim();
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new SourceFeedImportResult();
        }

        var safeLimit = Math.Clamp(request.Limit, 1, OptionsMonitor.CurrentValue.Integration.Spotify.MaxItemsPerImport);
        var sourceKind = request.SourceKind.Trim().ToLowerInvariant();

        if (request.FetchProviderUrls && LooksLikeSpotify(sourceText, sourceKind))
        {
            return await PreviewSpotifyAsync(request, safeLimit, cancellationToken).ConfigureAwait(false);
        }

        return PreviewLocalText(sourceText, sourceKind, request.IncludeAlbum, safeLimit);
    }

    private static bool LooksLikeSpotify(string sourceText, string sourceKind)
    {
        return sourceKind == "spotify" ||
            sourceText.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase) ||
            sourceText.Contains("open.spotify.com", StringComparison.OrdinalIgnoreCase);
    }

    private SourceFeedImportResult PreviewLocalText(string sourceText, string sourceKind, bool includeAlbum, int limit)
    {
        IReadOnlyList<SourceFeedRow> rows;
        var kind = sourceKind == "auto" ? DetectLocalKind(sourceText) : sourceKind;

        rows = kind switch
        {
            "csv" => ParseCsvRows(sourceText, includeAlbum),
            "m3u" or "pls" => ParsePlaylistRows(sourceText),
            "rss" or "opml" => ParseXmlRows(sourceText),
            _ => ParsePlainTextRows(sourceText),
        };

        return BuildResult("local", kind, string.Empty, rows.Take(limit));
    }

    private async Task<SourceFeedImportResult> PreviewSpotifyAsync(
        SourceFeedImportRequest request,
        int limit,
        CancellationToken cancellationToken)
    {
        var target = ParseSpotifyTarget(request.SourceText.Trim());
        var token = await GetSpotifyAccessTokenAsync(
            target.RequiresUserToken,
            request.ProviderAccessToken,
            cancellationToken).ConfigureAwait(false);

        if (target.RequiresUserToken && string.IsNullOrWhiteSpace(token))
        {
            return new SourceFeedImportResult
            {
                Provider = "spotify",
                SourceKind = target.Type,
                SourceId = target.Id,
                RequiresAccessToken = true,
                RequiredScopeHint = target.ScopeHint,
            };
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new SourceFeedImportResult
            {
                Provider = "spotify",
                SourceKind = target.Type,
                SourceId = target.Id,
                RequiresAccessToken = true,
                RequiredScopeHint = "Configure integrations.spotify.client_id/client_secret, connect a Spotify account, or provide a Spotify bearer token.",
            };
        }

        var rows = new List<SourceFeedRow>();
        var requests = 0;

        switch (target.Type)
        {
            case "playlist":
                requests += await FetchSpotifyPlaylistItemsAsync(token, target.Id, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "album":
                requests += await FetchSpotifyAlbumTracksAsync(token, target.Id, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "track":
                requests += await FetchSpotifyTrackAsync(token, target.Id, rows, cancellationToken).ConfigureAwait(false);
                break;
            case "artist":
                requests += await FetchSpotifyArtistTopTracksAsync(token, target.Id, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "user":
                requests += await FetchSpotifyUserPlaylistsAsync(token, target.Id, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "liked":
            case "saved-tracks":
                requests += await FetchSpotifySavedTracksAsync(token, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "saved-albums":
                requests += await FetchSpotifySavedAlbumsAsync(token, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "followed-artists":
                requests += await FetchSpotifyFollowedArtistsAsync(token, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
            case "playlists":
                requests += await FetchSpotifyCurrentUserPlaylistsAsync(token, rows, limit, cancellationToken).ConfigureAwait(false);
                break;
        }

        var result = BuildResult("spotify", target.Type, target.Id, rows);
        result.NetworkRequestCount = requests;
        return result;
    }

    private async Task<string> GetSpotifyAccessTokenAsync(
        bool requiresUserToken,
        string providerAccessToken,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(providerAccessToken))
        {
            return providerAccessToken.Trim();
        }

        if (requiresUserToken)
        {
            return await SpotifyConnectionService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }

        var appToken = await GetSpotifyAppTokenAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(appToken)
            ? await SpotifyConnectionService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false)
            : appToken;
    }

    private async Task<string> GetSpotifyAppTokenAsync(CancellationToken cancellationToken)
    {
        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return string.Empty;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
        });

        using var response = await SendSpotifyAsync(request, cancellationToken).ConfigureAwait(false);
        var token = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return token?.AccessToken ?? string.Empty;
    }

    private async Task<int> FetchSpotifyPlaylistItemsAsync(string token, string playlistId, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
    {
        var requests = 0;
        var offset = 0;

        while (rows.Count < limit)
        {
            var pageLimit = Math.Min(50, limit - rows.Count);
            var relative = $"https://api.spotify.com/v1/playlists/{Uri.EscapeDataString(playlistId)}/items?limit={pageLimit}&offset={offset}&additional_types=track";
            var page = await GetSpotifyAsync<SpotifyPaging<SpotifyPlaylistItem>>(token, relative, cancellationToken).ConfigureAwait(false);
            requests++;
            if (page?.Items == null || page.Items.Count == 0)
            {
                break;
            }

            rows.AddRange(page.Items.Select(item => ToRow(item.Track, "spotify:playlist", playlistId)).Where(row => row != null)!);

            if (page.Next == null || rows.Count >= page.Total)
            {
                break;
            }

            offset += page.Items.Count;
        }

        return requests;
    }

    private async Task<int> FetchSpotifyAlbumTracksAsync(string token, string albumId, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
    {
        var album = await GetSpotifyAsync<SpotifyAlbum>($"Bearer {token}", $"https://api.spotify.com/v1/albums/{Uri.EscapeDataString(albumId)}", cancellationToken).ConfigureAwait(false);
        rows.AddRange((album?.Tracks?.Items ?? [])
            .Select(track => ToRow(track with { Album = new SpotifyAlbumSummary { Name = album?.Name ?? string.Empty } }, "spotify:album", albumId))
            .Where(row => row != null)!);
        return 1;
    }

    private async Task<int> FetchSpotifyTrackAsync(string token, string trackId, List<SourceFeedRow> rows, CancellationToken cancellationToken)
    {
        var track = await GetSpotifyAsync<SpotifyTrack>(token, $"https://api.spotify.com/v1/tracks/{Uri.EscapeDataString(trackId)}", cancellationToken).ConfigureAwait(false);
        var row = ToRow(track, "spotify:track", trackId);
        if (row != null)
        {
            rows.Add(row);
        }

        return 1;
    }

    private async Task<int> FetchSpotifyArtistTopTracksAsync(string token, string artistId, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
    {
        var market = OptionsMonitor.CurrentValue.Integration.Spotify.Market;
        var top = await GetSpotifyAsync<SpotifyTopTracks>(token, $"https://api.spotify.com/v1/artists/{Uri.EscapeDataString(artistId)}/top-tracks?market={Uri.EscapeDataString(market)}", cancellationToken).ConfigureAwait(false);
        rows.AddRange((top?.Tracks ?? []).Take(limit).Select(track => ToRow(track, "spotify:artist-top-tracks", artistId)).Where(row => row != null)!);
        return 1;
    }

    private async Task<int> FetchSpotifySavedTracksAsync(string token, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
        => await FetchSpotifyTrackPagesAsync(token, "https://api.spotify.com/v1/me/tracks", rows, limit, "spotify:liked", cancellationToken).ConfigureAwait(false);

    private async Task<int> FetchSpotifySavedAlbumsAsync(string token, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
    {
        var requests = 0;
        var offset = 0;

        while (rows.Count < limit)
        {
            var pageLimit = Math.Min(20, limit - rows.Count);
            var page = await GetSpotifyAsync<SpotifyPaging<SpotifySavedAlbum>>(
                token,
                $"https://api.spotify.com/v1/me/albums?limit={pageLimit}&offset={offset}",
                cancellationToken).ConfigureAwait(false);
            requests++;
            if (page?.Items == null || page.Items.Count == 0)
            {
                break;
            }

            foreach (var album in page.Items.Select(item => item.Album).Where(album => album != null))
            {
                rows.Add(new SourceFeedRow(
                    Title: album!.Name,
                    Artist: string.Join(" ", album.Artists.Select(artist => artist.Name).Where(value => !string.IsNullOrWhiteSpace(value))),
                    Album: album.Name,
                    Source: "spotify:saved-albums",
                    SourceId: album.Id,
                    ProviderUrl: album.ExternalUrls?.Spotify ?? string.Empty,
                    RawText: album.Name));
            }

            if (page.Next == null || rows.Count >= page.Total)
            {
                break;
            }

            offset += page.Items.Count;
        }

        return requests;
    }

    private async Task<int> FetchSpotifyFollowedArtistsAsync(string token, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
    {
        var requests = 0;
        string? after = null;

        while (rows.Count < limit)
        {
            var pageLimit = Math.Min(50, limit - rows.Count);
            var uri = $"https://api.spotify.com/v1/me/following?type=artist&limit={pageLimit}" +
                (string.IsNullOrWhiteSpace(after) ? string.Empty : $"&after={Uri.EscapeDataString(after)}");
            var page = await GetSpotifyAsync<SpotifyFollowedArtists>(token, uri, cancellationToken).ConfigureAwait(false);
            requests++;
            var artists = page?.Artists?.Items ?? [];
            if (artists.Count == 0)
            {
                break;
            }

            rows.AddRange(artists.Select(artist => new SourceFeedRow(
                Title: artist.Name,
                Artist: artist.Name,
                Album: string.Empty,
                Source: "spotify:followed-artists",
                SourceId: artist.Id,
                ProviderUrl: artist.ExternalUrls?.Spotify ?? string.Empty,
                RawText: artist.Name)));

            after = page?.Artists?.Cursors?.After;
            if (string.IsNullOrWhiteSpace(after))
            {
                break;
            }
        }

        return requests;
    }

    private async Task<int> FetchSpotifyCurrentUserPlaylistsAsync(string token, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
        => await FetchSpotifyPlaylistCollectionAsync(token, "https://api.spotify.com/v1/me/playlists", rows, limit, cancellationToken).ConfigureAwait(false);

    private async Task<int> FetchSpotifyUserPlaylistsAsync(string token, string userId, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
        => await FetchSpotifyPlaylistCollectionAsync(token, $"https://api.spotify.com/v1/users/{Uri.EscapeDataString(userId)}/playlists", rows, limit, cancellationToken).ConfigureAwait(false);

    private async Task<int> FetchSpotifyPlaylistCollectionAsync(string token, string uri, List<SourceFeedRow> rows, int limit, CancellationToken cancellationToken)
    {
        var requests = 0;
        var offset = 0;

        while (rows.Count < limit)
        {
            var page = await GetSpotifyAsync<SpotifyPaging<SpotifyPlaylistSummary>>(token, $"{uri}?limit=20&offset={offset}", cancellationToken).ConfigureAwait(false);
            requests++;
            if (page?.Items == null || page.Items.Count == 0)
            {
                break;
            }

            foreach (var playlist in page.Items)
            {
                requests += await FetchSpotifyPlaylistItemsAsync(token, playlist.Id, rows, limit, cancellationToken).ConfigureAwait(false);
                if (rows.Count >= limit)
                {
                    break;
                }
            }

            if (page.Next == null || rows.Count >= page.Total)
            {
                break;
            }

            offset += page.Items.Count;
        }

        return requests;
    }

    private async Task<int> FetchSpotifyTrackPagesAsync(string token, string uri, List<SourceFeedRow> rows, int limit, string source, CancellationToken cancellationToken)
    {
        var requests = 0;
        var offset = 0;

        while (rows.Count < limit)
        {
            var pageLimit = Math.Min(50, limit - rows.Count);
            var page = await GetSpotifyAsync<SpotifyPaging<SpotifySavedTrack>>(
                token,
                $"{uri}?limit={pageLimit}&offset={offset}",
                cancellationToken).ConfigureAwait(false);
            requests++;
            if (page?.Items == null || page.Items.Count == 0)
            {
                break;
            }

            rows.AddRange(page.Items.Select(item => ToRow(item.Track, source, item.Track?.Id ?? string.Empty)).Where(row => row != null)!);

            if (page.Next == null || rows.Count >= page.Total)
            {
                break;
            }

            offset += page.Items.Count;
        }

        return requests;
    }

    private async Task<T?> GetSpotifyAsync<T>(string token, string uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? AuthenticationHeaderValue.Parse(token)
            : new AuthenticationHeaderValue("Bearer", token);
        using var response = await SendSpotifyAsync(request, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendSpotifyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        var client = HttpClientFactory.CreateClient(nameof(SourceFeedImportService));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static SpotifyTarget ParseSpotifyTarget(string sourceText)
    {
        var uriMatch = SpotifyUriRegex.Match(sourceText);
        if (uriMatch.Success)
        {
            return NormalizeSpotifyTarget(uriMatch.Groups["type"].Value, uriMatch.Groups["id"].Value);
        }

        var urlMatch = SpotifyUrlRegex.Match(sourceText);
        if (urlMatch.Success)
        {
            var type = urlMatch.Groups["type"].Value.ToLowerInvariant();
            if (type == "collection")
            {
                var path = sourceText.ToLowerInvariant();
                if (path.Contains("albums", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeSpotifyTarget("saved-albums", string.Empty);
                }

                if (path.Contains("following", StringComparison.OrdinalIgnoreCase) || path.Contains("artists", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeSpotifyTarget("followed-artists", string.Empty);
                }

                return NormalizeSpotifyTarget("liked", string.Empty);
            }

            return NormalizeSpotifyTarget(type, urlMatch.Groups["id"].Value);
        }

        return NormalizeSpotifyTarget(sourceText.Trim().ToLowerInvariant(), string.Empty);
    }

    private static SpotifyTarget NormalizeSpotifyTarget(string type, string id)
    {
        var normalized = type.Trim().ToLowerInvariant() switch
        {
            "collection" or "liked" or "saved-tracks" => "saved-tracks",
            "saved-albums" => "saved-albums",
            "followed-artists" => "followed-artists",
            "playlists" => "playlists",
            var value => value,
        };

        return normalized switch
        {
            "saved-tracks" => new SpotifyTarget("saved-tracks", id, true, "user-library-read"),
            "saved-albums" => new SpotifyTarget("saved-albums", id, true, "user-library-read"),
            "followed-artists" => new SpotifyTarget("followed-artists", id, true, "user-follow-read"),
            "playlists" => new SpotifyTarget("playlists", id, true, "playlist-read-private playlist-read-collaborative"),
            "user" => new SpotifyTarget("user", id, false, string.Empty),
            var value => new SpotifyTarget(value, id, false, string.Empty),
        };
    }

    private static SourceFeedRow? ToRow(SpotifyTrack? track, string source, string sourceId)
    {
        if (track == null)
        {
            return null;
        }

        return new SourceFeedRow(
            Title: track.Name,
            Artist: string.Join(" ", track.Artists.Select(artist => artist.Name).Where(value => !string.IsNullOrWhiteSpace(value))),
            Album: track.Album?.Name ?? string.Empty,
            Source: source,
            SourceId: sourceId,
            ProviderUrl: track.ExternalUrls?.Spotify ?? string.Empty,
            RawText: track.Name);
    }

    private static SourceFeedImportResult BuildResult(string provider, string sourceKind, string sourceId, IEnumerable<SourceFeedRow> rows)
    {
        var result = new SourceFeedImportResult
        {
            Provider = provider,
            SourceKind = sourceKind,
            SourceId = sourceId,
        };
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            result.TotalRows++;
            var searchText = BuildSearchText(row.Title, row.Artist, row.Album);
            if (string.IsNullOrWhiteSpace(searchText))
            {
                result.SkippedCount++;
                result.SkippedRows.Add(new SourceFeedSkippedRow
                {
                    RowNumber = result.TotalRows,
                    Reason = "Missing artist/title metadata",
                    RawText = row.RawText,
                });
                continue;
            }

            var key = $"{provider}:{row.Source}:{NormalizeKey(searchText)}";
            if (!keys.Add(key))
            {
                result.DuplicateCount++;
                continue;
            }

            result.Suggestions.Add(new SourceFeedSuggestion
            {
                Title = string.IsNullOrWhiteSpace(row.Title) ? searchText : row.Title,
                Artist = row.Artist,
                Album = row.Album,
                SearchText = searchText,
                Source = row.Source,
                SourceId = row.SourceId,
                SourceItemId = row.SourceId,
                ProviderUrl = row.ProviderUrl,
                EvidenceKey = key,
                Reason = $"Imported from {provider} {sourceKind}.",
            });
        }

        result.SuggestionCount = result.Suggestions.Count;
        return result;
    }

    private static string DetectLocalKind(string sourceText)
    {
        var trimmed = sourceText.TrimStart();
        if (trimmed.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("#EXTINF", StringComparison.OrdinalIgnoreCase))
        {
            return "m3u";
        }

        if (trimmed.StartsWith("<rss", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<feed", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<opml", StringComparison.OrdinalIgnoreCase))
        {
            return "rss";
        }

        return LooksLikeCsv(sourceText) ? "csv" : "text";
    }

    private static bool LooksLikeCsv(string sourceText)
    {
        var firstLine = sourceText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstLine.Contains(',', StringComparison.Ordinal) &&
            firstLine.Contains("track", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<SourceFeedRow> ParseCsvRows(string csvText, bool includeAlbum)
    {
        var rows = ParseDelimitedRows(csvText);
        if (rows.Count == 0)
        {
            return [];
        }

        var hasHeader = LooksLikeHeader(rows[0]);
        var header = hasHeader ? rows[0] : [];
        var titleIndex = hasHeader ? FindColumn(header, "trackname", "track", "title", "songname", "song", "name") : 0;
        var artistIndex = hasHeader ? FindColumn(header, "artistname", "artistnames", "artists", "artist") : 1;
        var albumIndex = hasHeader ? FindColumn(header, "albumname", "album", "release") : 2;
        var urlIndex = hasHeader ? FindColumn(header, "url", "spotifyurl", "trackurl", "link") : -1;
        var startIndex = hasHeader ? 1 : 0;
        var result = new List<SourceFeedRow>();

        for (var index = startIndex; index < rows.Count; index++)
        {
            var row = rows[index];
            result.Add(new SourceFeedRow(
                Title: GetCell(row, titleIndex),
                Artist: GetCell(row, artistIndex),
                Album: includeAlbum ? GetCell(row, albumIndex) : string.Empty,
                Source: "csv",
                SourceId: (index + 1).ToString(CultureInfo.InvariantCulture),
                ProviderUrl: GetCell(row, urlIndex),
                RawText: string.Join(",", row)));
        }

        return result;
    }

    private static IReadOnlyList<SourceFeedRow> ParsePlainTextRows(string sourceText)
    {
        return sourceText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select((line, index) => ParseLooseLine(line, "text", index + 1))
            .ToList();
    }

    private static IReadOnlyList<SourceFeedRow> ParsePlaylistRows(string sourceText)
    {
        var rows = new List<SourceFeedRow>();
        var lines = sourceText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (!line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var comma = line.LastIndexOf(',');
            if (comma >= 0 && comma + 1 < line.Length)
            {
                rows.Add(ParseLooseLine(line[(comma + 1)..], "m3u", index + 1));
            }
        }

        return rows;
    }

    private static IReadOnlyList<SourceFeedRow> ParseXmlRows(string sourceText)
    {
        var rows = new List<SourceFeedRow>();
        var document = XDocument.Parse(sourceText);
        rows.AddRange(document.Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .Select((element, index) =>
            {
                var title = element.Elements().FirstOrDefault(child => child.Name.LocalName == "title")?.Value ?? string.Empty;
                return ParseLooseLine(title, "rss", index + 1);
            }));
        rows.AddRange(document.Descendants()
            .Where(element => element.Name.LocalName == "outline")
            .Select((element, index) => ParseLooseLine(
                element.Attribute("text")?.Value ?? element.Attribute("title")?.Value ?? string.Empty,
                "opml",
                index + 1)));
        return rows;
    }

    private static SourceFeedRow ParseLooseLine(string line, string source, int rowNumber)
    {
        var text = line.Trim();
        var parts = text.Split(" - ", 2, StringSplitOptions.TrimEntries);
        var artist = parts.Length == 2 ? parts[0] : string.Empty;
        var title = parts.Length == 2 ? parts[1] : text;
        return new SourceFeedRow(title, artist, string.Empty, source, rowNumber.ToString(CultureInfo.InvariantCulture), string.Empty, text);
    }

    private static string BuildSearchText(string title, string artist, string album)
    {
        var parts = new[] { artist, title, album }
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length >= 2 || string.IsNullOrWhiteSpace(artist)
            ? string.Join(" ", parts)
            : string.Empty;
    }

    private static string NormalizeKey(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");

    private static int FindColumn(IReadOnlyList<string> header, params string[] names)
    {
        for (var index = 0; index < header.Count; index++)
        {
            if (names.Contains(NormalizeHeader(header[index]), StringComparer.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetCell(IReadOnlyList<string> row, int index)
        => index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    private static bool LooksLikeHeader(IReadOnlyList<string> row)
    {
        return row
            .Select(NormalizeHeader)
            .Any(value => value is "trackname" or "track" or "title" or "songname" or "song" or "artistname" or "artist" or "artists" or "albumname" or "album");
    }

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static List<List<string>> ParseDelimitedRows(string csvText)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csvText.Length; index++)
        {
            var ch = csvText[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < csvText.Length && csvText[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((ch == '\n' || ch == '\r') && !inQuotes)
            {
                if (ch == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                AddDelimitedRow(rows, row);
                row = [];
            }
            else
            {
                field.Append(ch);
            }
        }

        row.Add(field.ToString());
        AddDelimitedRow(rows, row);
        return rows;
    }

    private static void AddDelimitedRow(List<List<string>> rows, List<string> row)
    {
        if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            rows.Add(row);
        }
    }

    private sealed record SourceFeedRow(
        string Title,
        string Artist,
        string Album,
        string Source,
        string SourceId,
        string ProviderUrl,
        string RawText);

    private sealed record SpotifyTarget(string Type, string Id, bool RequiresUserToken, string ScopeHint);

    private sealed record SpotifyTokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record SpotifyPaging<T>
    {
        public int Total { get; init; }

        public string? Next { get; init; }

        public List<T> Items { get; init; } = [];
    }

    private sealed record SpotifyPlaylistItem(SpotifyTrack? Track);

    private sealed record SpotifySavedTrack(SpotifyTrack? Track);

    private sealed record SpotifySavedAlbum(SpotifyAlbum? Album);

    private sealed record SpotifyTopTracks(List<SpotifyTrack> Tracks);

    private sealed record SpotifyFollowedArtists(SpotifyArtistPaging Artists);

    private sealed record SpotifyArtistPaging
    {
        public List<SpotifyArtist> Items { get; init; } = [];

        public SpotifyCursor? Cursors { get; init; }
    }

    private sealed record SpotifyCursor(string? After);

    private sealed record SpotifyPlaylistSummary(string Id);

    private sealed record SpotifyTrack
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public SpotifyAlbumSummary? Album { get; init; }

        public List<SpotifyArtist> Artists { get; init; } = [];

        [JsonPropertyName("external_urls")]
        public SpotifyExternalUrls? ExternalUrls { get; init; }
    }

    private sealed record SpotifyAlbum
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public List<SpotifyArtist> Artists { get; init; } = [];

        public SpotifyPaging<SpotifyTrack>? Tracks { get; init; }

        [JsonPropertyName("external_urls")]
        public SpotifyExternalUrls? ExternalUrls { get; init; }
    }

    private sealed record SpotifyAlbumSummary
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed record SpotifyArtist
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("external_urls")]
        public SpotifyExternalUrls? ExternalUrls { get; init; }
    }

    private sealed record SpotifyExternalUrls
    {
        public string Spotify { get; init; } = string.Empty;
    }
}
