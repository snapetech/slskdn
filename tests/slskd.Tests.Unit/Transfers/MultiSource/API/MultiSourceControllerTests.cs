// <copyright file="MultiSourceControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.API;

using Microsoft.AspNetCore.Mvc;
using Moq;
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
}
