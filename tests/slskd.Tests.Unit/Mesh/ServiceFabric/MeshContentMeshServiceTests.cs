// <copyright file="MeshContentMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.Shares;
using Xunit;

public class MeshContentMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var service = new MeshContentMeshService(
            Mock.Of<ILogger<MeshContentMeshService>>(),
            Mock.Of<IShareService>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "MeshContent",
                Method = "SensitiveContentMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown method", reply.ErrorMessage);
        Assert.DoesNotContain("SensitiveContentMethod", reply.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_GetByContentId_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var shareService = new Mock<IShareService>();
        shareService
            .Setup(service => service.GetLocalRepository())
            .Throws(new InvalidOperationException("sensitive detail"));

        var service = new MeshContentMeshService(
            Mock.Of<ILogger<MeshContentMeshService>>(),
            shareService.Object);

        var call = new ServiceCall
        {
            ServiceName = "MeshContent",
            Method = "GetByContentId",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                contentId = "content:audio:track:mb-12345"
            })
        };

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.UnknownError, reply.StatusCode);
        Assert.Equal("Mesh content service error", reply.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", reply.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_GetByContentId_WhenFileTooLarge_ReturnsSanitizedError()
    {
        var repo = new Mock<IShareServiceLocalRepository>();
        repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
            .Returns(new ContentItem
            {
                ContentId = "content:audio:track:mb-12345",
                MaskedFilename = "masked-file.flac",
                IsAdvertisable = true
            });
        repo.Setup(repository => repository.FindFileInfo("masked-file.flac"))
            .Returns((Filename: Path.GetTempFileName(), Size: 40L * 1024 * 1024, LastWriteTimeUtc: DateTime.UtcNow));

        var shareService = new Mock<IShareService>();
        shareService.Setup(service => service.GetLocalRepository()).Returns(repo.Object);

        var service = new MeshContentMeshService(
            Mock.Of<ILogger<MeshContentMeshService>>(),
            shareService.Object);

        var call = new ServiceCall
        {
            ServiceName = "MeshContent",
            Method = "GetByContentId",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                contentId = "content:audio:track:mb-12345"
            })
        };

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.PayloadTooLarge, reply.StatusCode);
        Assert.Equal("File too large; use range request", reply.ErrorMessage);
        Assert.DoesNotContain("41943040", reply.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_GetByContentId_TrimsContentIdBeforeLookup()
    {
        var repo = new Mock<IShareServiceLocalRepository>();
        repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
            .Returns(new ContentItem
            {
                ContentId = "content:audio:track:mb-12345",
                MaskedFilename = "masked-file.flac",
                IsAdvertisable = false
            });

        var shareService = new Mock<IShareService>();
        shareService.Setup(service => service.GetLocalRepository()).Returns(repo.Object);

        var service = new MeshContentMeshService(
            Mock.Of<ILogger<MeshContentMeshService>>(),
            shareService.Object);

        await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "MeshContent",
                Method = "GetByContentId",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { contentId = " content:audio:track:mb-12345 " })
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        repo.Verify(repository => repository.FindContentItem("content:audio:track:mb-12345"), Times.Once);
    }

    [Fact]
    public async Task HandleCallAsync_GetByContentId_WithInvalidRange_ReturnsInvalidPayload()
    {
        var repo = new Mock<IShareServiceLocalRepository>();
        repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
            .Returns(new ContentItem
            {
                ContentId = "content:audio:track:mb-12345",
                MaskedFilename = "masked-file.flac",
                IsAdvertisable = true
            });
        repo.Setup(repository => repository.FindFileInfo("masked-file.flac"))
            .Returns((Filename: Path.GetTempFileName(), Size: 1024L, LastWriteTimeUtc: DateTime.UtcNow));

        var shareService = new Mock<IShareService>();
        shareService.Setup(service => service.GetLocalRepository()).Returns(repo.Object);

        var service = new MeshContentMeshService(
            Mock.Of<ILogger<MeshContentMeshService>>(),
            shareService.Object);

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "MeshContent",
                Method = "GetByContentId",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    contentId = "content:audio:track:mb-12345",
                    range = new { offset = -1, length = 10 }
                })
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, reply.StatusCode);
        Assert.Equal("Invalid range request", reply.ErrorMessage);
    }
}
