// <copyright file="SqlitePodMessageStorageTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.PodCore;

using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.PodCore;
using Xunit;

public sealed class SqlitePodMessageStorageTests : IDisposable
{
    private const string ValidPodId = "pod:00000000000000000000000000000001";
    private const string ValidChannelId = "chan:00000000000000000000000000000001";
    private readonly string databasePath;
    private readonly DbContextOptions<PodDbContext> contextOptions;
    private readonly PodDbContext dbContext;
    private readonly SqlitePodMessageStorage storage;

    public SqlitePodMessageStorageTests()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sqlite");

        contextOptions = new DbContextOptionsBuilder<PodDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        dbContext = new PodDbContext(contextOptions);
        storage = new SqlitePodMessageStorage(dbContext, NullLogger<SqlitePodMessageStorage>.Instance);
    }

    public void Dispose()
    {
        storage.Dispose();
        dbContext.Dispose();
        File.Delete(databasePath);
    }

    [Fact]
    public async Task SearchMessagesAsync_InitializesSchemaOnFirstUse()
    {
        var messages = await storage.SearchMessagesAsync(ValidPodId, "hello");

        Assert.Empty(messages);
        Assert.True(GetInitialized());
        Assert.True(await TableExistsAsync("Messages"));
        Assert.True(await TableExistsAsync("Messages_fts"));
    }

    [Fact]
    public async Task SearchMessagesAsync_RecoversFromExistingFtsOnlyArtifact()
    {
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();

            await using var command = new SqliteCommand(@"
                CREATE VIRTUAL TABLE IF NOT EXISTS Messages_fts USING fts5(
                    PodId,
                    ChannelId,
                    TimestampUnixMs UNINDEXED,
                    SenderPeerId,
                    Body
                );", connection);

            await command.ExecuteNonQueryAsync();
        }

        var messages = await storage.SearchMessagesAsync(ValidPodId, "hello");

        Assert.Empty(messages);
        Assert.True(GetInitialized());
        Assert.True(await TableExistsAsync("Messages"));
    }

    [Fact]
    public async Task RebuildSearchIndexAsync_ClearsAndBackfillsSearchRows()
    {
        dbContext.Database.EnsureCreated();

        dbContext.Messages.Add(new PodMessageEntity
        {
            PodId = ValidPodId,
            ChannelId = ValidChannelId,
            TimestampUnixMs = 123,
            SenderPeerId = "peer:1",
            Body = "hello world",
            Signature = "sig",
        });

        await dbContext.SaveChangesAsync();

        Assert.True(await storage.RebuildSearchIndexAsync());
        Assert.True(await storage.RebuildSearchIndexAsync());

        var messages = await storage.SearchMessagesAsync(ValidPodId, "hello");

        var message = Assert.Single(messages);
        Assert.Equal("hello world", message.Body);
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = new SqliteCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE name = @name;",
            connection);
        command.Parameters.AddWithValue("@name", tableName);

        var result = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return result > 0;
    }

    private bool GetInitialized()
    {
        var field = typeof(SqlitePodMessageStorage).GetField("initialized", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("initialized field was not found.");

        return (bool)field.GetValue(storage)!;
    }
}
