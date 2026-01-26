// <copyright file="MusicBrainzClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd;
    using slskd.Integrations.MusicBrainz.Models;

    /// <summary>
    ///     MusicBrainz HTTP client implementation.
    /// </summary>
    public class MusicBrainzClient : IMusicBrainzClient
    {
        private static readonly Regex DiscogsReleaseRegex = new(@"discogs\.com\/release\/(?<release>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
        private readonly ILogger<MusicBrainzClient> log;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicBrainzClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="optionsMonitor">Options monitor.</param>
        /// <param name="log">Logger.</param>
        public MusicBrainzClient(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<slskd.Options> optionsMonitor,
            ILogger<MusicBrainzClient> log)
        {
            this.httpClientFactory = httpClientFactory;
            this.optionsMonitor = optionsMonitor;
            this.log = log;
            serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
        }

        private slskd.Options AppOptions => optionsMonitor.CurrentValue;
        private slskd.Options.IntegrationOptions.MusicBrainzOptions MusicBrainzOptions => AppOptions.Integration.MusicBrainz;

        /// <inheritdoc />
        public async Task<AlbumTarget?> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
            {
                throw new ArgumentException("Release ID must be supplied", nameof(releaseId));
            }

            var requestUrl = $"{MusicBrainzOptions.BaseUrl.TrimEnd('/')}/release/{releaseId}?fmt=json&inc=recordings+artists+labels+discids+isrcs+relations";
            var response = await GetAsync<ReleaseResponse>(requestUrl, cancellationToken).ConfigureAwait(false);
            return response is null ? null : MapToAlbumTarget(response);
        }

        /// <inheritdoc />
        public async Task<TrackTarget?> GetRecordingAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                throw new ArgumentException("Recording ID must be supplied", nameof(recordingId));
            }

            var requestUrl = $"{MusicBrainzOptions.BaseUrl.TrimEnd('/')}/recording/{recordingId}?fmt=json&inc=artists+isrcs";
            var response = await GetAsync<RecordingResponse>(requestUrl, cancellationToken).ConfigureAwait(false);
            return response is null ? null : MapToTrackTarget(response);
        }

        /// <inheritdoc />
        public async Task<AlbumTarget?> GetReleaseByDiscogsReleaseIdAsync(string discogsReleaseId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(discogsReleaseId))
            {
                throw new ArgumentException("Discogs release ID must be supplied", nameof(discogsReleaseId));
            }

            var query = WebUtility.UrlEncode(discogsReleaseId);
            var requestUrl = $"{MusicBrainzOptions.BaseUrl.TrimEnd('/')}/release/?query=discogsrelease:{query}&fmt=json&limit=1";
            var response = await GetAsync<ReleaseSearchResponse>(requestUrl, cancellationToken).ConfigureAwait(false);
            var releaseId = response?.Releases?.FirstOrDefault()?.Id;

            return releaseId is null ? null : await GetReleaseAsync(releaseId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<RecordingSearchHit>> SearchRecordingsAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<RecordingSearchHit>();
            }

            var encoded = WebUtility.UrlEncode(query);
            var requestUrl = $"{MusicBrainzOptions.BaseUrl.TrimEnd('/')}/recording?query={encoded}&fmt=json&limit={Math.Max(1, Math.Min(limit, 100))}";
            var response = await GetAsync<RecordingSearchResponse>(requestUrl, cancellationToken).ConfigureAwait(false);
            var recordings = response?.Recordings;
            if (recordings is null || recordings.Length == 0)
            {
                return Array.Empty<RecordingSearchHit>();
            }

            return recordings
                .Select(r =>
                {
                    var ac = r.ArtistCredit?.FirstOrDefault();
                    return new RecordingSearchHit(
                        r.Id,
                        r.Title ?? string.Empty,
                        ac?.Name ?? string.Empty,
                        ac?.Artist?.Id);
                })
                .ToList();
        }

        private async Task<T?> GetAsync<T>(string requestUri, CancellationToken cancellationToken) where T : class
        {
            var options = MusicBrainzOptions;

            for (var attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
                request.Headers.UserAgent.ParseAdd(options.UserAgent);

                var http = httpClientFactory.CreateClient();
                http.Timeout = options.Timeout;

                try
                {
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }

                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return await JsonSerializer.DeserializeAsync<T>(stream, serializerOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < options.RetryAttempts && (ex is HttpRequestException || ex is TaskCanceledException))
                {
                    log.LogWarning(ex, "MusicBrainz request {Uri} failed on attempt {Attempt}; retrying", requestUri, attempt);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "MusicBrainz request {Uri} failed: {Message}", requestUri, ex.Message);
                    throw;
                }
            }
        }

        private AlbumTarget MapToAlbumTarget(ReleaseResponse release)
        {
            var tracks = release.Media?
                .SelectMany(media => media.Tracks ?? Array.Empty<TrackResponse>())
                .Select((track, index) => MapTrackResponse(track, index + 1))
                .Where(track => track is not null)
                .Cast<TrackTarget>()
                .ToList() ?? new List<TrackTarget>();

            var metadata = new ReleaseMetadata
            {
                Country = release.Country,
                Label = release.LabelInfo?.Select(li => li.Label?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                Status = release.Status,
                ReleaseDate = ParseReleaseDate(release.Date),
            };

            return new AlbumTarget
            {
                MusicBrainzReleaseId = release.Id,
                DiscogsReleaseId = ExtractDiscogsReleaseId(release.Relations),
                Title = release.Title ?? string.Empty,
                Artist = FormatArtistCredit(release.ArtistCredit),
                Metadata = metadata,
                Tracks = tracks,
            };
        }

        private TrackTarget? MapTrackResponse(TrackResponse trackResponse, int fallbackPosition)
        {
            var recording = trackResponse?.Recording;

            if (recording is null)
            {
                return null;
            }

            var position = ResolvePosition(trackResponse.Position, fallbackPosition);
            var duration = recording.Length.HasValue ? TimeSpan.FromMilliseconds(recording.Length.Value) : TimeSpan.Zero;

            return new TrackTarget
            {
                MusicBrainzRecordingId = recording.Id,
                Position = position,
                Title = string.IsNullOrWhiteSpace(trackResponse.Title) ? recording.Title : trackResponse.Title,
                Artist = FormatArtistCredit(recording.ArtistCredit),
                Duration = duration,
                Isrc = recording.Isrcs?.FirstOrDefault(),
            };
        }

        private TrackTarget MapToTrackTarget(RecordingResponse recording)
        {
            var duration = recording.Length.HasValue ? TimeSpan.FromMilliseconds(recording.Length.Value) : TimeSpan.Zero;

            return new TrackTarget
            {
                MusicBrainzRecordingId = recording.Id,
                Position = 0,
                Title = recording.Title,
                Artist = FormatArtistCredit(recording.ArtistCredit),
                Duration = duration,
                Isrc = recording.Isrcs?.FirstOrDefault(),
            };
        }

        private static int ResolvePosition(string? position, int fallback)
        {
            if (!string.IsNullOrWhiteSpace(position))
            {
                if (int.TryParse(position, NumberStyles.Integer, CultureInfo.InvariantCulture, out var direct))
                {
                    return direct;
                }

                var segments = position.Split('.', StringSplitOptions.RemoveEmptyEntries);

                for (var i = segments.Length - 1; i >= 0; i--)
                {
                    if (int.TryParse(segments[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                }
            }

            return fallback;
        }

        private static DateOnly? ParseReleaseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var formats = new[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" };

            foreach (var format in formats)
            {
                if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static string FormatArtistCredit(ArtistCreditResponse[]? credits)
        {
            if (credits is null || credits.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            foreach (var credit in credits)
            {
                builder.Append(credit.Name);
                builder.Append(credit.JoinPhrase ?? string.Empty);
            }

            return builder.ToString().Trim();
        }

        private static string? ExtractDiscogsReleaseId(RelationResponse[]? relations)
        {
            if (relations is null)
            {
                return null;
            }

            foreach (var relation in relations)
            {
                var type = relation.Type;

                if (string.Equals(type, "discogs release", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type, "discogs master", StringComparison.OrdinalIgnoreCase))
                {
                    var resource = relation.Url?.Resource;

                    if (string.IsNullOrWhiteSpace(resource))
                    {
                        continue;
                    }

                    var match = DiscogsReleaseRegex.Match(resource);

                    if (match.Success)
                    {
                        return match.Groups["release"].Value;
                    }
                }
            }

            return null;
        }

        private sealed record ReleaseResponse(
            string Id,
            string? Title,
            string? Country,
            string? Date,
            string? Status,
            ArtistCreditResponse[]? ArtistCredit,
            LabelInfoResponse[]? LabelInfo,
            MediaResponse[]? Media,
            RelationResponse[]? Relations);

        private sealed record MediaResponse(string? Format, int? TrackCount, TrackResponse[]? Tracks);

        private sealed record TrackResponse(string? Position, string? Title, RecordingResponse? Recording);

        private sealed record RecordingResponse(
            string Id,
            string Title,
            long? Length,
            string[]? Isrcs,
            ArtistCreditResponse[]? ArtistCredit);

        private sealed record ArtistCreditResponse(string Name, string? JoinPhrase);

        private sealed record LabelInfoResponse(LabelResponse? Label);

        private sealed record LabelResponse(string? Name);

        private sealed record RelationResponse(string? Type, RelationUrlResponse? Url);

        private sealed record RelationUrlResponse(string? Resource);

        private sealed record ReleaseSearchResponse(ReleaseSummary[]? Releases);

        private sealed record ReleaseSummary(string Id);

        private sealed record RecordingSearchResponse(RecordingSearchItem[]? Recordings);

        private sealed record RecordingSearchItem(
            string Id,
            string? Title,
            [property: System.Text.Json.Serialization.JsonPropertyName("artist-credit")] ArtistCreditSearchItem[]? ArtistCredit);

        private sealed record ArtistCreditSearchItem(string? Name, ArtistRef? Artist);

        private sealed record ArtistRef(string? Id);
    }
}



