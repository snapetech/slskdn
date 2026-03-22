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
}
