// <copyright file="SongIdRunStoreTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#nullable enable

namespace slskd.Tests.Unit.SongID;

using System.Reflection;
using slskd.SongID;
using Xunit;

[Collection("SongIdAppDirectory")]
public sealed class SongIdRunStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalAppDirectory;

    public SongIdRunStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "slskdn-songid-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var property = typeof(Program).GetProperty(nameof(Program.AppDirectory), BindingFlags.Public | BindingFlags.Static);
        _originalAppDirectory = property?.GetValue(null) as string;
        property?.SetValue(null, _tempDir);
    }

    [Fact]
    public void UpsertAndGet_RoundTripsRunPayload()
    {
        var store = new SongIdRunStore();
        var run = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "https://youtube.com/watch?v=test",
            SourceType = "youtube_url",
            Status = "running",
            Summary = "Running SongID analysis.",
            Query = "artist title",
            ArtifactDirectory = Path.Combine(_tempDir, "artifacts"),
            Evidence = new List<string> { "initial evidence" },
        };

        store.Upsert(run);
        var stored = store.Get(run.Id);

        Assert.NotNull(stored);
        Assert.Equal(run.Id, stored!.Id);
        Assert.Equal(run.Source, stored.Source);
        Assert.Equal(run.Status, stored.Status);
        Assert.Equal(run.Query, stored.Query);
        Assert.Single(stored.Evidence);
    }

    [Fact]
    public void List_ReturnsNewestRunsFirst()
    {
        var store = new SongIdRunStore();
        var older = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "older",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var newer = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "newer",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        store.Upsert(older);
        store.Upsert(newer);

        var listed = store.List(2);

        Assert.Equal(2, listed.Count);
        Assert.Equal(newer.Id, listed[0].Id);
        Assert.Equal(older.Id, listed[1].Id);
    }

    [Fact]
    public void ListByStatuses_FiltersAndReturnsOldestQueuedFirst()
    {
        var store = new SongIdRunStore();
        var queuedOlder = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "queued-older",
            Status = "queued",
            QueuePosition = 2,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
        };
        var running = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "running",
            Status = "running",
            WorkerSlot = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
        };
        var queuedNewer = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "queued-newer",
            Status = "queued",
            QueuePosition = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        };
        var completed = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "completed",
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        store.Upsert(queuedOlder);
        store.Upsert(running);
        store.Upsert(queuedNewer);
        store.Upsert(completed);

        var active = store.ListByStatuses(new[] { "queued", "running" }, 10);

        Assert.Equal(3, active.Count);
        Assert.Equal(queuedOlder.Id, active[0].Id);
        Assert.Equal(running.Id, active[1].Id);
        Assert.Equal(queuedNewer.Id, active[2].Id);
        Assert.Equal(2, active[0].QueuePosition);
        Assert.Equal(1, active[1].WorkerSlot);
    }

    public void Dispose()
    {
        var property = typeof(Program).GetProperty(nameof(Program.AppDirectory), BindingFlags.Public | BindingFlags.Static);
        property?.SetValue(null, _originalAppDirectory);

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
