// <copyright file="PodChannelControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodChannelControllerTests
{
    [Fact]
    public async Task CreateChannel_TrimsRouteAndChannelFieldsBeforeDispatch()
    {
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetPodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pod { PodId = "pod-1" });
        podService
            .Setup(service => service.CreateChannelAsync(It.IsAny<string>(), It.IsAny<PodChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodChannel { ChannelId = "channel-1", Name = "General" });

        var controller = new PodChannelController(
            podService.Object,
            NullLogger<PodChannelController>.Instance);

        var result = await controller.CreateChannel(
            " pod-1 ",
            new PodChannel
            {
                ChannelId = " channel-1 ",
                Name = " General ",
                BindingInfo = " soulseek-room:ambient ",
                Description = "  main room  "
            },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        podService.Verify(
            service => service.CreateChannelAsync(
                "pod-1",
                It.Is<PodChannel>(channel =>
                    channel.ChannelId == "channel-1" &&
                    channel.Name == "General" &&
                    channel.BindingInfo == "soulseek-room:ambient" &&
                    channel.Description == "main room"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateChannel_WithWhitespaceOnlyRouteChannelId_ReturnsBadRequest()
    {
        var podService = new Mock<IPodService>();
        var controller = new PodChannelController(
            podService.Object,
            NullLogger<PodChannelController>.Instance);

        var result = await controller.UpdateChannel(
            " pod-1 ",
            "   ",
            new PodChannel { ChannelId = "channel-1", Name = "General" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        podService.Verify(
            service => service.UpdateChannelAsync(It.IsAny<string>(), It.IsAny<PodChannel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateChannel_WithWhitespaceOnlyName_ReturnsBadRequest()
    {
        var podService = new Mock<IPodService>();
        var controller = new PodChannelController(
            podService.Object,
            NullLogger<PodChannelController>.Instance);

        var result = await controller.CreateChannel(
            "pod-1",
            new PodChannel
            {
                ChannelId = "channel-1",
                Name = "   ",
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        podService.Verify(
            service => service.CreateChannelAsync(It.IsAny<string>(), It.IsAny<PodChannel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateChannel_WhenServiceThrowsArgumentException_ReturnsSanitizedBadRequest()
    {
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetPodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pod { PodId = "pod-1" });
        podService
            .Setup(service => service.CreateChannelAsync(It.IsAny<string>(), It.IsAny<PodChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("sensitive detail"));

        var controller = new PodChannelController(
            podService.Object,
            NullLogger<PodChannelController>.Instance);

        var result = await controller.CreateChannel(
            "pod-1",
            new PodChannel { ChannelId = "channel-1", Name = "General" },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid channel request", badRequest.Value);
    }
}
