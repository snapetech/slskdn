// <copyright file="DownloadsCompatibilityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Compatibility;

using System.Linq;
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
    public async Task CreateDownloads_WithWhitespaceOnlyItemFields_ReturnsBadRequest()
    {
        var downloadService = new Mock<IDownloadService>();
        var controller = CreateController(downloadService);

        var result = await controller.CreateDownloads(
            new DownloadRequest(new List<DownloadItem> { new(" ", "   ", string.Empty) }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        downloadService.Verify(
            service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateDownloads_WithMixedValidAndBlankItems_ReturnsBadRequest()
    {
        var downloadService = new Mock<IDownloadService>();
        var controller = CreateController(downloadService);

        var result = await controller.CreateDownloads(
            new DownloadRequest(new List<DownloadItem>
            {
                new("alice", "/music/song.flac", string.Empty),
                new("bob", "   ", string.Empty)
            }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        downloadService.Verify(
            service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateDownloads_TrimsUserAndRemotePathBeforeEnqueue()
    {
        var downloadService = new Mock<IDownloadService>();
        downloadService
            .Setup(service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Transfer>(), new List<string>()));

        var controller = CreateController(downloadService);

        var result = await controller.CreateDownloads(
            new DownloadRequest(new List<DownloadItem> { new(" alice ", " /music/song.flac ", string.Empty) }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        downloadService.Verify(
            service => service.EnqueueAsync(
                "alice",
                It.Is<IEnumerable<(string Filename, long Size)>>(files =>
                    files.Single().Filename == "/music/song.flac"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateDownloads_WhenEnqueueThrows_DoesNotLeakExceptionMessage()
    {
        var downloadService = new Mock<IDownloadService>();
        downloadService
            .Setup(service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(string Filename, long Size)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = CreateController(downloadService);

        var result = await controller.CreateDownloads(
            new DownloadRequest(new List<DownloadItem> { new("alice", "/music/song.flac", string.Empty) }),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var text = ok.Value?.ToString() ?? string.Empty;
        Assert.DoesNotContain("sensitive detail", text);
        Assert.Contains("Failed to enqueue download", text);
    }

    private static DownloadsCompatibilityController CreateController(Mock<IDownloadService> downloadService)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.SetupGet(monitor => monitor.CurrentValue).Returns(new slskd.Options());

        return new DownloadsCompatibilityController(
            downloadService.Object,
            NullLogger<DownloadsCompatibilityController>.Instance,
            optionsMonitor.Object);
    }
}
