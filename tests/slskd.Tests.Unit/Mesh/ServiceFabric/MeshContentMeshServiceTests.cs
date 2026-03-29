// <copyright file="MeshContentMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.Collections.Generic;
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
    public async Task HandleStreamAsync_GetByContentIdRequest_SendsContentAndCloses()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        try
        {
            var repo = new Mock<IShareRepository>();
            repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
                .Returns((Domain: "audio", WorkId: "work-1", MaskedFilename: "masked-file.flac", IsAdvertisable: true, ModerationReason: string.Empty, CheckedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            repo.Setup(repository => repository.FindFileInfo("masked-file.flac"))
                .Returns((Filename: tempFile, Size: 4L));

            var shareService = new Mock<IShareService>();
            shareService.Setup(service => service.GetLocalRepository()).Returns(repo.Object);

            var service = new MeshContentMeshService(
                Mock.Of<ILogger<MeshContentMeshService>>(),
                shareService.Object);

            var stream = new TestMeshServiceStream(JsonSerializer.SerializeToUtf8Bytes(new
            {
                contentId = " content:audio:track:mb-12345 ",
            }));

            await service.HandleStreamAsync(
                stream,
                new MeshServiceContext { RemotePeerId = "peer-1" },
                CancellationToken.None);

            Assert.True(stream.Closed);
            Assert.Single(stream.SentPayloads);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, stream.SentPayloads[0]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

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
        var repo = new Mock<IShareRepository>();
        repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
            .Returns((Domain: "audio", WorkId: "work-1", MaskedFilename: "masked-file.flac", IsAdvertisable: true, ModerationReason: string.Empty, CheckedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        repo.Setup(repository => repository.FindFileInfo("masked-file.flac"))
            .Returns((Filename: Path.GetTempFileName(), Size: 40L * 1024 * 1024));

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
        var repo = new Mock<IShareRepository>();
        repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
            .Returns((Domain: "audio", WorkId: "work-1", MaskedFilename: "masked-file.flac", IsAdvertisable: false, ModerationReason: string.Empty, CheckedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

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
        var repo = new Mock<IShareRepository>();
        repo.Setup(repository => repository.FindContentItem("content:audio:track:mb-12345"))
            .Returns((Domain: "audio", WorkId: "work-1", MaskedFilename: "masked-file.flac", IsAdvertisable: true, ModerationReason: string.Empty, CheckedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        repo.Setup(repository => repository.FindFileInfo("masked-file.flac"))
            .Returns((Filename: Path.GetTempFileName(), Size: 1024L));

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

    private sealed class TestMeshServiceStream : MeshServiceStream
    {
        private readonly byte[] _requestPayload;

        public TestMeshServiceStream(byte[] requestPayload)
        {
            _requestPayload = requestPayload;
        }

        public bool Closed { get; private set; }

        public List<byte[]> SentPayloads { get; } = new();

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            SentPayloads.Add(data.ToArray());
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<byte[]?>(_requestPayload);
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Closed = true;
            return Task.CompletedTask;
        }
    }
}
