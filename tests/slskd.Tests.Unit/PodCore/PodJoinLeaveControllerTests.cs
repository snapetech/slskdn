// <copyright file="PodJoinLeaveControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodJoinLeaveControllerTests
{
    [Fact]
    public async Task RequestJoin_TrimsPodPeerAndRoleBeforeDispatch()
    {
        var joinLeaveService = new Mock<IPodJoinLeaveService>();
        joinLeaveService
            .Setup(service => service.RequestJoinAsync(It.IsAny<PodJoinRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodJoinResult(true, "pod-1", "peer-1"));

        var controller = new PodJoinLeaveController(
            NullLogger<PodJoinLeaveController>.Instance,
            joinLeaveService.Object);

        var result = await controller.RequestJoin(
            new PodJoinRequest(" pod-1 ", " peer-1 ", " member ", " pub ", 1, " sig ", " hi ", " nonce "),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        joinLeaveService.Verify(
            service => service.RequestJoinAsync(
                It.Is<PodJoinRequest>(request =>
                    request.PodId == "pod-1" &&
                    request.PeerId == "peer-1" &&
                    request.RequestedRole == "member" &&
                    request.PublicKey == "pub" &&
                    request.Signature == "sig" &&
                    request.Message == "hi" &&
                    request.Nonce == "nonce"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelJoinRequest_TrimsPodAndPeerIdsBeforeDispatch()
    {
        var joinLeaveService = new Mock<IPodJoinLeaveService>();
        joinLeaveService
            .Setup(service => service.CancelJoinRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PodJoinLeaveController(
            NullLogger<PodJoinLeaveController>.Instance,
            joinLeaveService.Object);

        var result = await controller.CancelJoinRequest(" pod-1 ", " peer-1 ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        joinLeaveService.Verify(
            service => service.CancelJoinRequestAsync("pod-1", "peer-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
