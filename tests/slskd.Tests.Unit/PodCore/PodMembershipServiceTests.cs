// <copyright file="PodMembershipServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;
using slskd.PodCore;
using Xunit;

public class PodMembershipServiceTests
{
    [Fact]
    public async Task ListPodMembershipsAsync_ReturnsPublishedActiveMemberships()
    {
        var service = CreateService(out _, out _);

        await service.PublishMembershipAsync("pod-1", new PodMember
        {
            PeerId = "peer-a",
            Role = "member",
            PublicKey = "pub-a",
        });

        await service.PublishMembershipAsync("pod-1", new PodMember
        {
            PeerId = "peer-b",
            Role = "mod",
            PublicKey = "pub-b",
            IsBanned = true,
        });

        var memberships = await service.ListPodMembershipsAsync("pod-1");

        Assert.Equal(2, memberships.Count);
        Assert.Equal(new[] { "peer-a", "peer-b" }, memberships.Select(m => m.PeerId).ToArray());
        Assert.Equal("join", memberships.Single(m => m.PeerId == "peer-a").SignedRecord!.Action);
        Assert.Equal("ban", memberships.Single(m => m.PeerId == "peer-b").SignedRecord!.Action);
    }

    [Fact]
    public async Task ListPodMembershipsAsync_OmitsRemovedMemberships()
    {
        var service = CreateService(out _, out _);

        await service.PublishMembershipAsync("pod-1", new PodMember
        {
            PeerId = "peer-a",
            Role = "member",
            PublicKey = "pub-a",
        });

        await service.RemoveMembershipAsync("pod-1", "peer-a");

        var memberships = await service.ListPodMembershipsAsync("pod-1");

        Assert.Empty(memberships);
    }

    [Fact]
    public async Task ChangeRoleAsync_DoesNotDoubleCountReplacementMembership()
    {
        var service = CreateService(out _, out _);

        await service.PublishMembershipAsync("pod-1", new PodMember
        {
            PeerId = "peer-a",
            Role = "member",
            PublicKey = "pub-a",
        });

        await service.ChangeRoleAsync("pod-1", "peer-a", "moderator");

        var stats = await service.GetStatsAsync();

        Assert.Equal(1, stats.TotalMemberships);
        Assert.Equal(1, stats.ActiveMemberships);
        Assert.Equal(0, stats.BannedMemberships);
        Assert.Equal(1, stats.MembershipsByPod["pod-1"]);
        Assert.Equal(1, stats.MembershipsByRole["moderator"]);
        Assert.False(stats.MembershipsByRole.TryGetValue("member", out var oldRoleCount) && oldRoleCount > 0);
    }

    [Fact]
    public async Task BanAndUnbanMemberAsync_OnlyToggleBannedCount()
    {
        var service = CreateService(out _, out _);

        await service.PublishMembershipAsync("pod-1", new PodMember
        {
            PeerId = "peer-a",
            Role = "member",
            PublicKey = "pub-a",
        });

        await service.BanMemberAsync("pod-1", "peer-a", "test");

        var bannedStats = await service.GetStatsAsync();
        Assert.Equal(1, bannedStats.TotalMemberships);
        Assert.Equal(1, bannedStats.ActiveMemberships);
        Assert.Equal(1, bannedStats.BannedMemberships);

        await service.UnbanMemberAsync("pod-1", "peer-a");

        var unbannedStats = await service.GetStatsAsync();
        Assert.Equal(1, unbannedStats.TotalMemberships);
        Assert.Equal(1, unbannedStats.ActiveMemberships);
        Assert.Equal(0, unbannedStats.BannedMemberships);
        Assert.Equal(1, unbannedStats.MembershipsByPod["pod-1"]);
        Assert.Equal(1, unbannedStats.MembershipsByRole["member"]);
    }

    private static PodMembershipService CreateService(
        out Mock<IMeshDhtClient> dhtClient,
        out ConcurrentDictionary<string, object?> store)
    {
        var localStore = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        store = localStore;
        dhtClient = new Mock<IMeshDhtClient>();
        dhtClient
            .Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, object?, int, CancellationToken>((key, value, _, _) =>
            {
                localStore[key] = value;
                return Task.CompletedTask;
            });
        dhtClient
            .Setup(x => x.GetAsync<SignedMembershipRecord>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) =>
            {
                localStore.TryGetValue(key, out var value);
                return Task.FromResult(value as SignedMembershipRecord);
            });

        var signer = new Mock<IControlSigner>();
        var signatureSequence = 0;
        signer
            .Setup(x => x.Sign(It.IsAny<ControlEnvelope>()))
            .Returns<ControlEnvelope>(envelope =>
            {
                signatureSequence++;
                envelope.Signature = $"sig-{signatureSequence}";
                return envelope;
            });
        signer
            .Setup(x => x.Verify(It.IsAny<ControlEnvelope>()))
            .Returns(true);

        return new PodMembershipService(
            NullLogger<PodMembershipService>.Instance,
            dhtClient.Object,
            signer.Object);
    }
}
