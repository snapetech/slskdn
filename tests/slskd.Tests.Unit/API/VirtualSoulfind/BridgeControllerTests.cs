// <copyright file="BridgeControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.VirtualSoulfind;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.VirtualSoulfind;
using slskd.VirtualSoulfind.Bridge;
using Xunit;

public class BridgeControllerTests
{
    [Fact]
    public async Task Search_WhenBridgeApiThrows_DoesNotLeakExceptionMessage()
    {
        var bridgeApi = new Mock<IBridgeApi>();
        bridgeApi
            .Setup(api => api.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new BridgeController(
            NullLogger<BridgeController>.Instance,
            bridgeApi.Object,
            Mock.Of<ISoulfindBridgeService>(),
            Mock.Of<IBridgeDashboard>());

        var result = await controller.Search(new BridgeSearchRequest("hello"), CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetStatus_WhenBridgeServiceThrows_DoesNotLeakExceptionMessage()
    {
        var bridgeService = new Mock<ISoulfindBridgeService>();
        bridgeService
            .Setup(service => service.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new BridgeController(
            NullLogger<BridgeController>.Instance,
            Mock.Of<IBridgeApi>(),
            bridgeService.Object,
            Mock.Of<IBridgeDashboard>());

        var result = await controller.GetStatus(CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
    }
}
