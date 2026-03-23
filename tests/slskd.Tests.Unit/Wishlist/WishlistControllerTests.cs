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
}
