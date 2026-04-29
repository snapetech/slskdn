// <copyright file="PodDiscoveryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.PodCore;

using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh.Dht;
using slskd.PodCore;
using Xunit;

public class PodDiscoveryTests
{
    [Fact]
    public async Task DiscoverPodsAsync_DeduplicatesDuplicatePodIdsFromIndex()
    {
        var store = new ConcurrentDictionary<string, object?>();
        store["pod:index:listed"] = new PodIndex
        {
            PodIds = new List<string> { "pod-1", "pod-1", "pod-2" },
        };
        store["pod:metadata:pod-1"] = new PodMetadata
        {
            PodId = "pod-1",
            Name = "First",
            PublishedAt = 10,
        };
        store["pod:metadata:pod-2"] = new PodMetadata
        {
            PodId = "pod-2",
            Name = "Second",
            PublishedAt = 20,
        };

        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PodIndex>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store[key] as PodIndex));
        dht.Setup(x => x.GetAsync<PodMetadata>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store[key] as PodMetadata));

        var service = new PodDiscovery(dht.Object, NullLogger<PodDiscovery>.Instance);

        var pods = await service.DiscoverPodsAsync();

        Assert.Equal(2, pods.Count);
        Assert.Equal(new[] { "pod-2", "pod-1" }, pods.Select(x => x.PodId).ToArray());
    }

    [Fact]
    public async Task DiscoverPodsAsync_IgnoresBlankPodIdsAndTrimsFilters()
    {
        var store = new ConcurrentDictionary<string, object?>();
        store["pod:index:listed"] = new PodIndex
        {
            PodIds = new List<string> { " ", " pod-1 ", "pod-2" },
        };
        store["pod:metadata:pod-1"] = new PodMetadata
        {
            PodId = "pod-1",
            Name = "Alpha Pod",
            FocusContentId = "content:audio:track:1",
            Tags = new List<string> { "rock" },
            PublishedAt = 10,
        };
        store["pod:metadata:pod-2"] = new PodMetadata
        {
            PodId = "pod-2",
            Name = "Beta Pod",
            FocusContentId = "content:audio:track:2",
            Tags = new List<string> { "jazz" },
            PublishedAt = 20,
        };

        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PodIndex>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store[key] as PodIndex));
        dht.Setup(x => x.GetAsync<PodMetadata>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store.TryGetValue(key, out var value) ? value as PodMetadata : null));

        var service = new PodDiscovery(dht.Object, NullLogger<PodDiscovery>.Instance);

        var pods = await service.DiscoverPodsAsync(
            searchQuery: "  alpha  ",
            tags: new List<string> { " ", " rock " },
            focusContentId: " content:audio:track:1 ",
            limit: 50);

        var pod = Assert.Single(pods);
        Assert.Equal("pod-1", pod.PodId);
    }

    [Fact]
    public async Task DiscoverPodsAsync_NormalizesReturnedMetadataBeforeFilteringAndDeduping()
    {
        var store = new ConcurrentDictionary<string, object?>();
        store["pod:index:listed"] = new PodIndex
        {
            PodIds = new List<string> { " pod-1 ", "pod-2" },
        };
        store["pod:metadata:pod-1"] = new PodMetadata
        {
            PodId = " pod-1 ",
            Name = " Alpha Pod ",
            FocusContentId = " content:audio:track:1 ",
            Tags = new List<string> { " rock ", "rock", " " },
            PublishedAt = 10,
        };
        store["pod:metadata:pod-2"] = new PodMetadata
        {
            PodId = "pod-2",
            Name = "Beta Pod",
            FocusContentId = "content:audio:track:2",
            Tags = new List<string> { "jazz" },
            PublishedAt = 20,
        };

        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PodIndex>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store[key] as PodIndex));
        dht.Setup(x => x.GetAsync<PodMetadata>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store.TryGetValue(key, out var value) ? value as PodMetadata : null));

        var service = new PodDiscovery(dht.Object, NullLogger<PodDiscovery>.Instance);

        var pods = await service.DiscoverPodsAsync(
            searchQuery: " alpha ",
            tags: new List<string> { " rock " },
            focusContentId: " content:audio:track:1 ",
            limit: 10);

        var pod = Assert.Single(pods);
        Assert.Equal("pod-1", pod.PodId);
        Assert.Equal("Alpha Pod", pod.Name);
        Assert.Equal("content:audio:track:1", pod.FocusContentId);
        Assert.Single(pod.Tags);
        Assert.Equal("rock", pod.Tags[0]);
    }

    [Fact]
    public async Task DiscoverPodsAsync_WithNonPositiveLimit_ReturnsEmpty()
    {
        var dht = new Mock<IMeshDhtClient>();
        var service = new PodDiscovery(dht.Object, NullLogger<PodDiscovery>.Instance);

        var pods = await service.DiscoverPodsAsync(limit: 0);

        Assert.Empty(pods);
        dht.Verify(x => x.GetAsync<PodIndex>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DiscoverPodAsync_FallsBackToIndexedPodIdMatch()
    {
        var store = new ConcurrentDictionary<string, object?>();
        store["pod:index:listed"] = new PodIndex
        {
            PodIds = new List<string> { " Pod-1 " },
        };
        store["pod:metadata:Pod-1"] = new PodMetadata
        {
            PodId = " Pod-1 ",
            Name = " Alpha Pod ",
            PublishedAt = 10,
        };

        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(x => x.GetAsync<PodMetadata>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) => Task.FromResult(store.TryGetValue(key, out var value) ? value as PodMetadata : null));
        dht.Setup(x => x.GetAsync<PodIndex>("pod:index:listed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(store["pod:index:listed"] as PodIndex);

        var service = new PodDiscovery(dht.Object, NullLogger<PodDiscovery>.Instance);

        var pod = await service.DiscoverPodAsync(" pod-1 ");

        Assert.NotNull(pod);
        Assert.Equal("Pod-1", pod!.PodId);
        Assert.Equal("Alpha Pod", pod.Name);
    }
}
