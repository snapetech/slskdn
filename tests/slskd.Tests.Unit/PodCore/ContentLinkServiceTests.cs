// <copyright file="ContentLinkServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;
using slskd.PodCore;
using Xunit;

public class ContentLinkServiceTests
{
    [Fact]
    public async Task ValidateContentIdAsync_TrimsWhitespaceBeforeResolving()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        musicBrainz
            .Setup(client => client.GetRecordingAsync("recording-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackTarget(
                "recording-1",
                "Song",
                "Artist",
                TimeSpan.FromSeconds(180),
                1.0));

        var service = new ContentLinkService(musicBrainz.Object, NullLogger<ContentLinkService>.Instance);

        var result = await service.ValidateContentIdAsync("  content:audio:track:recording-1  ");

        Assert.True(result.IsValid);
        Assert.Equal("content:audio:track:recording-1", result.ContentId);
        Assert.Equal("content:audio:track:recording-1", result.Metadata!.ContentId);
    }

    [Fact]
    public async Task SearchContentAsync_UsesMusicBrainzSearchForAudio()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        musicBrainz
            .Setup(client => client.SearchRecordingsAsync("song", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordingSearchHit("recording-1", "Song", "Artist", "artist-1"),
                new RecordingSearchHit(" recording-2 ", "Other Song", "Other Artist", null),
            });

        var service = new ContentLinkService(musicBrainz.Object, NullLogger<ContentLinkService>.Instance);

        var results = await service.SearchContentAsync(" song ", " audio ", 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("content:audio:track:recording-1", results[0].ContentId);
        Assert.Equal("Artist", results[0].Subtitle);
        Assert.Equal("content:audio:track:recording-2", results[1].ContentId);
    }

    [Fact]
    public async Task SearchContentAsync_UnsupportedDomain_ReturnsEmptyWithoutCallingSearch()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        var service = new ContentLinkService(musicBrainz.Object, NullLogger<ContentLinkService>.Instance);

        var results = await service.SearchContentAsync("song", "video", 10);

        Assert.Empty(results);
        musicBrainz.Verify(client => client.SearchRecordingsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
