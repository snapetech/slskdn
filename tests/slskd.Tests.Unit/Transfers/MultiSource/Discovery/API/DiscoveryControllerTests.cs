// <copyright file="DiscoveryControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.MultiSource.Discovery.API;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Transfers.MultiSource.Discovery;
using slskd.Transfers.MultiSource.Discovery.API;
using Xunit;

public class DiscoveryControllerTests
{
    [Fact]
    public async Task Start_TrimsSearchTermBeforeDispatch()
    {
        var discovery = new Mock<ISourceDiscoveryService>();
        var controller = new DiscoveryController(discovery.Object);

        var result = await controller.Start(new DiscoveryStartRequest
        {
            SearchTerm = "  hello world  ",
            EnableHashVerification = true,
        });

        Assert.IsType<OkObjectResult>(result);
        discovery.Verify(x => x.StartDiscoveryAsync("hello world", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetSourcesByFilename_WithNonPositiveLimit_ReturnsBadRequest()
    {
        var controller = new DiscoveryController(Mock.Of<ISourceDiscoveryService>());

        var result = controller.GetSourcesByFilename("test", 0);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetSourcesBySize_WithNonPositiveSize_ReturnsBadRequest()
    {
        var controller = new DiscoveryController(Mock.Of<ISourceDiscoveryService>());

        var result = controller.GetSourcesBySize(0, 10);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
