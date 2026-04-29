// <copyright file="TransfersControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.API;

using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Transfers;
using slskd.Transfers.API;
using slskd.Transfers.AutoReplace;
using slskd.Transfers.Downloads;
using slskd.Transfers.Uploads;
using Soulseek;
using Xunit;
using SlskdTransfer = slskd.Transfers.Transfer;

public class TransfersControllerTests
{
    [Fact]
    public async Task EnqueueAsync_WithWhitespaceFilename_ReturnsBadRequest()
    {
        var downloads = new Mock<IDownloadService>();
        var controller = CreateController(downloads);

        var result = await controller.EnqueueAsync(
            "alice",
            new[] { new QueueDownloadRequest { Filename = "   ", Size = 10 } });

        Assert.IsType<BadRequestObjectResult>(result);
        downloads.Verify(
            service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size, Guid? BatchId)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueAsync_TrimsUsernameAndFilenameBeforeEnqueue()
    {
        var downloads = new Mock<IDownloadService>();
        downloads
            .Setup(service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size, Guid? BatchId)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SlskdTransfer>(), new List<string>()));

        var controller = CreateController(downloads);

        var result = await controller.EnqueueAsync(
            " alice ",
            new[] { new QueueDownloadRequest { Filename = " Music/song.flac ", Size = 10 } });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        downloads.Verify(
            service => service.EnqueueAsync(
                "alice",
                It.Is<IEnumerable<(string Filename, long Size, Guid? BatchId)>>(files =>
                    files.Single().Filename == "Music/song.flac" && files.Single().BatchId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_WithMultipleFiles_AssignsSharedBatchId()
    {
        var downloads = new Mock<IDownloadService>();
        List<(string Filename, long Size, Guid? BatchId)> queued = new();
        downloads
            .Setup(service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size, Guid? BatchId)>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<(string Filename, long Size, Guid? BatchId)>, CancellationToken>((_, files, _) => queued = files.ToList())
            .ReturnsAsync((new List<SlskdTransfer>(), new List<string>()));

        var controller = CreateController(downloads);

        var result = await controller.EnqueueAsync(
            "alice",
            new[]
            {
                new QueueDownloadRequest { Filename = "Music/one.flac", Size = 10 },
                new QueueDownloadRequest { Filename = "Music/two.flac", Size = 20 },
            });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(2, queued.Count);
        Assert.NotNull(queued[0].BatchId);
        Assert.Equal(queued[0].BatchId, queued[1].BatchId);
    }

    [Fact]
    public void GetDownload_WithMismatchedRouteUsername_ReturnsNotFound()
    {
        var downloads = new Mock<IDownloadService>();
        var transferId = Guid.NewGuid();
        downloads
            .Setup(service => service.Find(It.IsAny<Expression<Func<SlskdTransfer, bool>>>()))
            .Returns(new SlskdTransfer { Id = transferId, Username = "bob" });

        var controller = CreateController(downloads);

        var result = controller.GetDownload("alice", transferId.ToString());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPlaceInQueueAsync_WithMismatchedRouteUsername_ReturnsNotFound()
    {
        var downloads = new Mock<IDownloadService>();
        var transferId = Guid.NewGuid();
        downloads
            .Setup(service => service.Find(It.IsAny<Expression<Func<SlskdTransfer, bool>>>()))
            .Returns(new SlskdTransfer { Id = transferId, Username = "bob" });

        var controller = CreateController(downloads);

        var result = await controller.GetPlaceInQueueAsync("alice", transferId.ToString());

        Assert.IsType<NotFoundResult>(result);
        downloads.Verify(service => service.GetPlaceInQueueAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task EnqueueAsync_WhenEnqueueThrows_DoesNotLeakExceptionMessage()
    {
        var downloads = new Mock<IDownloadService>();
        downloads
            .Setup(service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size, Guid? BatchId)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = CreateController(downloads);

        var result = await controller.EnqueueAsync(
            "alice",
            new[] { new QueueDownloadRequest { Filename = "Music/song.flac", Size = 10 } });

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Equal("Failed to enqueue downloads", error.Value);
    }

    [Fact]
    public async Task GetPlaceInQueueAsync_WhenQueueLookupThrows_DoesNotLeakExceptionMessage()
    {
        var downloads = new Mock<IDownloadService>();
        var transferId = Guid.NewGuid();
        downloads
            .Setup(service => service.Find(It.IsAny<Expression<Func<SlskdTransfer, bool>>>()))
            .Returns(new SlskdTransfer { Id = transferId, Username = "alice" });
        downloads
            .Setup(service => service.GetPlaceInQueueAsync(transferId))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = CreateController(downloads);

        var result = await controller.GetPlaceInQueueAsync("alice", transferId.ToString());

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Equal("Failed to get queue position", error.Value);
    }

    [Fact]
    public async Task GetUploadDiagnostics_WhenListenerAndSharesLookBad_ReturnsActionableWarnings()
    {
        var uploads = new Mock<IUploadService>();
        uploads
            .Setup(service => service.List(It.IsAny<Expression<Func<SlskdTransfer, bool>>>(), true))
            .Returns(new List<SlskdTransfer>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Username = "remote-user",
                    Filename = "shared\\file.flac",
                    RequestedAt = DateTime.UtcNow,
                    State = TransferStates.Completed | TransferStates.Errored,
                    Exception = "Connection failed",
                },
            });

        var options = new slskd.Options
        {
            Soulseek = new slskd.Options.SoulseekOptions
            {
                ListenIpAddress = "127.0.0.1",
                ListenPort = 1,
            },
        };

        var state = new slskd.State
        {
            Server = new slskd.ServerState
            {
                State = SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn,
            },
            Shares = new slskd.ShareState
            {
                Files = 0,
                Directories = 0,
            },
        };

        var controller = CreateController(uploads: uploads, options: options, state: state);

        var result = await controller.GetUploadDiagnostics();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UploadDiagnosticsResponse>(ok.Value);
        Assert.False(response.LocalListenProbe.Succeeded);
        Assert.Equal("127.0.0.1", response.ListenIpAddress);
        Assert.Equal(1, response.TotalUploadRecords);
        Assert.Contains(response.Warnings, warning => warning.Contains("loopback", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Warnings, warning => warning.Contains("No shared files", StringComparison.OrdinalIgnoreCase));
        Assert.Single(response.RecentUploads);
    }

    private static TransfersController CreateController(
        Mock<IDownloadService>? downloads = null,
        Mock<IUploadService>? uploads = null,
        slskd.Options? options = null,
        slskd.State? state = null)
    {
        var transferService = new Mock<ITransferService>();
        transferService.SetupGet(service => service.Downloads).Returns((downloads ?? new Mock<IDownloadService>()).Object);
        transferService.SetupGet(service => service.Uploads).Returns((uploads ?? new Mock<IUploadService>()).Object);

        var optionsSnapshot = new Mock<IOptionsSnapshot<slskd.Options>>();
        optionsSnapshot.SetupGet(snapshot => snapshot.Value).Returns(options ?? new slskd.Options());

        var stateSnapshot = new Mock<IStateSnapshot<slskd.State>>();
        stateSnapshot.SetupGet(snapshot => snapshot.Value).Returns(state ?? new slskd.State());

        using var autoReplaceBackgroundService = new AutoReplaceBackgroundService(
            Mock.Of<IAutoReplaceService>(),
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IOptionsMonitor<slskd.Options>>(),
            new OptionsAtStartup());

        return new TransfersController(
            transferService.Object,
            optionsSnapshot.Object,
            stateSnapshot.Object,
            Mock.Of<IAutoReplaceService>(),
            autoReplaceBackgroundService);
    }
}
