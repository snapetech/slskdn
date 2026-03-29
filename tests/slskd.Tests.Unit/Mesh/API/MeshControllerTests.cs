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

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("published", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("flac-key", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupKey_WhenFound_DoesNotEchoFlacKey()
    {
        var meshSync = new Mock<IMeshSyncService>();
        meshSync
            .Setup(service => service.LookupHashAsync("flac-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeshHashEntry { FlacKey = "flac-key", ByteHash = "hash-1", Size = 10 });

        var controller = CreateController(meshSync);

        var result = await controller.LookupKey(" flac-key ");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("found = True", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("{ flacKey =", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        meshSync.Verify(service => service.LookupHashAsync("flac-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LookupKey_WhenMissing_DoesNotEchoFlacKey()
    {
        var meshSync = new Mock<IMeshSyncService>();
        meshSync
            .Setup(service => service.LookupHashAsync("flac-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeshHashEntry?)null);

        var controller = CreateController(meshSync);

        var result = await controller.LookupKey(" flac-key ");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("found = False", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("flac-key", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        meshSync.Verify(service => service.LookupHashAsync("flac-key", It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task DetectNatType_WhenDetectorThrows_DoesNotLeakExceptionMessage()
    {
        var natDetector = new Mock<INatDetector>();
        natDetector
            .Setup(detector => detector.DetectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new MeshController(
            Mock.Of<IMeshSyncService>(),
            new MeshStatsCollector(NullLogger<MeshStatsCollector>.Instance, Mock.Of<IServiceProvider>()),
            natDetector.Object);

        var result = await controller.DetectNatType();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", ok.Value?.ToString() ?? string.Empty);
        Assert.Contains("NAT detection failed", ok.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task SyncWithPeer_WhenSyncFails_DoesNotLeakErrorMessage()
    {
        var meshSync = new Mock<IMeshSyncService>();
        meshSync.SetupGet(service => service.Stats).Returns(new MeshSyncStats());
        meshSync
            .Setup(service => service.TrySyncWithPeerAsync("peer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeshSyncResult
            {
                Success = false,
                PeerUsername = "peer-1",
                Error = "sensitive detail"
            });

        var controller = CreateController(meshSync);

        var result = await controller.SyncWithPeer("peer-1");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", badRequest.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to sync with peer", badRequest.Value?.ToString() ?? string.Empty);
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
