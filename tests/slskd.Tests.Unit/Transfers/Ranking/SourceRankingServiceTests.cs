// <copyright file="SourceRankingServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.Ranking;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Events;
using slskd.Transfers.Ranking;
using Xunit;

public sealed class SourceRankingServiceTests
{
    [Fact]
    public async Task RecordFailureAsync_ConcurrentNewUsername_RecordsEveryFailure()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SourceRankingDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (var context = new SourceRankingDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var service = new SourceRankingService(
            new TestDbContextFactory(options),
            NullLogger<SourceRankingService>.Instance,
            new EventBus(new EventService(Mock.Of<IDbContextFactory<EventsDbContext>>())));

        try
        {
            var tasks = Enumerable.Range(0, 40)
                .Select(_ => service.RecordFailureAsync("same-user"));

            await Task.WhenAll(tasks);

            var history = await service.GetHistoryAsync("same-user");

            Assert.Equal(0, history.Successes);
            Assert.Equal(40, history.Failures);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task RecordSuccessAndFailureAsync_ExistingUsername_UpdatesCounters()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SourceRankingDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (var context = new SourceRankingDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var service = new SourceRankingService(
            new TestDbContextFactory(options),
            NullLogger<SourceRankingService>.Instance,
            new EventBus(new EventService(Mock.Of<IDbContextFactory<EventsDbContext>>())));

        try
        {
            await service.RecordSuccessAsync("same-user");
            await service.RecordFailureAsync("same-user");
            await service.RecordFailureAsync("same-user");

            var history = await service.GetHistoryAsync("same-user");

            Assert.Equal(1, history.Successes);
            Assert.Equal(2, history.Failures);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<SourceRankingDbContext>
    {
        private readonly DbContextOptions<SourceRankingDbContext> options;

        public TestDbContextFactory(DbContextOptions<SourceRankingDbContext> options)
        {
            this.options = options;
        }

        public SourceRankingDbContext CreateDbContext()
        {
            return new SourceRankingDbContext(options);
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IDbContextFactory transfers DbContext disposal ownership to the caller.")]
        public ValueTask<SourceRankingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CreateDbContext());
        }
    }
}
