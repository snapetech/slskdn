// <copyright file="PodDhtControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.PodCore;

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodDhtControllerTests
{
    [Fact]
    public async Task PublishPod_UsesStoredLocalPodInsteadOfCallerSuppliedBody()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        var podService = new Mock<IPodService>();
        var localPod = new Pod
        {
            PodId = "pod-1",
            Name = "Trusted Local Pod",
            Description = "Stored local metadata",
            Tags = new List<string> { "trusted" },
            Members = new List<PodMember>
            {
                new() { PeerId = "local-peer", Role = "owner", PublicKey = "local-key" }
            },
        };

        podService
            .Setup(service => service.GetPodAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(localPod);
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodPublishResult(true, "pod-1", "key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)));

        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object,
            podService.Object);

        var result = await controller.PublishPod(
            new PublishPodRequest(
                new Pod
                {
                    PodId = " pod-1 ",
                    Name = "Caller Supplied Forgery",
                    Description = "This body must not be signed",
                    FocusContentId = "forged-content",
                    Tags = new List<string> { " electronic ", "electronic", " ambient " },
                }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        podService.Verify(service => service.GetPodAsync("pod-1", It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(
            service => service.PublishAsync(localPod, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshPod_WithWhitespaceOnlyPodId_ReturnsBadRequest()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        var podService = new Mock<IPodService>();
        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object,
            podService.Object);

        var result = await controller.RefreshPod("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        publisher.Verify(service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishPod_WhenPublisherReturnsFailure_DoesNotLeakErrorMessage()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetPodAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pod { PodId = "pod-1" });
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodPublishResult(false, "pod-1", string.Empty, DateTimeOffset.MinValue, DateTimeOffset.MinValue, "sensitive detail"));

        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object,
            podService.Object);

        var result = await controller.PublishPod(
            new PublishPodRequest(new Pod { PodId = "pod-1" }),
            CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to publish pod", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetPodMetadata_WhenPublisherReturnsNotFound_DoesNotLeakErrorMessage()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        var podService = new Mock<IPodService>();
        publisher
            .Setup(service => service.GetPublishedMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMetadataResult(false, "pod-1", null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, false, "sensitive detail"));

        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object,
            podService.Object);

        var result = await controller.GetPodMetadata("pod-1", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", notFound.Value?.ToString() ?? string.Empty);
        Assert.Contains("Pod not found", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("pod-1", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
