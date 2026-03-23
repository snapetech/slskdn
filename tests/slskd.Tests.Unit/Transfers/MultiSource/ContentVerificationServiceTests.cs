// <copyright file="ContentVerificationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource;

using Moq;
using slskd.Transfers.MultiSource;
using Soulseek;
using Xunit;

public class ContentVerificationServiceTests
{
    [Fact]
    public async Task VerifySourcesAsync_WhenDownloadThrows_ReturnsSanitizedFailureReason()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.DownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task<Stream>>>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int?>(),
                It.IsAny<TransferOptions>(),
                It.IsAny<CancellationToken?>()))
            .ThrowsAsync(new InvalidOperationException("sensitive verification detail"));

        var service = new ContentVerificationService(soulseekClient.Object);

        var result = await service.VerifySourcesAsync(
            new ContentVerificationRequest
            {
                Filename = "song.flac",
                FileSize = 1234,
                CandidateSources = new Dictionary<string, string>
                {
                    ["alice"] = @"Music\song.flac",
                },
                TimeoutMs = 1000,
            },
            CancellationToken.None);

        var failed = Assert.Single(result.FailedSources);
        Assert.Equal("alice", failed.Username);
        Assert.Equal("File too small for verification", failed.Reason);
        Assert.DoesNotContain("sensitive", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
