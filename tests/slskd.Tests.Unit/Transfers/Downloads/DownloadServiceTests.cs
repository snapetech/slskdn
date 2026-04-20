namespace slskd.Tests.Unit.Transfers.Downloads;

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Events;
using slskd.Files;
using slskd.Integrations.FTP;
using slskd.Relay;
using slskd.Transfers;
using slskd.Transfers.Downloads;
using Soulseek;
using Xunit;

public class DownloadServiceTests
{
    [Fact]
    public async Task EnqueueAsync_DoesNotRequirePeerPreflightConnection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new TransfersDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .SetupGet(client => client.Downloads)
            .Returns(Array.Empty<Soulseek.Transfer>());
        soulseekClient
            .Setup(client => client.ConnectToUserAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken?>()))
            .ThrowsAsync(new InvalidOperationException("peer preflight should not run"));
        soulseekClient
            .Setup(client => client.GetUserEndPointAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken?>()))
            .ThrowsAsync(new InvalidOperationException("endpoint preflight should not run"));
        soulseekClient
            .Setup(client => client.DownloadAsync(
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
                TransferOptions transferOptions,
                CancellationToken? cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken ?? CancellationToken.None);
                return null!;
            });

        var service = new DownloadService(
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            soulseekClient.Object,
            new TestDbContextFactory(options),
            new FileService(new TestOptionsMonitor<slskd.Options>(new slskd.Options())),
            Mock.Of<IRelayService>(),
            Mock.Of<IFTPService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        try
        {
            var (enqueued, failed) = await service.EnqueueAsync(
                "alice",
                new[] { (Filename: @"Music\track.flac", Size: 1234L) },
                CancellationToken.None);

            Assert.Single(enqueued);
            Assert.Empty(failed);

            Assert.True(service.TryCancel(enqueued.Single().Id));

            soulseekClient.Verify(client => client.ConnectToUserAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken?>()), Times.Never);
            soulseekClient.Verify(client => client.GetUserEndPointAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken?>()), Times.Never);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task EnqueueAsync_CancelledTransfer_DoesNotFailFromDisposedBatchSemaphore()
    {
        var databasePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (var context = new TransfersDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .SetupGet(client => client.Downloads)
            .Returns(Array.Empty<Soulseek.Transfer>());
        soulseekClient
            .Setup(client => client.DownloadAsync(
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
                TransferOptions transferOptions,
                CancellationToken? cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken ?? CancellationToken.None);
                return null!;
            });

        var service = new DownloadService(
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            soulseekClient.Object,
            new TestDbContextFactory(options),
            new FileService(new TestOptionsMonitor<slskd.Options>(new slskd.Options())),
            Mock.Of<IRelayService>(),
            Mock.Of<IFTPService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        try
        {
            var (enqueued, failed) = await service.EnqueueAsync(
                "alice",
                new[] { (Filename: @"Music\track.flac", Size: 1234L) },
                CancellationToken.None);

            Assert.Single(enqueued);
            Assert.Empty(failed);

            var transferId = enqueued.Single().Id;
            Assert.True(service.TryCancel(transferId));

            var cancelledTransfer = await WaitForTransferAsync(
                () => service.Find(t => t.Id == transferId && t.EndedAt != null),
                TimeSpan.FromSeconds(5));

            Assert.True(cancelledTransfer.State.HasFlag(TransferStates.Completed));
            Assert.DoesNotContain("SemaphoreSlim", cancelledTransfer.Exception ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("disposed object", cancelledTransfer.Exception ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            service.Dispose();

            if (System.IO.File.Exists(databasePath))
            {
                System.IO.File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Dispose_WhenApplicationIsShuttingDown_DoesNotMarkActiveDownloadFailed()
    {
        var databasePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (var context = new TransfersDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .SetupGet(client => client.Downloads)
            .Returns(Array.Empty<Soulseek.Transfer>());
        soulseekClient
            .Setup(client => client.DownloadAsync(
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
                TransferOptions transferOptions,
                CancellationToken? cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken ?? CancellationToken.None);
                return null!;
            });

        var service = new DownloadService(
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            soulseekClient.Object,
            new TestDbContextFactory(options),
            new FileService(new TestOptionsMonitor<slskd.Options>(new slskd.Options())),
            Mock.Of<IRelayService>(),
            Mock.Of<IFTPService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        try
        {
            var (enqueued, failed) = await service.EnqueueAsync(
                "alice",
                new[] { (Filename: @"Music\track.flac", Size: 1234L) },
                CancellationToken.None);

            Assert.Single(enqueued);
            Assert.Empty(failed);

            SetApplicationShuttingDown(true);
            service.Dispose();
            await Task.Delay(250);

            await using var context = new TransfersDbContext(options);
            var transfer = await context.Transfers.SingleAsync(t => t.Id == enqueued.Single().Id);
            Assert.Null(transfer.EndedAt);
            Assert.False(transfer.State.HasFlag(TransferStates.Completed));
        }
        finally
        {
            SetApplicationShuttingDown(false);
            service.Dispose();

            if (System.IO.File.Exists(databasePath))
            {
                System.IO.File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task ShutdownAsync_WaitsForCancelledDownloadsToDrain()
    {
        var databasePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (var context = new TransfersDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drainCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .SetupGet(client => client.Downloads)
            .Returns(Array.Empty<Soulseek.Transfer>());
        soulseekClient
            .Setup(client => client.DownloadAsync(
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
                TransferOptions transferOptions,
                CancellationToken? cancellationToken) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken ?? CancellationToken.None);
                    return null!;
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.TrySetResult();
                    await Task.Delay(150);
                    drainCompleted.TrySetResult();
                    throw;
                }
            });

        var service = new DownloadService(
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            soulseekClient.Object,
            new TestDbContextFactory(options),
            new FileService(new TestOptionsMonitor<slskd.Options>(new slskd.Options())),
            Mock.Of<IRelayService>(),
            Mock.Of<IFTPService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        try
        {
            var (enqueued, failed) = await service.EnqueueAsync(
                "alice",
                new[] { (Filename: @"Music\track.flac", Size: 1234L) },
                CancellationToken.None);

            Assert.Single(enqueued);
            Assert.Empty(failed);

            SetApplicationShuttingDown(true);
            await service.ShutdownAsync(CancellationToken.None);

            await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await drainCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            SetApplicationShuttingDown(false);
            service.Dispose();

            if (System.IO.File.Exists(databasePath))
            {
                System.IO.File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public void Dispose_UnsubscribesClockMinuteHandler()
    {
        var optionsMonitor = new TestOptionsMonitor<slskd.Options>(new slskd.Options());
        var clockEveryMinuteListenersBefore = GetStaticEventInvocationCount(typeof(Clock), "EveryMinute");
        var service = new DownloadService(
            optionsMonitor,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<TransfersDbContext>>(),
            new FileService(optionsMonitor),
            Mock.Of<IRelayService>(),
            Mock.Of<IFTPService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        Assert.Equal(clockEveryMinuteListenersBefore + 1, GetStaticEventInvocationCount(typeof(Clock), "EveryMinute"));

        service.Dispose();

        Assert.Equal(clockEveryMinuteListenersBefore, GetStaticEventInvocationCount(typeof(Clock), "EveryMinute"));
    }

    private static async Task<slskd.Transfers.Transfer> WaitForTransferAsync(Func<slskd.Transfers.Transfer?> finder, TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            var transfer = finder();

            if (transfer is not null)
            {
                return transfer;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Timed out waiting {timeout.TotalSeconds} seconds for transfer state update");
    }

    private static int GetStaticEventInvocationCount(Type type, string eventName)
    {
        var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{type.FullName}.{eventName} backing field was not found.");

        return (field.GetValue(null) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }

    private static void SetApplicationShuttingDown(bool value)
    {
        var property = typeof(slskd.Application).GetProperty("ShuttingDown", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Application.ShuttingDown property was not found.");

        property.SetValue(null, value);
    }

    private sealed class TestDbContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<TransfersDbContext>
    {
        private readonly DbContextOptions<TransfersDbContext> _options;

        public TestDbContextFactory(DbContextOptions<TransfersDbContext> options)
        {
            _options = options;
        }

        public TransfersDbContext CreateDbContext() => new(_options);
    }
}
