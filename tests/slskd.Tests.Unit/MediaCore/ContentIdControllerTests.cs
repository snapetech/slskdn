// <copyright file="ContentIdControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class ContentIdControllerTests
{
    [Fact]
    public async Task Register_TrimsExternalAndContentIdsBeforeRegistration()
    {
        var registry = new Mock<IContentIdRegistry>();
        var controller = new ContentIdController(NullLogger<ContentIdController>.Instance, registry.Object);

        var result = await controller.Register(
            new ContentIdRegistrationRequest("  mb:recording:12345  ", "  content:mb:recording:12345  "),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        registry.Verify(
            x => x.RegisterAsync("mb:recording:12345", "content:mb:recording:12345", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindByDomainAndType_TrimsInputsAndUsesNormalizedTypeFromNormalizedDomain()
    {
        var registry = new Mock<IContentIdRegistry>();
        registry.Setup(x => x.FindByDomainAndTypeAsync("audio", "recording", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "content:mb:recording:12345" });

        var controller = new ContentIdController(NullLogger<ContentIdController>.Instance, registry.Object);

        var result = await controller.FindByDomainAndType(" mb ", " recording ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        registry.Verify(
            x => x.FindByDomainAndTypeAsync("audio", "recording", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenExternalIdIsMissing_DoesNotEchoInput()
    {
        var registry = new Mock<IContentIdRegistry>();
        registry.Setup(x => x.ResolveAsync("mb:recording:12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var controller = new ContentIdController(NullLogger<ContentIdController>.Instance, registry.Object);

        var result = await controller.Resolve(" mb:recording:12345 ", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.DoesNotContain("12345", notFound.Value?.ToString() ?? string.Empty);
        Assert.Contains("External ID not found", notFound.Value?.ToString() ?? string.Empty);
    }
}
