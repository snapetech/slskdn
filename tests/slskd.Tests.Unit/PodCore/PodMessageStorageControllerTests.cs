// <copyright file="PodMessageStorageControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodMessageStorageControllerTests
{
    [Fact]
    public async Task SearchMessages_TrimsPodQueryAndChannelBeforeDispatch()
    {
        var storage = new Mock<IPodMessageStorage>();
        storage
            .Setup(service => service.SearchMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PodMessage>());

        var controller = new PodMessageStorageController(
            storage.Object,
            NullLogger<PodMessageStorageController>.Instance);

        var result = await controller.SearchMessages(
            " pod-1 ",
            "  hello world  ",
            " general ",
            25,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        storage.Verify(
            service => service.SearchMessagesAsync("pod-1", "hello world", "general", 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupChannelMessages_WithWhitespaceOnlyChannelId_ReturnsBadRequest()
    {
        var storage = new Mock<IPodMessageStorage>();
        var controller = new PodMessageStorageController(
            storage.Object,
            NullLogger<PodMessageStorageController>.Instance);

        var result = await controller.CleanupChannelMessages(" pod-1 ", "   ", 1000, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        storage.Verify(
            service => service.DeleteMessagesInChannelOlderThanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchMessages_WithOutOfRangeLimit_ReturnsBadRequest()
    {
        var storage = new Mock<IPodMessageStorage>();
        var controller = new PodMessageStorageController(
            storage.Object,
            NullLogger<PodMessageStorageController>.Instance);

        var result = await controller.SearchMessages(
            "pod-1",
            "hello",
            null,
            501,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        storage.Verify(
            service => service.SearchMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
