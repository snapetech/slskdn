namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.API;
using slskd.Integrations.MusicBrainz.API.DTO;
using slskd.Integrations.MusicBrainz.Models;
using Xunit;

public class MusicBrainzControllerTests
{
    private readonly Mock<IMusicBrainzClient> client = new();
    private readonly Mock<IHashDbService> hashDb = new();
    private readonly Mock<IArtistReleaseGraphService> releaseGraph = new();
    private readonly MusicBrainzController controller;

    public MusicBrainzControllerTests()
    {
        controller = new MusicBrainzController(client.Object, hashDb.Object, releaseGraph.Object);
    }

    [Fact]
    public async Task ResolveTarget_WithReleaseId_UpsertsAlbum()
    {
        var release = new AlbumTarget
        {
            MusicBrainzReleaseId = "mb:release",
            Title = "Release",
            Artist = "Test Artist",
        };

        client.Setup(x => x.GetReleaseAsync("mb:release", It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        var result = await controller.ResolveTarget(
            new MusicBrainzTargetRequest { ReleaseId = "mb:release" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MusicBrainzTargetResponse>(ok.Value);
        Assert.Same(release, response.Album);
        hashDb.Verify(x => x.UpsertAlbumTargetAsync(release, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAlbumCompletion_ReturnsCompletionSummaries()
    {
        var release = new AlbumTargetEntry
        {
            ReleaseId = "release-1",
            Title = "Love",
            Artist = "Band",
            ReleaseDate = "2020-01-01",
            DiscogsReleaseId = "123",
        };

        hashDb.Setup(x => x.GetAlbumTargetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { release });

        hashDb.Setup(x => x.GetAlbumTracksAsync("release-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AlbumTargetTrackEntry
                {
                    ReleaseId = "release-1",
                    Position = 1,
                    Title = "Track One",
                    RecordingId = "rec-1",
                },
            });

        hashDb.Setup(x => x.LookupHashesByRecordingIdAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HashDbEntry
                {
                    FlacKey = "flackey",
                    Size = 1024,
                    UseCount = 2,
                    FirstSeenAt = 1,
                    LastUpdatedAt = 2,
                },
            });

        var result = await controller.GetAlbumCompletion(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AlbumCompletionResponse>(ok.Value);

        var album = Assert.Single(response.Albums);
        Assert.Equal("Love", album.Title);
        Assert.Equal(1, album.CompletedTracks);
        var track = Assert.Single(album.Tracks);
        Assert.True(track.Complete);
        var match = Assert.Single(track.Matches);
        Assert.Equal("flackey", match.FlacKey);
    }
}

