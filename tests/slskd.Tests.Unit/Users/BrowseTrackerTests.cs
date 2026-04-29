// <copyright file="BrowseTrackerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
#nullable enable

namespace slskd.Tests.Unit.Users;

using System.Runtime.Serialization;
using slskd.Users;
using Soulseek;
using Xunit;

public class BrowseTrackerTests
{
    [Fact]
    public void TryRemove_WithMatchingProgress_RemovesEntry()
    {
        var tracker = new BrowseTracker();
        var progress = CreateProgress();

        tracker.AddOrUpdate("alice", progress);

        var removed = tracker.TryRemove("alice", progress);

        Assert.True(removed);
        Assert.False(tracker.TryGet("alice", out _));
    }

    [Fact]
    public void TryRemove_WithStaleProgress_PreservesNewerEntry()
    {
        var tracker = new BrowseTracker();
        var staleProgress = CreateProgress();
        var currentProgress = CreateProgress();
        tracker.AddOrUpdate("alice", staleProgress);
        tracker.AddOrUpdate("alice", currentProgress);

        var removed = tracker.TryRemove("alice", staleProgress);

        Assert.False(removed);
        Assert.True(tracker.TryGet("alice", out var storedProgress));
        Assert.Same(currentProgress, storedProgress);
    }

    private static BrowseProgressUpdatedEventArgs CreateProgress()
        => (BrowseProgressUpdatedEventArgs)FormatterServices.GetUninitializedObject(typeof(BrowseProgressUpdatedEventArgs));
}
