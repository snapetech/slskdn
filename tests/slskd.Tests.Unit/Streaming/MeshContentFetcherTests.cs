// <copyright file="MeshContentFetcherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Streaming;

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Streaming;
using Xunit;

public class MeshContentFetcherTests
{
    [Fact]
    public async Task FetchAsync_WhenMeshServiceReplyFails_ReturnsSanitizedError()
    {
        var meshClient = new Mock<IMeshServiceClient>();
        meshClient
            .Setup(client => client.CallAsync("peer-1", It.IsAny<ServiceCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceReply
            {
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = "sensitive remote detail",
                Payload = Array.Empty<byte>()
            });

        var fetcher = new MeshContentFetcher(
            meshClient.Object,
            Mock.Of<ILogger<MeshContentFetcher>>());

        var result = await fetcher.FetchAsync("peer-1", "content:mb:recording:test", cancellationToken: CancellationToken.None);

        Assert.Equal("Mesh content fetch failed", result.Error);
        Assert.DoesNotContain("sensitive", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.SizeValid);
        Assert.False(result.HashValid);
    }

    [Fact]
    public async Task FetchAsync_WhenMeshClientThrows_ReturnsSanitizedError()
    {
        var meshClient = new Mock<IMeshServiceClient>();
        meshClient
            .Setup(client => client.CallAsync("peer-1", It.IsAny<ServiceCall>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive fetch detail"));

        var fetcher = new MeshContentFetcher(
            meshClient.Object,
            Mock.Of<ILogger<MeshContentFetcher>>());

        var result = await fetcher.FetchAsync("peer-1", "content:mb:recording:test", cancellationToken: CancellationToken.None);

        Assert.Equal("Mesh content fetch failed", result.Error);
        Assert.DoesNotContain("sensitive", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.SizeValid);
        Assert.False(result.HashValid);
    }
}
