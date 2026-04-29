// <copyright file="BackfillSchedulerServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Backfill;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Backfill;
using slskd.Capabilities;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Mesh;
using Soulseek;
using Xunit;

public class BackfillSchedulerServiceTests
{
    [Fact]
    public async Task BackfillFileAsync_WhenHeaderDownloadThrows_ReturnsSanitizedErrorMessage()
    {
        var hashDb = new Mock<IHashDbService>();
        hashDb
            .Setup(service => service.UpsertFlacEntryAsync(It.IsAny<FlacInventoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        hashDb
            .Setup(service => service.MarkFlacHashFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.DownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task<System.IO.Stream>>>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int?>(),
                It.IsAny<TransferOptions>(),
                It.IsAny<CancellationToken?>()))
            .ThrowsAsync(new InvalidOperationException("sensitive transfer detail"));

        var service = new BackfillSchedulerService(
            hashDb.Object,
            Mock.Of<IMeshSyncService>(),
            soulseekClient.Object,
            Mock.Of<ICapabilityService?>(),
            NullLogger<BackfillSchedulerService>.Instance);

        var result = await service.BackfillFileAsync("alice", @"Music\song.flac", 1234, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed to read FLAC header", result.Error);
        Assert.DoesNotContain("sensitive", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
