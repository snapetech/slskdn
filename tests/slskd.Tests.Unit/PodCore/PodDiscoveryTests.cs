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
}
