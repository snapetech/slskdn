// <copyright file="MultiSourceControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.API;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Transfers;
using slskd.Transfers.MultiSource;
using slskd.Transfers.MultiSource.API;
using slskd.Transfers.MultiSource.Discovery;
using Soulseek;
using Xunit;

public class MultiSourceControllerTests
{
    [Fact]
    public async Task GetTopUsers_WhenSearchThrows_DoesNotLeakExceptionMessage()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.SearchAsync(
                It.IsAny<SearchQuery>(),
                It.IsAny<Action<SearchResponse>>(),
                It.IsAny<SearchScope>(),
                It.IsAny<int>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new MultiSourceController(
            Mock.Of<IMultiSourceDownloadService>(),
            soulseekClient.Object,
            Mock.Of<ITransferService>(),
            Mock.Of<ISourceDiscoveryService>(),
            Mock.Of<IContentVerificationService>());

        var result = await controller.GetTopUsers("hello");

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Search failed", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task DownloadFile_WhenSearchThrows_DoesNotLeakExceptionMessage()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.SearchAsync(
                It.IsAny<SearchQuery>(),
                It.IsAny<Action<SearchResponse>>(),
                It.IsAny<SearchScope>(),
                It.IsAny<int>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new MultiSourceController(
            Mock.Of<IMultiSourceDownloadService>(),
            soulseekClient.Object,
            Mock.Of<ITransferService>(),
            Mock.Of<ISourceDiscoveryService>(),
            Mock.Of<IContentVerificationService>());

        var result = await controller.DownloadFile(new FileSourceRequest
        {
            Filename = "song.flac",
            Size = 1234,
        });

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Search failed", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task SwarmDownload_WhenDownloadFails_DoesNotLeakUnderlyingError()
    {
        var multiSource = new Mock<IMultiSourceDownloadService>();
        multiSource
            .Setup(service => service.DownloadAsync(
                It.IsAny<MultiSourceDownloadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MultiSourceDownloadResult
            {
                Success = false,
                Filename = "song.flac",
                Error = "sensitive detail",
                TotalTimeMs = 1000,
                SourcesUsed = 2,
            });

        var discovery = new Mock<ISourceDiscoveryService>();
        discovery
            .Setup(service => service.GetSourcesBySize(1234, 100))
            .Returns(new List<DiscoveredSource>
            {
                new() { Username = "alice", Filename = "Music/song.flac", UploadSpeed = 1000, Size = 1234 },
                new() { Username = "bob", Filename = "Music/song.flac", UploadSpeed = 900, Size = 1234 },
            });

        var controller = new MultiSourceController(
            multiSource.Object,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<ITransferService>(),
            discovery.Object,
            Mock.Of<IContentVerificationService>());

        var result = await controller.SwarmDownload(new SwarmDownloadRequest
        {
            Filename = "song.flac",
            Size = 1234,
            UseDiscoveryDb = true,
            SkipVerification = true,
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ok.Value?.ToString() ?? string.Empty;
        Assert.DoesNotContain("sensitive detail", payload);
        Assert.Contains("Swarm download failed", payload);
    }
}
