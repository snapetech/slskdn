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
        var publishStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Mock<IPodPublisher>();
        publisher
            .Setup(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .Returns<Pod, CancellationToken>((_, ct) =>
            {
                publishStarted.TrySetResult(true);
                return Task.CompletedTask;
            });

        var service = new PodService(publisher.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pod = await service.CreateAsync(new Pod
        {
            PodId = "pod:00000000000000000000000000000001",
            Name = "Listed pod",
            Visibility = PodVisibility.Listed,
        }, cts.Token);

        await publishStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("pod:00000000000000000000000000000001", pod.PodId);
        publisher.Verify(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateChannelAsync_StillStartsBackgroundPublish_WhenCallerTokenIsAlreadyCancelled()
    {
        var publishStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Mock<IPodPublisher>();
        publisher
            .Setup(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .Returns<Pod, CancellationToken>((_, ct) =>
            {
                publishStarted.TrySetResult(true);
                return Task.CompletedTask;
            });

        var service = new PodService(publisher.Object);
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

        await publishStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(updated);
        publisher.Verify(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
