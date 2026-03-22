// <copyright file="DownloadsCompatibilityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Compatibility;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.Compatibility;
using slskd.Transfers;
using slskd.Transfers.Downloads;
using Xunit;

public class DownloadsCompatibilityControllerTests
{
    [Fact]
    public async Task CreateDownloads_WithOnlyBlankItems_ReturnsBadRequest()
    {
        var controller = CreateController(new Mock<IDownloadService>());

        var result = await controller.CreateDownloads(
            new DownloadRequest(new List<DownloadItem>
            {
                new("", "   ", "", null),
                new("   ", "", "", null),
            }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateDownloads_TrimsUserAndRemotePathBeforeEnqueue()
    {
        var downloadService = new Mock<IDownloadService>();
        downloadService
            .Setup(service => service.EnqueueAsync(
                "alice",
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Transfer>(), new List<string>()));

        var controller = CreateController(downloadService);

        var result = await controller.CreateDownloads(
            new DownloadRequest(new List<DownloadItem>
            {
                new(" alice ", " Music/song.flac ", "", null),
            }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        downloadService.Verify(
            service => service.EnqueueAsync(
                "alice",
                It.Is<IEnumerable<(string Filename, long Size)>>(files =>
                    files.Count() == 1 &&
                    files.First().Filename == "Music/song.flac"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDownload_WithWhitespacePaddedId_ParsesTrimmedGuid()
    {
        var id = Guid.NewGuid();
        var downloadService = new Mock<IDownloadService>();
        downloadService
            .Setup(service => service.Find(It.IsAny<Func<Transfer, bool>>()))
            .Returns(new Transfer
            {
                Id = id,
                Username = "alice",
                Filename = "Music/song.flac",
                State = TransferStates.Queued,
            });

        var controller = CreateController(downloadService);

        var result = await controller.GetDownload($"  {id}  ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    private static DownloadsCompatibilityController CreateController(Mock<IDownloadService> downloadService)
    {
        return new DownloadsCompatibilityController(
            downloadService.Object,
            NullLogger<DownloadsCompatibilityController>.Instance,
            Mock.Of<IOptionsMonitor<slskd.Options>>());
    }
}
