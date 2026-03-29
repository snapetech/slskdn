// <copyright file="SharesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Shares.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Shares;
using slskd.Shares.API;
using Xunit;

public class SharesControllerTests
{
    [Fact]
    public void Get_TrimsIdBeforeLookup()
    {
        var share = new Share("C:\\music")
        {
            Id = "share-1",
        };

        var service = new Mock<IShareService>();
        service.SetupGet(s => s.Hosts).Returns(new[] { new Host("local", new[] { share }) });

        var controller = new SharesController(service.Object);

        var result = controller.Get(" share-1 ");

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<Share>(ok.Value);
        Assert.Equal("share-1", returned.Id);
    }

    [Fact]
    public async Task BrowseShare_WithBlankId_ReturnsBadRequest()
    {
        var controller = new SharesController(Mock.Of<IShareService>());

        var result = await controller.BrowseShare("   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
