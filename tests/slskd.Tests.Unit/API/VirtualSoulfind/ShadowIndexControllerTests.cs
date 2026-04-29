// <copyright file="ShadowIndexControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.VirtualSoulfind;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.VirtualSoulfind;
using slskd.VirtualSoulfind.ShadowIndex;
using Xunit;

public class ShadowIndexControllerTests
{
    [Fact]
    public async Task GetShadowIndex_WithBlankMbid_ReturnsBadRequest()
    {
        var controller = new ShadowIndexController(
            NullLogger<ShadowIndexController>.Instance,
            Mock.Of<IShadowIndexQuery>());

        var result = await controller.GetShadowIndex("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetShadowIndex_TrimsMbidBeforeDispatch()
    {
        var query = new Mock<IShadowIndexQuery>();
        query
            .Setup(service => service.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShadowIndexQueryResult());

        var controller = new ShadowIndexController(
            NullLogger<ShadowIndexController>.Instance,
            query.Object);

        await controller.GetShadowIndex(" mbid-1 ", CancellationToken.None);

        query.Verify(service => service.QueryAsync("mbid-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShadowIndex_WhenQueryThrows_DoesNotLeakMbid()
    {
        var query = new Mock<IShadowIndexQuery>();
        query
            .Setup(service => service.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new ShadowIndexController(
            NullLogger<ShadowIndexController>.Instance,
            query.Object);

        var result = await controller.GetShadowIndex("mbid-1", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.Contains("Failed to query shadow index", error.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("mbid-1", error.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetShadowIndex_WhenQuerySucceeds_DoesNotEchoMbid()
    {
        var query = new Mock<IShadowIndexQuery>();
        query
            .Setup(service => service.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShadowIndexQueryResult());

        var controller = new ShadowIndexController(
            NullLogger<ShadowIndexController>.Instance,
            query.Object);

        var result = await controller.GetShadowIndex("mbid-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.DoesNotContain("mbid-1", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("variants", ok.Value?.ToString() ?? string.Empty);
    }
}
