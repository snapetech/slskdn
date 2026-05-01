// <copyright file="UploadServiceLifecycleTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.Uploads;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.Events;
using slskd.Files;
using slskd.Relay;
using slskd.Tests.Unit;
using slskd.Shares;
using slskd.Transfers;
using slskd.Transfers.Uploads;
using slskd.Users;
using Soulseek;
using Xunit;

public class UploadServiceLifecycleTests
{
    [Fact]
    public void Dispose_DisposesOwnedGovernorAndQueue()
    {
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetGroup(It.IsAny<string>())).Returns(Application.DefaultGroup);
        var eventService = new EventService(Mock.Of<IDbContextFactory<EventsDbContext>>());
        var service = new UploadService(
            new FileService(optionsMonitor),
            userService.Object,
            Mock.Of<ISoulseekClient>(),
            optionsMonitor,
            Mock.Of<IShareService>(),
            Mock.Of<IRelayService>(),
            Mock.Of<IDbContextFactory<TransfersDbContext>>(),
            new EventBus(eventService));

        Assert.Equal(2, optionsMonitor.ListenerCount);

        service.Dispose();

        Assert.Equal(0, optionsMonitor.ListenerCount);
    }

    [Fact]
    public async Task UploadAsync_WhenSoulseekConnectionIsRefused_MarksUploadFailed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new TransfersDbContext(dbOptions))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var transfer = new slskd.Transfers.Transfer
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Direction = TransferDirection.Upload,
            Filename = @"Music\track.flac",
            Size = 4,
            State = TransferStates.Queued | TransferStates.Locally,
            RequestedAt = DateTime.UtcNow,
        };

        await using (var context = new TransfersDbContext(dbOptions))
        {
            context.Transfers.Add(transfer);
            await context.SaveChangesAsync();
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.flac");
        await System.IO.File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3, 4 });

        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetGroup(It.IsAny<string>())).Returns(Application.DefaultGroup);

        var shareService = new Mock<IShareService>();
        shareService
            .Setup(x => x.ResolveFileAsync(@"Music\track.flac"))
            .ReturnsAsync((Program.LocalHostName, tempFile, 4L));

        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .SetupGet(x => x.Uploads)
            .Returns(Array.Empty<Soulseek.Transfer>());
        soulseekClient
            .Setup(x => x.UploadAsync(
                "alice",
                @"Music\track.flac",
                4L,
                It.IsAny<Func<long, Task<Stream>>>(),
                It.IsAny<int?>(),
                It.IsAny<TransferOptions>(),
                It.IsAny<CancellationToken?>()))
            .ThrowsAsync(new IOException("Failed to connect from Soulseek.Network.Tcp.Connection.ConnectAsync: Connection refused"));

        var service = new UploadService(
            new FileService(optionsMonitor),
            userService.Object,
            soulseekClient.Object,
            optionsMonitor,
            shareService.Object,
            Mock.Of<IRelayService>(),
            new TestDbContextFactory(dbOptions),
            new EventBus(new EventService(Mock.Of<IDbContextFactory<EventsDbContext>>())));

        try
        {
            var exception = await Assert.ThrowsAsync<IOException>(() => service.UploadAsync(transfer));

            Assert.Contains("Connection refused", exception.Message, StringComparison.Ordinal);
            var failed = service.Find(t => t.Id == transfer.Id);
            Assert.NotNull(failed);
            Assert.True(failed.State.HasFlag(TransferStates.Completed));
            Assert.True(failed.State.HasFlag(TransferStates.Errored));
            Assert.Contains("Connection refused", failed.Exception, StringComparison.Ordinal);
        }
        finally
        {
            service.Dispose();
            System.IO.File.Delete(tempFile);
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<TransfersDbContext>
    {
        private readonly DbContextOptions<TransfersDbContext> _options;

        public TestDbContextFactory(DbContextOptions<TransfersDbContext> options)
        {
            _options = options;
        }

        public TransfersDbContext CreateDbContext() => new(_options);
    }
}
