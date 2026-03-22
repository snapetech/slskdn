// <copyright file="MeshControllerTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.API;

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh;
using slskd.Mesh.API;
using slskd.Mesh.Messages;
using Xunit;

public class MeshControllerTests
{
    [Fact]
    public async Task PublishHash_WithWhitespaceFields_ReturnsBadRequest()
    {
        var meshSync = new Mock<IMeshSyncService>();
        var controller = CreateController(meshSync);

        var result = await controller.PublishHash(new PublishHashRequest
        {
            FlacKey = "   ",
            ByteHash = " hash ",
            Size = 10
        });

        Assert.IsType<BadRequestObjectResult>(result);
        meshSync.Verify(
            service => service.PublishHashAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishHash_TrimsFieldsBeforePublish()
    {
        var meshSync = new Mock<IMeshSyncService>();
        var controller = CreateController(meshSync);

        var result = await controller.PublishHash(new PublishHashRequest
        {
            FlacKey = " flac-key ",
            ByteHash = " byte-hash ",
            Size = 10
        });

        Assert.IsType<OkObjectResult>(result);
        meshSync.Verify(
            service => service.PublishHashAsync(
                "flac-key",
                "byte-hash",
                10,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MergeEntries_WithInvalidEntryPayload_ReturnsBadRequest()
    {
        var meshSync = new Mock<IMeshSyncService>();
        var controller = CreateController(meshSync);

        var result = await controller.MergeEntries(
            "peer-1",
            new MergeEntriesRequest
            {
                Entries = new[]
                {
                    new MeshHashEntry { FlacKey = "key-1", ByteHash = " ", Size = 10 }
                }
            });

        Assert.IsType<BadRequestObjectResult>(result);
        meshSync.Verify(
            service => service.MergeEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<MeshHashEntry>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MergeEntries_TrimsFromUserAndEntriesBeforeMerge()
    {
        var meshSync = new Mock<IMeshSyncService>();
        meshSync.SetupGet(service => service.Stats).Returns(new MeshSyncStats());
        meshSync
            .Setup(service => service.MergeEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<MeshHashEntry>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var controller = CreateController(meshSync);

        var result = await controller.MergeEntries(
            " peer-1 ",
            new MergeEntriesRequest
            {
                Entries = new[]
                {
                    new MeshHashEntry { FlacKey = " key-1 ", ByteHash = " hash-1 ", Size = 10 }
                }
            });

        Assert.IsType<OkObjectResult>(result);
        meshSync.Verify(
            service => service.MergeEntriesAsync(
                "peer-1",
                It.Is<IEnumerable<MeshHashEntry>>(entries =>
                    entries.Single().FlacKey == "key-1" &&
                    entries.Single().ByteHash == "hash-1" &&
                    entries.Single().Size == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MergeEntries_FiltersDuplicateEntriesBeforeMerge()
    {
        var meshSync = new Mock<IMeshSyncService>();
        meshSync.SetupGet(service => service.Stats).Returns(new MeshSyncStats());
        meshSync
            .Setup(service => service.MergeEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<MeshHashEntry>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var controller = CreateController(meshSync);

        var result = await controller.MergeEntries(
            "peer-1",
            new MergeEntriesRequest
            {
                Entries = new[]
                {
                    new MeshHashEntry { FlacKey = "key-1", ByteHash = "hash-1", Size = 10, SeqId = 1 },
                    new MeshHashEntry { FlacKey = " key-1 ", ByteHash = " hash-1 ", Size = 10, SeqId = 1 }
                }
            });

        Assert.IsType<OkObjectResult>(result);
        meshSync.Verify(
            service => service.MergeEntriesAsync(
                "peer-1",
                It.Is<IEnumerable<MeshHashEntry>>(entries => entries.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MeshController CreateController(Mock<IMeshSyncService> meshSync)
    {
        meshSync.SetupGet(service => service.Stats).Returns(new MeshSyncStats());

        return new MeshController(
            meshSync.Object,
            new MeshStatsCollector(NullLogger<MeshStatsCollector>.Instance, Mock.Of<IServiceProvider>()),
            Mock.Of<INatDetector>());
    }
}
