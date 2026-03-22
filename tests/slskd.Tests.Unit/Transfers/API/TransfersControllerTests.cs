// <copyright file="TransfersControllerTests.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
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
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
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
                It.Is<IEnumerable<(string Filename, long Size)>>(files =>
                    files.Single().Filename == "Music/song.flac"),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

    private static TransfersController CreateController(Mock<IDownloadService> downloads)
    {
        var transferService = new Mock<ITransferService>();
        transferService.SetupGet(service => service.Downloads).Returns(downloads.Object);
        transferService.SetupGet(service => service.Uploads).Returns(Mock.Of<IUploadService>());

        using var autoReplaceBackgroundService = new AutoReplaceBackgroundService(
            Mock.Of<IAutoReplaceService>(),
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IOptionsMonitor<slskd.Options>>(),
            new OptionsAtStartup());

        return new TransfersController(
            transferService.Object,
            Mock.Of<IOptionsSnapshot<slskd.Options>>(),
            Mock.Of<IAutoReplaceService>(),
            autoReplaceBackgroundService);
    }
}
