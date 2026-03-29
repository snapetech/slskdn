// <copyright file="MusicBrainzClientTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Integrations.MusicBrainz;
using slskd.Tests.Unit;
using Xunit;

public sealed class MusicBrainzClientTests
{
    [Fact]
    public async Task SearchRecordingsAsync_TrimsQueryAndDeduplicatesResults()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "recordings": [
                { "id": " rec-1 ", "title": " Song ", "artist-credit": [ { "name": "" }, { "name": " Artist " } ] },
                { "id": "rec-1", "title": "Song", "artist-credit": [ { "name": "Artist" } ] }
              ]
            }
            """);
        var client = CreateClient(handler);

        var results = await client.SearchRecordingsAsync("  query text  ", 10);

        var result = Assert.Single(results);
        Assert.Equal("rec-1", result.RecordingId);
        Assert.Equal("Song", result.Title);
        Assert.Equal("Artist", result.Artist);
        Assert.Contains("query", handler.LastRequestUri, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", handler.LastRequestUri, StringComparison.Ordinal);
    }

    private static MusicBrainzClient CreateClient(HttpMessageHandler handler)
    {
        var options = new slskd.Options
        {
            Integration = new slskd.Options.IntegrationOptions
            {
                MusicBrainz = new slskd.Options.IntegrationOptions.MusicBrainzOptions
                {
                    BaseUrl = "https://musicbrainz.example.test/ws/2",
                    TimeoutSeconds = 5,
                    RetryAttempts = 1,
                    UserAgent = "slskdn-test/1.0",
                },
            },
        };

        return new MusicBrainzClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            new TestOptionsMonitor<slskd.Options>(options),
            Mock.Of<ILogger<MusicBrainzClient>>());
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _json;

        public CapturingHttpMessageHandler(string json)
        {
            _json = json;
        }

        public string LastRequestUri { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString() ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json),
            });
        }
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
