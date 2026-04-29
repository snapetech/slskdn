// <copyright file="PeerDescriptorRefreshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.Dht;
using Xunit;

public class PeerDescriptorRefreshServiceTests
{
    [Fact]
    public async Task StartAsync_DoesNotImmediatelyDuplicateBootstrapPublish()
    {
        var publisher = new CountingPeerDescriptorPublisher();
        var options = Options.Create(new MeshOptions
        {
            EnableDht = true,
            PeerDescriptorRefresh = new PeerDescriptorRefreshOptions
            {
                EnableIpChangeDetection = false,
                RefreshInterval = TimeSpan.FromMinutes(30)
            }
        });
        var service = new PeerDescriptorRefreshService(
            NullLogger<PeerDescriptorRefreshService>.Instance,
            publisher,
            options);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, publisher.PublishCount);
    }

    private sealed class CountingPeerDescriptorPublisher : IPeerDescriptorPublisher
    {
        public int PublishCount { get; private set; }

        public Task PublishSelfAsync(CancellationToken ct = default)
        {
            PublishCount++;
            return Task.CompletedTask;
        }

        public Task MarkPeerRequiresRelayAsync(string peerId, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
