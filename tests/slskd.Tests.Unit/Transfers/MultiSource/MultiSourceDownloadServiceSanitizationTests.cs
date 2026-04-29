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
