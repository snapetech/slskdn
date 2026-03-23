// <copyright file="PodMessageBackfillControllerTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Identity;
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

    [Fact]
    public async Task SyncAllPods_UsesLocalMembershipAndLastSeenState()
    {
        var backfill = new Mock<IPodMessageBackfill>();
        var podService = new Mock<IPodService>();
        var profileService = new Mock<IProfileService>();

        profileService.Setup(service => service.GetMyProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerProfile { PeerId = "peer:self" });
        podService.Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pod>
            {
                new() { PodId = "pod-1", Name = "Pod One" },
                new() { PodId = "pod-2", Name = "Pod Two" },
            });
        podService.Setup(service => service.GetMembersAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new() { PeerId = "peer:self" } });
        podService.Setup(service => service.GetMembersAsync("pod-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new() { PeerId = "peer:other" } });
        backfill.Setup(service => service.GetLastSeenTimestamps("pod-1"))
            .Returns(new Dictionary<string, long> { ["general"] = 42 });
        backfill.Setup(service => service.GetLastSeenTimestamps("pod-2"))
            .Returns(new Dictionary<string, long> { ["general"] = 24 });
        backfill.Setup(service => service.SyncOnRejoinAsync("pod-1", It.IsAny<IReadOnlyDictionary<string, long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodBackfillResult(true, "pod-1", 1, 2, TimeSpan.Zero));

        var controller = new PodMessageBackfillController(
            backfill.Object,
            NullLogger<PodMessageBackfillController>.Instance,
            podService.Object,
            profileService.Object);

        var result = await controller.SyncAllPods(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<PodBackfillResult>>(ok.Value);
        var results = payload.ToList();

        Assert.Single(results);
        Assert.Equal("pod-1", results[0].PodId);
        backfill.Verify(service => service.SyncOnRejoinAsync("pod-1", It.Is<IReadOnlyDictionary<string, long>>(x => x["general"] == 42), It.IsAny<CancellationToken>()), Times.Once);
        backfill.Verify(service => service.SyncOnRejoinAsync("pod-2", It.IsAny<IReadOnlyDictionary<string, long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
