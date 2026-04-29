// <copyright file="PodContentControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.PodCore;

using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodContentControllerTests
{
    [Fact]
    public async Task SearchContent_TrimsQueryAndDomainBeforeDispatch()
    {
        var contentLinkService = new Mock<IContentLinkService>();
        contentLinkService
            .Setup(service => service.SearchContentAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContentSearchResult>());

        var controller = CreateController(contentLinkService.Object);

        var result = await controller.SearchContent("  ambient techno  ", "  audio  ", 5, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        contentLinkService.Verify(
            service => service.SearchContentAsync("ambient techno", "audio", 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateContentLinkedPod_TrimsAndDeduplicatesTagsBeforeDispatch()
    {
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.CreateContentLinkedPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pod { PodId = "pod-1", Name = "Demo", FocusContentId = "content-1" });

        var controller = CreateController(Mock.Of<IContentLinkService>(), podService.Object);

        var result = await controller.CreateContentLinkedPod(
            new ContentLinkedPodRequest(
                " pod-1 ",
                " Demo ",
                PodVisibility.Listed,
                " content-1 ",
                new List<string> { " rock ", "rock", " ", " ambient " }),
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        podService.Verify(
            service => service.CreateContentLinkedPodAsync(
                It.Is<Pod>(pod =>
                    pod.PodId == "pod-1" &&
                    pod.Name == "Demo" &&
                    pod.FocusContentId == "content-1" &&
                    pod.Tags.SequenceEqual(new[] { "rock", "ambient" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateContentLinkedPod_WhenServiceThrowsArgumentException_ReturnsSanitizedBadRequest()
    {
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.CreateContentLinkedPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("sensitive detail"));

        var controller = CreateController(Mock.Of<IContentLinkService>(), podService.Object);

        var result = await controller.CreateContentLinkedPod(
            new ContentLinkedPodRequest("pod-1", "Demo", PodVisibility.Listed, "content-1"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid content-linked pod request", badRequest.Value);
    }

    private static PodContentController CreateController(IContentLinkService contentLinkService, IPodService? podService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(podService ?? Mock.Of<IPodService>());

        var controller = new PodContentController(
            contentLinkService,
            NullLogger<PodContentController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services.BuildServiceProvider()
                }
            }
        };

        return controller;
    }
}
