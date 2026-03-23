// <copyright file="MultiSourceControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.API;

using Microsoft.AspNetCore.Http;
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
                It.IsAny<int?>(),
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
                It.IsAny<int?>(),
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

    [Fact]
    public async Task RunTest_WhenDownloadFails_DoesNotLeakUnderlyingError()
    {
        var multiSource = new Mock<IMultiSourceDownloadService>();
        multiSource
            .Setup(service => service.FindVerifiedSourcesAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentVerificationResult
            {
                SourcesByHash = new Dictionary<string, List<VerifiedSource>>
                {
                    ["hash-1"] = new()
                    {
                        new VerifiedSource
                        {
                            Username = "alice",
                            FullPath = @"Music\song.flac",
                            ContentHash = "hash-1",
                            Method = VerificationMethod.FlacStreamInfoMd5,
                        },
                        new VerifiedSource
                        {
                            Username = "bob",
                            FullPath = @"Music\song.flac",
                            ContentHash = "hash-1",
                            Method = VerificationMethod.FlacStreamInfoMd5,
                        },
                    },
                },
            });
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

        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.SearchAsync(
                It.IsAny<SearchQuery>(),
                It.IsAny<Action<SearchResponse>>(),
                It.IsAny<SearchScope>(),
                It.IsAny<int?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<SearchQuery, Action<SearchResponse>, SearchScope, int?, SearchOptions, CancellationToken?>((_, responseHandler, _, _, _, _) =>
            {
                responseHandler(new SearchResponse(
                    "alice",
                    1,
                    uploadSpeed: 1000,
                    hasFreeUploadSlot: true,
                    queueLength: 0,
                    fileList: new List<Soulseek.File> { new(1, @"Music\song.flac", 1234, ".flac", null) }));
                responseHandler(new SearchResponse(
                    "bob",
                    1,
                    uploadSpeed: 900,
                    hasFreeUploadSlot: true,
                    queueLength: 0,
                    fileList: new List<Soulseek.File> { new(1, @"Music\song.flac", 1234, ".flac", null) }));
            })
            .ReturnsAsync((Soulseek.Search)null!);

        var controller = new MultiSourceController(
            multiSource.Object,
            soulseekClient.Object,
            Mock.Of<ITransferService>(),
            Mock.Of<ISourceDiscoveryService>(),
            Mock.Of<IContentVerificationService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        var result = await controller.RunTest(new TestRequest
        {
            SearchText = "song",
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TestResult>(ok.Value);
        Assert.DoesNotContain("sensitive detail", payload.Error ?? string.Empty);
        Assert.Equal("Multi-source test download failed", payload.Error);
    }
}
