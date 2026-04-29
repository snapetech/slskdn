// <copyright file="BackfillControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Backfill;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Backfill;
using slskd.Backfill.API;
using Xunit;

public class BackfillControllerTests
{
    [Fact]
    public async Task GetCandidates_WithNonPositiveLimit_UsesDefaultLimit()
    {
        var backfill = new Mock<IBackfillSchedulerService>();
        backfill
            .Setup(service => service.GetCandidatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BackfillCandidate>());

        var controller = new BackfillController(backfill.Object);

        await controller.GetCandidates(0);

        backfill.Verify(service => service.GetCandidatesAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BackfillFile_TrimsRequestValuesBeforeDispatch()
    {
        var backfill = new Mock<IBackfillSchedulerService>();
        backfill
            .Setup(service => service.BackfillFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackfillResult { Success = true });

        var controller = new BackfillController(backfill.Object);

        await controller.BackfillFile(new BackfillFileRequest
        {
            PeerId = " peer-1 ",
            Path = " /music/test.flac ",
            Size = 123,
        });

        backfill.Verify(service => service.BackfillFileAsync("peer-1", "/music/test.flac", 123, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BackfillFile_WithWhitespaceValues_ReturnsBadRequest()
    {
        var controller = new BackfillController(Mock.Of<IBackfillSchedulerService>());

        var result = await controller.BackfillFile(new BackfillFileRequest
        {
            PeerId = "   ",
            Path = "   ",
            Size = 123,
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
