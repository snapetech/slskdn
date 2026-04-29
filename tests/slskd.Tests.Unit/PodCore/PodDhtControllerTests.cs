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
    public async Task PublishPod_TrimsPodFieldsBeforeDispatch()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodPublishResult(true, "pod-1", "key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)));

        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object);

        var result = await controller.PublishPod(
            new PublishPodRequest(
                new Pod
                {
                    PodId = " pod-1 ",
                    Name = " Ambient ",
                    Description = "  late night room  ",
                    FocusContentId = " content:mb:recording:abc ",
                    Tags = new List<string> { " electronic ", "electronic", " ambient " },
                    Members = new List<PodMember>
                    {
                        new() { PeerId = " peer-1 ", Role = " owner ", PublicKey = " key-1 " }
                    },
                    ExternalBindings = new List<ExternalBinding>
                    {
                        new() { Kind = " soulseek-room ", Mode = " readonly ", Identifier = " ambient-room " }
                    },
                    PrivateServicePolicy = new PodPrivateServicePolicy
                    {
                        GatewayPeerId = " peer-1 ",
                        RegisteredServices = new List<RegisteredService>
                        {
                            new() { Name = " web ui ", Description = " home server ", Host = " example.local ", Protocol = " tcp " }
                        },
                        AllowedDestinations = new List<AllowedDestination>
                        {
                            new() { HostPattern = " 192.168.1.10 ", Protocol = " tcp " }
                        }
                    },
                    Channels = new List<PodChannel>
                    {
                        new() { ChannelId = " general ", Name = " Main ", BindingInfo = " soulseek-room:ambient ", Description = "  room  " },
                    },
                }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        publisher.Verify(
            service => service.PublishAsync(
                It.Is<Pod>(pod =>
                    pod.PodId == "pod-1" &&
                    pod.Name == "Ambient" &&
                    pod.Description == "late night room" &&
                    pod.FocusContentId == "content:mb:recording:abc" &&
                    pod.Tags.SequenceEqual(new[] { "electronic", "ambient" }) &&
                    pod.Channels.Count == 1 &&
                    pod.Channels[0].ChannelId == "general" &&
                    pod.Channels[0].Name == "Main" &&
                    pod.Channels[0].BindingInfo == "soulseek-room:ambient" &&
                    pod.Channels[0].Description == "room" &&
                    pod.Members != null &&
                    pod.Members.Count == 1 &&
                    pod.Members[0].PeerId == "peer-1" &&
                    pod.Members[0].Role == "owner" &&
                    pod.Members[0].PublicKey == "key-1" &&
                    pod.ExternalBindings.Count == 1 &&
                    pod.ExternalBindings[0].Kind == "soulseek-room" &&
                    pod.ExternalBindings[0].Mode == "readonly" &&
                    pod.ExternalBindings[0].Identifier == "ambient-room" &&
                    pod.PrivateServicePolicy != null &&
                    pod.PrivateServicePolicy.GatewayPeerId == "peer-1" &&
                    pod.PrivateServicePolicy.RegisteredServices.Count == 1 &&
                    pod.PrivateServicePolicy.RegisteredServices[0].Name == "web ui" &&
                    pod.PrivateServicePolicy.RegisteredServices[0].Description == "home server" &&
                    pod.PrivateServicePolicy.RegisteredServices[0].Host == "example.local" &&
                    pod.PrivateServicePolicy.RegisteredServices[0].Protocol == "tcp" &&
                    pod.PrivateServicePolicy.AllowedDestinations.Count == 1 &&
                    pod.PrivateServicePolicy.AllowedDestinations[0].HostPattern == "192.168.1.10" &&
                    pod.PrivateServicePolicy.AllowedDestinations[0].Protocol == "tcp"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshPod_WithWhitespaceOnlyPodId_ReturnsBadRequest()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object);

        var result = await controller.RefreshPod("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        publisher.Verify(service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishPod_WhenPublisherReturnsFailure_DoesNotLeakErrorMessage()
    {
        var publisher = new Mock<IPodDhtPublisher>();
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodPublishResult(false, "pod-1", string.Empty, DateTimeOffset.MinValue, DateTimeOffset.MinValue, "sensitive detail"));

        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object);

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
        publisher
            .Setup(service => service.GetPublishedMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMetadataResult(false, "pod-1", null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, false, "sensitive detail"));

        var controller = new PodDhtController(
            NullLogger<PodDhtController>.Instance,
            publisher.Object);

        var result = await controller.GetPodMetadata("pod-1", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", notFound.Value?.ToString() ?? string.Empty);
        Assert.Contains("Pod not found", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("pod-1", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
