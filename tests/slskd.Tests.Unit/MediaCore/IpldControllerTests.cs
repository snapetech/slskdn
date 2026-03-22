// <copyright file="IpldControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class IpldControllerTests
{
    [Fact]
    public async Task AddLinks_WithWhitespaceOnlyNameOrTarget_ReturnsBadRequest()
    {
        var mapper = new Mock<IIpldMapper>();
        var controller = new IpldController(NullLogger<IpldController>.Instance, mapper.Object);

        var result = await controller.AddLinks(
            "content:audio:track:1",
            new AddLinksRequest(new[] { new IpldLinkRequest(" ", "content:audio:album:1") }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mapper.Verify(
            x => x.AddLinksAsync(It.IsAny<string>(), It.IsAny<IEnumerable<IpldLink>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddLinks_TrimsLinkPayloadBeforePassingToMapper()
    {
        var mapper = new Mock<IIpldMapper>();
        var controller = new IpldController(NullLogger<IpldController>.Instance, mapper.Object);

        var result = await controller.AddLinks(
            "content:audio:track:1",
            new AddLinksRequest(new[] { new IpldLinkRequest(" album ", " content:audio:album:1 ", " main " ) }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mapper.Verify(
            x => x.AddLinksAsync(
                "content:audio:track:1",
                It.Is<IEnumerable<IpldLink>>(links =>
                    links.Single().Name == "album" &&
                    links.Single().Target == "content:audio:album:1" &&
                    links.Single().LinkName == "main"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
