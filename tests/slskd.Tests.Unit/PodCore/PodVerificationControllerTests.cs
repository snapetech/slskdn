// <copyright file="PodVerificationControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodVerificationControllerTests
{
    [Fact]
    public async Task CheckRole_TrimsRouteArgumentsBeforeDispatch()
    {
        var verifier = new Mock<IPodMembershipVerifier>();
        verifier
            .Setup(service => service.HasRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PodVerificationController(
            NullLogger<PodVerificationController>.Instance,
            verifier.Object);

        var result = await controller.CheckRole(" pod-1 ", " peer-1 ", " moderator ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        verifier.Verify(
            service => service.HasRoleAsync("pod-1", "peer-1", "moderator", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyMessage_WithBlankMessageIdOrPodId_ReturnsBadRequest()
    {
        var verifier = new Mock<IPodMembershipVerifier>();
        var controller = new PodVerificationController(
            NullLogger<PodVerificationController>.Instance,
            verifier.Object);

        var result = await controller.VerifyMessage(
            new PodMessage { MessageId = "   ", PodId = " pod-1 " },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        verifier.Verify(
            service => service.VerifyMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
