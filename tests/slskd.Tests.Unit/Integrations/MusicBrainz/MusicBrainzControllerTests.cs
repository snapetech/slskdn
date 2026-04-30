// <copyright file="MusicBrainzControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
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
    private readonly Mock<IDiscographyCoverageService> coverage = new();
    private readonly MusicBrainzController controller;

    public MusicBrainzControllerTests()
    {
        controller = new MusicBrainzController(client.Object, hashDb.Object, releaseGraph.Object, coverage.Object);
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
    public async Task ResolveTarget_TrimsIdentifiersBeforeLookup()
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
            new MusicBrainzTargetRequest { ReleaseId = " mb:release " },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        client.Verify(x => x.GetReleaseAsync("mb:release", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReleaseGraph_WithBlankArtistId_ReturnsBadRequest()
    {
        var result = await controller.GetReleaseGraph("   ", cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
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

    [Fact]
    public async Task GetDiscographyCoverage_TrimsArtistIdBeforeDispatch()
    {
        coverage.Setup(x => x.GetCoverageAsync(
                It.IsAny<DiscographyCoverageRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscographyCoverageResult { ArtistId = "artist-1" });

        var result = await controller.GetDiscographyCoverage(" artist-1 ", cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        coverage.Verify(x => x.GetCoverageAsync(
            It.Is<DiscographyCoverageRequest>(request => request.ArtistId == "artist-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteDiscographyCoverageToWishlist_WithInvalidMaxResults_ReturnsBadRequest()
    {
        var result = await controller.PromoteDiscographyCoverageToWishlist(
            "artist-1",
            new DiscographyWishlistPromotionRequest { MaxResults = 0 },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("MaxResults must be greater than 0", bad.Value);
    }
}
