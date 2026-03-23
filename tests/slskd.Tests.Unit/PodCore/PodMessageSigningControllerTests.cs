// <copyright file="PodMessageSigningControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodMessageSigningControllerTests
{
    [Fact]
    public async Task SignMessage_TrimsPrivateKeyAndMessageFieldsBeforeDispatch()
    {
        var signer = new Mock<IMessageSigner>();
        signer
            .Setup(service => service.SignMessageAsync(It.IsAny<PodMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessage { MessageId = "msg-1" });

        var controller = new PodMessageSigningController(
            NullLogger<PodMessageSigningController>.Instance,
            signer.Object);

        var result = await controller.SignMessage(
            new MessageSigningRequest(
                new PodMessage
                {
                    MessageId = " msg-1 ",
                    PodId = " pod-1 ",
                    ChannelId = " general ",
                    SenderPeerId = " peer-1 ",
                    Body = " hello ",
                    Signature = " sig ",
                },
                " secret "),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        signer.Verify(
            service => service.SignMessageAsync(
                It.Is<PodMessage>(message =>
                    message.MessageId == "msg-1" &&
                    message.PodId == "pod-1" &&
                    message.ChannelId == "general" &&
                    message.SenderPeerId == "peer-1" &&
                    message.Body == "hello" &&
                    message.Signature == "sig"),
                "secret",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyMessage_WithWhitespaceOnlyMessageId_ReturnsBadRequest()
    {
        var signer = new Mock<IMessageSigner>();
        var controller = new PodMessageSigningController(
            NullLogger<PodMessageSigningController>.Instance,
            signer.Object);

        var result = await controller.VerifyMessage(
            new PodMessage { MessageId = "   " },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        signer.Verify(service => service.VerifyMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyMessage_ReturnsSanitizedSuccessPayload()
    {
        var signer = new Mock<IMessageSigner>();
        signer
            .Setup(service => service.VerifyMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PodMessageSigningController(
            NullLogger<PodMessageSigningController>.Instance,
            signer.Object);

        var result = await controller.VerifyMessage(
            new PodMessage { MessageId = "msg-1", PodId = "pod-1" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("isValid", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("msg-1", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
