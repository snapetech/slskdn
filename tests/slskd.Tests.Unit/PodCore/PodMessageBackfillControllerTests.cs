// <copyright file="PodMessageBackfillControllerTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodMessageBackfillControllerTests
{
    [Fact]
    public async Task SyncOnRejoin_WithBlankChannelKeyOrNonPositiveTimestamp_ReturnsBadRequest()
    {
        var backfill = new Mock<IPodMessageBackfill>();
        var controller = new PodMessageBackfillController(backfill.Object, NullLogger<PodMessageBackfillController>.Instance);

        var result = await controller.SyncOnRejoin(
            "pod-1",
            new Dictionary<string, long>
            {
                ["general"] = 10,
                ["   "] = 20
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        backfill.Verify(
            service => service.SyncOnRejoinAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, long>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncOnRejoin_TrimsPodAndChannelIdsBeforeDispatch()
    {
        var backfill = new Mock<IPodMessageBackfill>();
        backfill
            .Setup(service => service.SyncOnRejoinAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodBackfillResult(true, "pod-1", 1, 0, TimeSpan.Zero));

        var controller = new PodMessageBackfillController(backfill.Object, NullLogger<PodMessageBackfillController>.Instance);

        var result = await controller.SyncOnRejoin(
            " pod-1 ",
            new Dictionary<string, long> { [" general "] = 42 },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        backfill.Verify(
            service => service.SyncOnRejoinAsync(
                "pod-1",
                It.Is<IReadOnlyDictionary<string, long>>(timestamps =>
                    timestamps.Count == 1 &&
                    timestamps.ContainsKey("general") &&
                    timestamps["general"] == 42),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncOnRejoin_WithDuplicateChannelIdsAfterTrim_ReturnsBadRequest()
    {
        var backfill = new Mock<IPodMessageBackfill>();
        var controller = new PodMessageBackfillController(backfill.Object, NullLogger<PodMessageBackfillController>.Instance);

        var result = await controller.SyncOnRejoin(
            "pod-1",
            new Dictionary<string, long>
            {
                ["general"] = 10,
                [" general "] = 20
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        backfill.Verify(
            service => service.SyncOnRejoinAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, long>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
