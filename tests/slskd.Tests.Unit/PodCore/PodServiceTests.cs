// <copyright file="PodServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Threading;
using System.Threading.Tasks;
using Moq;
using slskd.PodCore;
using Xunit;

public class PodServiceTests
{
    [Fact]
    public async Task CreateAsync_StillStartsBackgroundPublish_WhenCallerTokenIsAlreadyCancelled()
    {
        var publisher = new Mock<IPodPublisher>();
        publisher
            .Setup(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Synchronous runner: background work runs inline, no thread-scheduling dependency.
        var service = new PodService(publisher.Object, backgroundRunner: work => work());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pod = await service.CreateAsync(new Pod
        {
            PodId = "pod:00000000000000000000000000000001",
            Name = "Listed pod",
            Visibility = PodVisibility.Listed,
        }, cts.Token);

        Assert.Equal("pod:00000000000000000000000000000001", pod.PodId);
        publisher.Verify(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateChannelAsync_StillStartsBackgroundPublish_WhenCallerTokenIsAlreadyCancelled()
    {
        var publisher = new Mock<IPodPublisher>();
        publisher
            .Setup(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Synchronous runner: background work runs inline, no thread-scheduling dependency.
        var service = new PodService(publisher.Object, backgroundRunner: work => work());
        await service.CreateAsync(new Pod
        {
            PodId = "pod:00000000000000000000000000000002",
            Name = "Listed pod",
            Visibility = PodVisibility.Private,
            Channels = new()
            {
                new PodChannel { ChannelId = "general", Name = "general", Kind = PodChannelKind.General },
                new PodChannel { ChannelId = "updates", Name = "updates", Kind = PodChannelKind.General },
            },
        });

        var existingPod = await service.GetPodAsync("pod:00000000000000000000000000000002");
        Assert.NotNull(existingPod);
        existingPod!.Visibility = PodVisibility.Listed;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var updated = await service.UpdateChannelAsync(
            "pod:00000000000000000000000000000002",
            new PodChannel { ChannelId = "updates", Name = "release-updates", Kind = PodChannelKind.General },
            cts.Token);

        Assert.True(updated);
        publisher.Verify(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
