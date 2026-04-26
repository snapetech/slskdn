// <copyright file="WishlistControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Wishlist;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Wishlist;
using slskd.Wishlist.API;
using Xunit;

public class WishlistControllerTests
{
    [Fact]
    public async Task Create_TrimsSearchTextAndFilterBeforePersisting()
    {
        var service = new Mock<IWishlistService>();
        service
            .Setup(x => x.CreateAsync(It.IsAny<WishlistItem>()))
            .ReturnsAsync((WishlistItem item) =>
            {
                item.Id = Guid.NewGuid();
                return item;
            });

        var controller = new WishlistController(service.Object);

        var result = await controller.Create(new CreateWishlistRequest
        {
            SearchText = " artist - title ",
            Filter = " flac ",
            Enabled = true,
            AutoDownload = false,
            MaxResults = 25,
        });

        Assert.IsType<CreatedAtActionResult>(result);
        service.Verify(
            x => x.CreateAsync(It.Is<WishlistItem>(item =>
                item.SearchText == "artist - title" &&
                item.Filter == "flac" &&
                item.MaxResults == 25)),
            Times.Once);
    }

    [Fact]
    public async Task Update_WithBlankSearchTextAfterTrim_ReturnsBadRequest()
    {
        var controller = new WishlistController(Mock.Of<IWishlistService>());

        var result = await controller.Update(Guid.NewGuid(), new UpdateWishlistRequest
        {
            SearchText = "   ",
            Filter = " flac ",
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("SearchText is required", bad.Value);
    }

    [Fact]
    public async Task Create_WithNonPositiveMaxResults_ReturnsBadRequest()
    {
        var controller = new WishlistController(Mock.Of<IWishlistService>());

        var result = await controller.Create(new CreateWishlistRequest
        {
            SearchText = "artist - title",
            MaxResults = 0,
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("MaxResults must be greater than 0", bad.Value);
    }

    [Fact]
    public async Task Update_WithNonPositiveMaxResults_ReturnsBadRequest()
    {
        var controller = new WishlistController(Mock.Of<IWishlistService>());

        var result = await controller.Update(Guid.NewGuid(), new UpdateWishlistRequest
        {
            SearchText = "artist - title",
            MaxResults = -1,
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("MaxResults must be greater than 0", bad.Value);
    }

    [Fact]
    public async Task ImportCsv_TrimsFilterAndPassesOptions()
    {
        var service = new Mock<IWishlistService>();
        service
            .Setup(x => x.ImportCsvAsync(
                It.IsAny<string>(),
                It.IsAny<WishlistCsvImportOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WishlistCsvImportResult
            {
                TotalRows = 1,
                CreatedCount = 1,
            });
        var controller = new WishlistController(service.Object);

        var result = await controller.ImportCsv(new ImportWishlistCsvRequest
        {
            CsvText = "Track name,Artist name\nSong,Artist",
            Filter = " flac ",
            Enabled = false,
            AutoDownload = true,
            IncludeAlbum = true,
            MaxResults = 25,
        });

        Assert.IsType<OkObjectResult>(result);
        service.Verify(
            x => x.ImportCsvAsync(
                "Track name,Artist name\nSong,Artist",
                It.Is<WishlistCsvImportOptions>(options =>
                    options.Filter == "flac" &&
                    options.Enabled == false &&
                    options.AutoDownload &&
                    options.IncludeAlbum &&
                    options.MaxResults == 25),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportCsv_WithBlankCsv_ReturnsBadRequest()
    {
        var controller = new WishlistController(Mock.Of<IWishlistService>());

        var result = await controller.ImportCsv(new ImportWishlistCsvRequest
        {
            CsvText = "   ",
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("CsvText is required", bad.Value);
    }

    [Fact]
    public void ParseCsvTracks_HandlesTuneMyMusicHeadersAndEscapedFields()
    {
        const string csv = "Track name,Artist name,Album name\n\"Song, Part 1\",\"Artist \"\"Name\"\"\",Album";

        var tracks = WishlistService.ParseCsvTracks(csv, includeAlbum: true);

        var track = Assert.Single(tracks);
        Assert.Equal("Artist \"Name\" Song, Part 1 Album", track.SearchText);
        Assert.Equal(2, track.RowNumber);
    }

    [Fact]
    public void ParseCsvTracks_SkipsHeaderlessRowsWithoutArtistAndTitle()
    {
        const string csv = "Song Only\nTitle,Artist";

        var tracks = WishlistService.ParseCsvTracks(csv, includeAlbum: false);

        Assert.Equal(2, tracks.Count);
        Assert.Equal(string.Empty, tracks[0].SearchText);
        Assert.Equal("Artist Title", tracks[1].SearchText);
    }
}
