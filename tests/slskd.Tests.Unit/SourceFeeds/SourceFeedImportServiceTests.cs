// <copyright file="SourceFeedImportServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SourceFeeds;

using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using slskd.SourceFeeds;
using Xunit;

public class SourceFeedImportServiceTests
{
    [Fact]
    public async Task PreviewAsync_CsvRows_ReturnsSuggestionsWithoutNetwork()
    {
        var handler = new QueueHttpHandler();
        var service = CreateService(handler);

        var result = await service.PreviewAsync(new SourceFeedImportRequest
        {
            SourceText = "Track name,Artist name,Album name\nSong,Artist,Album",
            SourceKind = "csv",
            IncludeAlbum = true,
        });

        var suggestion = Assert.Single(result.Suggestions);
        Assert.Equal("Artist Song Album", suggestion.SearchText);
        Assert.Equal("csv", result.SourceKind);
        Assert.Equal(0, handler.Requests.Count);
    }

    [Fact]
    public async Task PreviewAsync_SpotifyLikedWithoutToken_ReturnsScopeHint()
    {
        var service = CreateService(new QueueHttpHandler());

        var result = await service.PreviewAsync(new SourceFeedImportRequest
        {
            SourceText = "spotify:liked",
            SourceKind = "spotify",
        });

        Assert.True(result.RequiresAccessToken);
        Assert.Equal("user-library-read", result.RequiredScopeHint);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task PreviewAsync_DeduplicatesRepeatedLocalRows()
    {
        var handler = new QueueHttpHandler();
        var service = CreateService(handler);

        var result = await service.PreviewAsync(new SourceFeedImportRequest
        {
            SourceText = "Artist - Song\nartist - song",
            SourceKind = "text",
        });

        Assert.Single(result.Suggestions);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, handler.Requests.Count);
    }

    [Fact]
    public async Task PreviewAsync_SpotifyPlaylist_FetchesPlaylistItems()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueJson("""{"access_token":"app-token"}""");
        handler.EnqueueJson(
            """
            {
              "total": 1,
              "items": [
                {
                  "track": {
                    "id": "track-1",
                    "name": "Song",
                    "album": { "name": "Album" },
                    "artists": [{ "id": "artist-1", "name": "Artist" }],
                    "external_urls": { "spotify": "https://open.spotify.com/track/track-1" }
                  }
                }
              ]
            }
            """);
        var service = CreateService(handler, spotifyEnabled: true);

        var result = await service.PreviewAsync(new SourceFeedImportRequest
        {
            SourceText = "https://open.spotify.com/playlist/playlist-1",
            SourceKind = "spotify",
        });

        var suggestion = Assert.Single(result.Suggestions);
        Assert.Equal("Artist Song Album", suggestion.SearchText);
        Assert.Equal("spotify:playlist", suggestion.Source);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/v1/playlists/playlist-1/items", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task PreviewAsync_SpotifyLikedWithConnectedAccount_UsesStoredToken()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueJson(
            """
            {
              "total": 1,
              "items": [
                {
                  "track": {
                    "id": "track-1",
                    "name": "Song",
                    "album": { "name": "Album" },
                    "artists": [{ "id": "artist-1", "name": "Artist" }],
                    "external_urls": { "spotify": "https://open.spotify.com/track/track-1" }
                  }
                }
              ]
            }
            """);
        var service = CreateService(handler, connectedSpotifyToken: "connected-token");

        var result = await service.PreviewAsync(new SourceFeedImportRequest
        {
            SourceText = "spotify:liked",
            SourceKind = "spotify",
        });

        var suggestion = Assert.Single(result.Suggestions);
        Assert.Equal("Artist Song Album", suggestion.SearchText);
        Assert.False(result.RequiresAccessToken);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("connected-token", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    private static SourceFeedImportService CreateService(
        QueueHttpHandler handler,
        bool spotifyEnabled = false,
        string connectedSpotifyToken = "")
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var options = new Mock<IOptionsMonitor<global::slskd.Options>>();
        options.SetupGet(x => x.CurrentValue).Returns(new global::slskd.Options
        {
            Integration = new global::slskd.Options.IntegrationOptions
            {
                Spotify = new global::slskd.Options.IntegrationOptions.SpotifyOptions
                {
                    Enabled = spotifyEnabled,
                    ClientId = spotifyEnabled ? "client" : string.Empty,
                    ClientSecret = spotifyEnabled ? "secret" : string.Empty,
                },
            },
        });

        var spotifyConnection = new Mock<ISpotifyConnectionService>();
        spotifyConnection
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectedSpotifyToken);

        return new SourceFeedImportService(factory.Object, options.Object, spotifyConnection.Object);
    }

    private sealed class QueueHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public void EnqueueJson(string json)
        {
            responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }
    }
}
