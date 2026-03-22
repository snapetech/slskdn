// <copyright file="PodMessageRoutingControllerTests.cs" company="slskdN Team">
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

public class PodMessageRoutingControllerTests
{
    [Fact]
    public async Task RouteMessageToPeers_WithOnlyWhitespacePeerIds_ReturnsBadRequest()
    {
        var router = new Mock<IPodMessageRouter>();
        var controller = new PodMessageRoutingController(
            NullLogger<PodMessageRoutingController>.Instance,
            router.Object);

        var result = await controller.RouteMessageToPeers(
            new PodMessagePeerRoutingRequest(
                new PodMessage { MessageId = "msg-1", ChannelId = "channel-1" },
                new[] { "   ", "\t" }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        router.Verify(
            service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteMessageToPeers_TrimsMessageAndPeerIdsBeforeRouting()
    {
        var router = new Mock<IPodMessageRouter>();
        router
            .Setup(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessageRoutingResult(
                true,
                "msg-1",
                "pod-1",
                1,
                1,
                0,
                TimeSpan.Zero));

        var controller = new PodMessageRoutingController(
            NullLogger<PodMessageRoutingController>.Instance,
            router.Object);

        var result = await controller.RouteMessageToPeers(
            new PodMessagePeerRoutingRequest(
                new PodMessage
                {
                    MessageId = " msg-1 ",
                    ChannelId = " channel-1 ",
                    PodId = " pod-1 ",
                    SenderPeerId = " sender-1 "
                },
                new[] { " peer-1 " }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        router.Verify(
            service => service.RouteMessageToPeersAsync(
                It.Is<PodMessage>(message =>
                    message.MessageId == "msg-1" &&
                    message.ChannelId == "channel-1" &&
                    message.PodId == "pod-1" &&
                    message.SenderPeerId == "sender-1"),
                It.Is<IEnumerable<string>>(peerIds => peerIds.SequenceEqual(new[] { "peer-1" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteMessageToPeers_FiltersDuplicatePeerIdsBeforeRouting()
    {
        var router = new Mock<IPodMessageRouter>();
        router
            .Setup(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessageRoutingResult(
                true,
                "msg-1",
                "pod-1",
                2,
                2,
                0,
                TimeSpan.Zero));

        var controller = new PodMessageRoutingController(
            NullLogger<PodMessageRoutingController>.Instance,
            router.Object);

        var result = await controller.RouteMessageToPeers(
            new PodMessagePeerRoutingRequest(
                new PodMessage { MessageId = "msg-1", ChannelId = "channel-1" },
                new[] { "peer-1", " peer-1 ", "peer-2" }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        router.Verify(
            service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.Is<IEnumerable<string>>(peerIds => peerIds.SequenceEqual(new[] { "peer-1", "peer-2" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
