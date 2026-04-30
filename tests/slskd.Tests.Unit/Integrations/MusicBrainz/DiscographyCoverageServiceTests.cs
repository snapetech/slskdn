// <copyright file="DiscographyCoverageServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;
using slskd.Wishlist;
using Xunit;

public class DiscographyCoverageServiceTests
{
    [Fact]
    public async Task GetCoverageAsync_ResolvesMissingReleaseAndMarksHashDbTrackAvailable()
    {
        var releaseGraph = new Mock<IArtistReleaseGraphService>();
        var profile = new Mock<IDiscographyProfileService>();
        var client = new Mock<IMusicBrainzClient>();
        var hashDb = new Mock<IHashDbService>();
        var wishlist = new Mock<IWishlistService>();

        releaseGraph.Setup(x => x.GetArtistReleaseGraphAsync("artist-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGraph());
        profile.Setup(x => x.ApplyProfile(It.IsAny<ArtistReleaseGraph>(), It.IsAny<DiscographyProfileFilter>()))
            .Returns(new List<string> { "release-1" });
        hashDb.Setup(x => x.GetAlbumTargetAsync("release-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlbumTargetEntry?)null);
        client.Setup(x => x.GetReleaseAsync("release-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumTarget
            {
                MusicBrainzReleaseId = "release-1",
                Title = "Release One",
                Artist = "Artist",
            });
        hashDb.Setup(x => x.GetAlbumTracksAsync("release-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AlbumTargetTrackEntry
                {
                    ReleaseId = "release-1",
                    Position = 1,
                    RecordingId = "recording-1",
                    Title = "Song",
                    Artist = "Artist",
                },
            });
        hashDb.Setup(x => x.LookupHashesByRecordingIdAsync("recording-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HashDbEntry
                {
                    FlacKey = "flac-key",
                    Size = 123,
                    UseCount = 2,
                },
            });
        wishlist.Setup(x => x.ListAsync()).ReturnsAsync(new List<WishlistItem>());

        var service = CreateService(releaseGraph, profile, client, hashDb, wishlist);

        var result = await service.GetCoverageAsync(new DiscographyCoverageRequest { ArtistId = " artist-1 " });

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalTracks);
        Assert.Equal(1, result.CoveredTracks);
        var track = Assert.Single(Assert.Single(result.Releases).Tracks);
        Assert.Equal(DiscographyCoverageStatus.MeshAvailable, track.Status);
        Assert.Equal("flac-key", Assert.Single(track.Matches).FlacKey);
        client.Verify(x => x.GetReleaseAsync("release-1", It.IsAny<CancellationToken>()), Times.Once);
        hashDb.Verify(x => x.UpsertAlbumTargetAsync(It.Is<AlbumTarget>(album => album.MusicBrainzReleaseId == "release-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteMissingToWishlist_CreatesOnlyAbsentTrackSeeds()
    {
        var releaseGraph = new Mock<IArtistReleaseGraphService>();
        var profile = new Mock<IDiscographyProfileService>();
        var client = new Mock<IMusicBrainzClient>();
        var hashDb = new Mock<IHashDbService>();
        var wishlist = new Mock<IWishlistService>();

        releaseGraph.Setup(x => x.GetArtistReleaseGraphAsync("artist-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGraph());
        profile.Setup(x => x.ApplyProfile(It.IsAny<ArtistReleaseGraph>(), It.IsAny<DiscographyProfileFilter>()))
            .Returns(new List<string> { "release-1" });
        hashDb.Setup(x => x.GetAlbumTargetAsync("release-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumTargetEntry
            {
                ReleaseId = "release-1",
                Title = "Release One",
                Artist = "Artist",
            });
        hashDb.Setup(x => x.GetAlbumTracksAsync("release-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AlbumTargetTrackEntry
                {
                    ReleaseId = "release-1",
                    Position = 1,
                    RecordingId = "recording-1",
                    Title = "Missing Song",
                    Artist = "Artist",
                },
                new AlbumTargetTrackEntry
                {
                    ReleaseId = "release-1",
                    Position = 2,
                    RecordingId = "recording-2",
                    Title = "Seeded Song",
                    Artist = "Artist",
                },
            });
        hashDb.Setup(x => x.LookupHashesByRecordingIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HashDbEntry>());
        wishlist.Setup(x => x.ListAsync())
            .ReturnsAsync(new List<WishlistItem>
            {
                new() { SearchText = "Artist Seeded Song", Filter = "flac" },
            });
        wishlist.Setup(x => x.CreateAsync(It.IsAny<WishlistItem>()))
            .ReturnsAsync((WishlistItem item) =>
            {
                item.Id = Guid.NewGuid();
                return item;
            });

        var service = CreateService(releaseGraph, profile, client, hashDb, wishlist);

        var result = await service.PromoteMissingToWishlistAsync(new DiscographyWishlistPromotionRequest
        {
            ArtistId = "artist-1",
            Filter = "flac",
            MaxResults = 25,
        });

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.AlreadySeededCount);
        wishlist.Verify(x => x.CreateAsync(It.Is<WishlistItem>(item =>
            item.SearchText == "Artist Missing Song" &&
            item.Filter == "flac" &&
            item.MaxResults == 25 &&
            item.AutoDownload == false)), Times.Once);
    }

    private static DiscographyCoverageService CreateService(
        Mock<IArtistReleaseGraphService> releaseGraph,
        Mock<IDiscographyProfileService> profile,
        Mock<IMusicBrainzClient> client,
        Mock<IHashDbService> hashDb,
        Mock<IWishlistService> wishlist)
    {
        return new DiscographyCoverageService(
            releaseGraph.Object,
            profile.Object,
            client.Object,
            hashDb.Object,
            wishlist.Object,
            NullLogger<DiscographyCoverageService>.Instance);
    }

    private static ArtistReleaseGraph CreateGraph()
    {
        return new ArtistReleaseGraph
        {
            ArtistId = "artist-1",
            Name = "Artist",
            ReleaseGroups = new List<ReleaseGroup>
            {
                new()
                {
                    ReleaseGroupId = "rg-1",
                    Title = "Release One",
                    Type = ReleaseGroupType.Album,
                    Releases = new List<Release>
                    {
                        new()
                        {
                            ReleaseId = "release-1",
                            Title = "Release One",
                            ReleaseDate = "2020-01-01",
                        },
                    },
                },
            },
        };
    }
}
