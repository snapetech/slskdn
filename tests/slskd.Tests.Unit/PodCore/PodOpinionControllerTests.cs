// <copyright file="PodOpinionControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodOpinionControllerTests
{
    [Fact]
    public async Task PublishOpinion_TrimsRouteAndOpinionFieldsBeforeDispatch()
    {
        var service = new Mock<IPodOpinionService>();
        service
            .Setup(s => s.PublishOpinionAsync(It.IsAny<string>(), It.IsAny<PodVariantOpinion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpinionPublishResult(true, "pod-1", "content-1", "variant-1"));

        var controller = new PodOpinionController(
            service.Object,
            Mock.Of<IPodOpinionAggregator>(),
            NullLogger<PodOpinionController>.Instance);

        var result = await controller.PublishOpinion(
            " pod-1 ",
            new PodVariantOpinion
            {
                ContentId = " content-1 ",
                VariantHash = " variant-1 ",
                Note = "  great mix  ",
                SenderPeerId = " peer-1 ",
                Signature = " sig-1 ",
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        service.Verify(
            s => s.PublishOpinionAsync(
                "pod-1",
                It.Is<PodVariantOpinion>(opinion =>
                    opinion.ContentId == "content-1" &&
                    opinion.VariantHash == "variant-1" &&
                    opinion.Note == "great mix" &&
                    opinion.SenderPeerId == "peer-1" &&
                    opinion.Signature == "sig-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetVariantOpinions_WithWhitespaceOnlyVariantHash_ReturnsBadRequest()
    {
        var service = new Mock<IPodOpinionService>();
        var controller = new PodOpinionController(
            service.Object,
            Mock.Of<IPodOpinionAggregator>(),
            NullLogger<PodOpinionController>.Instance);

        var result = await controller.GetVariantOpinions(" pod-1 ", " content-1 ", "   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        service.Verify(
            s => s.GetVariantOpinionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishOpinion_WhenServiceReturnsFailure_DoesNotLeakErrorMessage()
    {
        var service = new Mock<IPodOpinionService>();
        service
            .Setup(s => s.PublishOpinionAsync(It.IsAny<string>(), It.IsAny<PodVariantOpinion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpinionPublishResult(false, "pod-1", "content-1", "variant-1", "sensitive detail"));

        var controller = new PodOpinionController(
            service.Object,
            Mock.Of<IPodOpinionAggregator>(),
            NullLogger<PodOpinionController>.Instance);

        var result = await controller.PublishOpinion(
            "pod-1",
            new PodVariantOpinion
            {
                ContentId = "content-1",
                VariantHash = "variant-1",
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", badRequest.Value?.ToString() ?? string.Empty);
        Assert.Contains("Opinion could not be published", badRequest.Value?.ToString() ?? string.Empty);
    }
}
