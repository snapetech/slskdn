// <copyright file="MembershipGateTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using slskd.PodCore;
using Xunit;

/// <summary>
///     Tests for PodService.JoinAsync (membership gate behavior).
///     Uses in-memory PodService(IPodPublisher=null, IPodMembershipSigner=null, IContentLinkService=null).
///     JoinAsync returns bool; assert via GetMembersAsync. No PodServices or IPodRepository.
/// </summary>
public class MembershipGateTests
{
    private readonly IPodService _podService = new PodService(podPublisher: null, membershipSigner: null, contentLinkService: null);

    /// <summary>Valid PodId format: ^pod:[a-f0-9]{32}$</summary>
    private static string PodId(int n) => $"pod:{n.ToString("x").PadLeft(32, '0')}";

    /// <summary>Creates a VPN pod. GatewayPeerId must match ^[a-zA-Z0-9\-_.@]{1,255}$ (no colons).</summary>
    private static Pod CreateVpnPod(string podId, string gatewayPeerId, int maxMembers) => new Pod
    {
        PodId = podId,
        Name = "VPN Pod",
        Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
        Channels = new List<PodChannel>(),
        PrivateServicePolicy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = gatewayPeerId,
            MaxMembers = maxMembers,
            AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "x.local", Port = 80 } },
            RegisteredServices = new List<RegisteredService>()
        }
    };

    [Fact]
    public async Task JoinAsync_ValidMemberForRegularPod_Succeeds()
    {
        var podId = PodId(1);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Test Pod" });

        var member = new PodMember { PeerId = "peer-123", Role = PodRoles.Member, JoinedAt = DateTimeOffset.UtcNow };

        var joined = await _podService.JoinAsync(podId, member);

        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        Assert.Contains(members, m => m.PeerId == "peer-123");
    }

    [Fact]
    public async Task JoinAsync_PodNotFound_ReturnsFalse()
    {
        var podId = PodId(0); // never created
        var member = new PodMember { PeerId = "peer-123" };

        var joined = await _podService.JoinAsync(podId, member);

        Assert.False(joined);
    }

    [Fact]
    public async Task JoinAsync_MemberAlreadyExists_ReturnsFalse()
    {
        var podId = PodId(2);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Test Pod" });
        await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-123" });

        var joined = await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-123" });

        Assert.False(joined);
    }

    [Fact]
    public async Task JoinAsync_VpnPodAtCapacity_ReturnsFalse()
    {
        var podId = PodId(20);
        await _podService.CreateAsync(CreateVpnPod(podId, "peer-gateway", maxMembers: 2));
        Assert.True(await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-gateway", Role = PodRoles.Owner }));
        Assert.True(await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-member2", Role = PodRoles.Member }));

        var joined = await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-member3", Role = PodRoles.Member });

        Assert.False(joined);
        var members = await _podService.GetMembersAsync(podId);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task JoinAsync_VpnPodWithAvailableCapacity_Succeeds()
    {
        var podId = PodId(21);
        await _podService.CreateAsync(CreateVpnPod(podId, "peer-gateway", maxMembers: 2));
        Assert.True(await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-gateway", Role = PodRoles.Owner }));

        var joined = await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-member2", Role = PodRoles.Member });

        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task JoinAsync_EmptyPeerId_InMemoryAccepts()
    {
        var podId = PodId(7);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Test Pod" });
        var member = new PodMember { PeerId = "" };

        var joined = await _podService.JoinAsync(podId, member);

        // In-memory PodService does not call PodValidation.ValidateMember; it accepts.
        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        Assert.Contains(members, m => m.PeerId == "");
    }

    [Fact]
    public async Task JoinAsync_NullMember_Throws()
    {
        var podId = PodId(14);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Test Pod" });

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _podService.JoinAsync(podId, null!));

        Assert.Equal("member", ex.ParamName);
    }

    [Fact]
    public async Task JoinAsync_EmptyPodId_ReturnsFalse()
    {
        var member = new PodMember { PeerId = "peer-123" };

        var joined = await _podService.JoinAsync("", member);

        Assert.False(joined);
    }

    [Fact]
    public async Task JoinAsync_MemberRole_Preserved()
    {
        var podId = PodId(9);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Test Pod" });
        var member = new PodMember { PeerId = "peer-123", Role = PodRoles.Owner };

        var joined = await _podService.JoinAsync(podId, member);

        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        var m = members.First(x => x.PeerId == "peer-123");
        Assert.Equal(PodRoles.Owner, m.Role);
    }

    [Fact]
    public async Task JoinAsync_GatewayPeer_JoinSucceeds()
    {
        var podId = PodId(22);
        await _podService.CreateAsync(CreateVpnPod(podId, "peer-gateway", maxMembers: 3));

        var joined = await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-gateway", Role = PodRoles.Owner });

        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        Assert.Single(members);
        Assert.Equal("peer-gateway", members[0].PeerId);
    }

    [Fact]
    public async Task JoinAsync_LargePodWithoutVpn_Succeeds()
    {
        var podId = PodId(11);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Large Pod" });
        for (var i = 1; i <= 50; i++)
            await _podService.JoinAsync(podId, new PodMember { PeerId = $"member-{i}", JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-i) });

        var joined = await _podService.JoinAsync(podId, new PodMember { PeerId = "peer-123" });

        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        Assert.Equal(51, members.Count);
        Assert.Contains(members, m => m.PeerId == "peer-123");
    }

    [Fact]
    public async Task JoinAsync_MemberWithExistingJoinTime_PreservesTimestamp()
    {
        var podId = PodId(12);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Test Pod" });
        var joinTime = DateTimeOffset.UtcNow.AddHours(-1);
        var member = new PodMember { PeerId = "peer-123", JoinedAt = joinTime };

        var joined = await _podService.JoinAsync(podId, member);

        Assert.True(joined);
        var members = await _podService.GetMembersAsync(podId);
        var m = members.First(x => x.PeerId == "peer-123");
        Assert.Equal(joinTime, m.JoinedAt);
    }

    [Fact]
    public async Task JoinAsync_ConcurrentJoins_Succeeds()
    {
        var podId = PodId(13);
        await _podService.CreateAsync(new Pod { PodId = podId, Name = "Concurrent Pod" });

        var joinTasks = Enumerable.Range(1, 5).Select(async i =>
        {
            var member = new PodMember { PeerId = $"peer-{i}" };
            return await _podService.JoinAsync(podId, member);
        });
        var results = await Task.WhenAll(joinTasks);

        Assert.All(results, r => Assert.True(r));
        var members = await _podService.GetMembersAsync(podId);
        Assert.Equal(5, members.Count);
        Assert.All(Enumerable.Range(1, 5), i => Assert.Contains(members, m => m.PeerId == $"peer-{i}"));
    }
}
