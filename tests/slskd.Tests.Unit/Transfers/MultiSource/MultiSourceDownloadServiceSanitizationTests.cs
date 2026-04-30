// <copyright file="MultiSourceDownloadServiceSanitizationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.MultiSource;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Transfers.MultiSource;
using Soulseek;
using Xunit;

public class MultiSourceDownloadServiceSanitizationTests
{
    [Fact]
    public async Task DownloadAsync_WithMixedSources_SequentialFailoverSkipsMeshOverlaySources()
    {
        var client = new Mock<ISoulseekClient>();
        client
            .Setup(c => c.DownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task<Stream>>>(),
                It.IsAny<long?>(),
                It.IsAny<long>(),
                It.IsAny<int?>(),
                It.IsAny<TransferOptions>(),
                It.IsAny<CancellationToken?>()))
            .Returns(async (
                string username,
                string remoteFilename,
                Func<Task<Stream>> outputStreamFactory,
                long? size,
                long startOffset,
                int? token,
                TransferOptions options,
                CancellationToken? cancellationToken) =>
            {
                var bytes = new byte[] { 1, 2, 3, 4 };
                var stream = await outputStreamFactory().ConfigureAwait(false);
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                return new Transfer(
                    TransferDirection.Download,
                    username,
                    remoteFilename,
                    token ?? 1,
                    TransferStates.Completed | TransferStates.Succeeded,
                    size ?? bytes.Length,
                    startOffset,
                    bytes.Length);
            });

        var service = new MultiSourceDownloadService(
            NullLogger<MultiSourceDownloadService>.Instance,
            client.Object,
            Mock.Of<IContentVerificationService>());

        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");

        try
        {
            var result = await service.DownloadAsync(
                new MultiSourceDownloadRequest
                {
                    Filename = "song.flac",
                    FileSize = 4,
                    OutputPath = outputPath,
                    Sources =
                    [
                        new VerifiedSource
                        {
                            Username = "mesh-peer",
                            FullPath = "mesh://song.flac",
                            Method = VerificationMethod.MeshOverlay,
                        },
                        new VerifiedSource
                        {
                            Username = "soulseek-peer",
                            FullPath = @"Music\song.flac",
                            Method = VerificationMethod.ContentSha256,
                        },
                    ],
                },
                CancellationToken.None);

            Assert.NotNull(result);
            client.Verify(
                c => c.DownloadAsync(
                    "mesh-peer",
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<Stream>>>(),
                    It.IsAny<long?>(),
                    It.IsAny<long>(),
                    It.IsAny<int?>(),
                    It.IsAny<TransferOptions>(),
                    It.IsAny<CancellationToken?>()),
                Times.Never);
            client.Verify(
                c => c.DownloadAsync(
                    "soulseek-peer",
                    @"Music\song.flac",
                    It.IsAny<Func<Task<Stream>>>(),
                    4,
                    0,
                    It.IsAny<int?>(),
                    It.IsAny<TransferOptions>(),
                    It.IsAny<CancellationToken?>()),
                Times.Once);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath))
            {
                System.IO.File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenTopLevelDownloadFlowThrows_ReturnsSanitizedErrorMessage()
    {
        var service = new MultiSourceDownloadService(
            NullLogger<MultiSourceDownloadService>.Instance,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IContentVerificationService>());

        var result = await service.DownloadAsync(
            new MultiSourceDownloadRequest
            {
                Filename = "song.flac",
                FileSize = 0,
                OutputPath = "\0invalid-output-path",
                Sources =
                [
                    new VerifiedSource
                    {
                        Username = "alice",
                        FullPath = @"Music\song.flac",
                    },
                ],
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Multi-source download failed", result.Error);
        Assert.DoesNotContain("invalid", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
