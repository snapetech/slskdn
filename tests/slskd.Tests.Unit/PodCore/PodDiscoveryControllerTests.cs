// <copyright file="PodDiscoveryControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodDiscoveryControllerTests
{
    [Fact]
    public async Task DiscoverPodsByTags_WithOnlyCommaWhitespaceTags_ReturnsBadRequest()
    {
        var discovery = new Mock<IPodDiscoveryService>();
        var controller = new PodDiscoveryController(
            NullLogger<PodDiscoveryController>.Instance,
            discovery.Object);

        var result = await controller.DiscoverPodsByTags(" ,  , ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        discovery.Verify(
            service => service.DiscoverPodsByTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterPod_TrimsAndDeduplicatesTagsBeforeDispatch()
    {
        var discovery = new Mock<IPodDiscoveryService>();
        discovery
            .Setup(service => service.RegisterPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodRegistrationResult(true, "pod-1", Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var controller = new PodDiscoveryController(
            NullLogger<PodDiscoveryController>.Instance,
            discovery.Object);

        var result = await controller.RegisterPod(
            new Pod
            {
                PodId = " pod-1 ",
                Name = " Demo ",
                Description = " Desc ",
                Tags = new List<string> { " rock ", "rock", " ", " ambient " }
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        discovery.Verify(
            service => service.RegisterPodAsync(
                It.Is<Pod>(pod =>
                    pod.PodId == "pod-1" &&
                    pod.Name == "Demo" &&
                    pod.Description == "Desc" &&
                    pod.Tags.SequenceEqual(new[] { "rock", "ambient" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterPod_WhenServiceReturnsFailure_DoesNotLeakErrorMessage()
    {
        var discovery = new Mock<IPodDiscoveryService>();
        discovery
            .Setup(service => service.RegisterPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodRegistrationResult(false, "pod-1", Array.Empty<string>(), null, null, "sensitive detail"));

        var controller = new PodDiscoveryController(
            NullLogger<PodDiscoveryController>.Instance,
            discovery.Object);

        var result = await controller.RegisterPod(new Pod { PodId = "pod-1" }, CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to register pod", error.Value?.ToString() ?? string.Empty);
    }
}
