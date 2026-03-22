// <copyright file="PodDiscoveryServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh.Dht;
using slskd.PodCore;
using Xunit;

public class PodDiscoveryServiceTests
{
    [Fact]
    public async Task UnregisterPodAsync_RemovesOnlyTargetPodFromSharedDiscoveryKeys()
    {
        var pods = new Dictionary<string, Pod>(StringComparer.Ordinal)
        {
            ["pod-1"] = CreateListedPod("pod-1", "Alpha", "rock"),
            ["pod-2"] = CreateListedPod("pod-2", "Beta", "rock"),
        };

        var dhtStore = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var service = CreateService(pods, dhtStore);

        await service.RegisterPodAsync(pods["pod-1"]);
        await service.RegisterPodAsync(pods["pod-2"]);

        var result = await service.UnregisterPodAsync("pod-1");
        var discovery = await service.DiscoverPodsByTagAsync("rock");

        Assert.True(result.Success);
        Assert.Single(discovery.Pods);
        Assert.Equal("pod-2", discovery.Pods[0].PodId);
        Assert.Equal(new[] { "pod-2" }, dhtStore["pod:discover:tag:rock"]);
        Assert.Equal(new[] { "pod-2" }, dhtStore["pod:discover:all"]);
    }

    [Fact]
    public async Task UpdatePodAsync_RewritesIndexesWithoutWipingOtherPods()
    {
        var pods = new Dictionary<string, Pod>(StringComparer.Ordinal)
        {
            ["pod-1"] = CreateListedPod("pod-1", "Alpha", "rock"),
            ["pod-2"] = CreateListedPod("pod-2", "Beta", "rock"),
        };

        var dhtStore = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var service = CreateService(pods, dhtStore);

        await service.RegisterPodAsync(pods["pod-1"]);
        await service.RegisterPodAsync(pods["pod-2"]);

        pods["pod-1"] = CreateListedPod("pod-1", "Alpha", "jazz");

        var updateResult = await service.UpdatePodAsync(pods["pod-1"]);
        var rockDiscovery = await service.DiscoverPodsByTagAsync("rock");
        var jazzDiscovery = await service.DiscoverPodsByTagAsync("jazz");
        var allDiscovery = await service.DiscoverAllPodsAsync();

        Assert.True(updateResult.Success);
        Assert.Single(rockDiscovery.Pods);
        Assert.Equal("pod-2", rockDiscovery.Pods[0].PodId);
        Assert.Single(jazzDiscovery.Pods);
        Assert.Equal("pod-1", jazzDiscovery.Pods[0].PodId);
        Assert.Equal(2, allDiscovery.Pods.Count);
        Assert.Contains("pod-1", dhtStore["pod:discover:all"]);
        Assert.Contains("pod-2", dhtStore["pod:discover:all"]);
    }

    private static PodDiscoveryService CreateService(
        Dictionary<string, Pod> pods,
        Dictionary<string, List<string>> dhtStore)
    {
        var dhtClient = new Mock<IMeshDhtClient>();
        dhtClient
            .Setup(x => x.GetAsync<List<string>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) =>
                Task.FromResult<List<string>?>(dhtStore.TryGetValue(key, out var podIds) ? podIds.ToList() : null));
        dhtClient
            .Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, int, CancellationToken>((key, value, _, _) =>
            {
                var podIds = Assert.IsType<List<string>>(value);
                dhtStore[key] = podIds.ToList();
            })
            .Returns(Task.CompletedTask);

        var podPublisher = new Mock<IPodDhtPublisher>();
        podPublisher
            .Setup(x => x.GetPublishedMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((podId, _) =>
            {
                pods.TryGetValue(podId, out var pod);
                return Task.FromResult(new PodMetadataResult(
                    Found: pod != null,
                    PodId: podId,
                    PublishedPod: pod,
                    RetrievedAt: DateTimeOffset.UtcNow,
                    ExpiresAt: DateTimeOffset.UtcNow.AddHours(24),
                    IsValidSignature: true));
            });

        var podService = new Mock<IPodService>();
        podService
            .Setup(x => x.GetPodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((podId, _) =>
                Task.FromResult(pods.TryGetValue(podId, out var pod) ? pod : null));

        return new PodDiscoveryService(
            NullLogger<PodDiscoveryService>.Instance,
            dhtClient.Object,
            podPublisher.Object,
            podService.Object);
    }

    private static Pod CreateListedPod(string podId, string name, string tag)
    {
        return new Pod
        {
            PodId = podId,
            Name = name,
            Visibility = PodVisibility.Listed,
            Tags = new List<string> { tag },
        };
    }
}
