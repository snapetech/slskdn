// <copyright file="CanonicalControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.VirtualSoulfind;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.VirtualSoulfind;
using slskd.VirtualSoulfind.ShadowIndex;
using Xunit;

public class CanonicalControllerTests
{
    [Fact]
    public async Task GetCanonical_WithBlankMbid_ReturnsBadRequest()
    {
        var controller = new CanonicalController(
            NullLogger<CanonicalController>.Instance,
            Mock.Of<IShadowIndexQuery>());

        var result = await controller.GetCanonical("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetCanonical_TrimsMbidBeforeDispatch()
    {
        var query = new Mock<IShadowIndexQuery>();
        query
            .Setup(service => service.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShadowIndexQueryResult());

        var controller = new CanonicalController(
            NullLogger<CanonicalController>.Instance,
            query.Object);

        await controller.GetCanonical(" mbid-1 ", CancellationToken.None);

        query.Verify(service => service.QueryAsync("mbid-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
