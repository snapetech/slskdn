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
                    pod.Channels[0].Description == "room"),
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
}
