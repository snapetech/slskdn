// <copyright file="MembershipGateTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.PodCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class MembershipGateTests
{
    private readonly Mock<ILogger<PodServices>> _loggerMock;
    private readonly Mock<IPodRepository> _podRepositoryMock;
    private readonly PodServices _podServices;

    public MembershipGateTests()
    {
        _loggerMock = new Mock<ILogger<PodServices>>();
        _podRepositoryMock = new Mock<IPodRepository>();
        _podServices = new PodServices(_loggerMock.Object, _podRepositoryMock.Object);
    }

    [Fact]
    public async Task JoinAsync_ValidMemberForRegularPod_Succeeds()
    {
        // Arrange
        var podId = "test-pod";
        var member = new PodMember
        {
            PeerId = "peer-123",
            Role = PodMemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow
        };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability>(), // No VPN gateway
            Members = new List<PodMember>()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        var result = await _podServices.JoinAsync(podId, member);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Members, m => m.PeerId == member.PeerId);
        _podRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_PodNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var podId = "nonexistent-pod";
        var member = new PodMember { PeerId = "peer-123" };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pod?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _podServices.JoinAsync(podId, member));
    }

    [Fact]
    public async Task JoinAsync_MemberAlreadyExists_ThrowsArgumentException()
    {
        // Arrange
        var podId = "test-pod";
        var existingMemberId = "peer-123";
        var member = new PodMember { PeerId = existingMemberId };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Members = new List<PodMember>
            {
                new PodMember { PeerId = existingMemberId }
            }
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _podServices.JoinAsync(podId, member));

        Assert.Contains("already a member", exception.Message);
    }

    [Fact]
    public async Task JoinAsync_VpnPodAtCapacity_ThrowsArgumentException()
    {
        // Arrange
        var podId = "vpn-pod";
        var member = new PodMember { PeerId = "peer-123" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 2,
                GatewayPeerId = "gateway-peer"
            },
            Members = new List<PodMember>
            {
                new PodMember { PeerId = "member-1" },
                new PodMember { PeerId = "member-2" }
            }
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _podServices.JoinAsync(podId, member));

        Assert.Contains("maximum members", exception.Message.ToLowerInvariant());
        Assert.Contains("2", exception.Message);
    }

    [Fact]
    public async Task JoinAsync_VpnPodWithAvailableCapacity_Succeeds()
    {
        // Arrange
        var podId = "vpn-pod";
        var member = new PodMember { PeerId = "peer-123" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3,
                GatewayPeerId = "gateway-peer"
            },
            Members = new List<PodMember>
            {
                new PodMember { PeerId = "member-1" },
                new PodMember { PeerId = "member-2" }
            }
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        var result = await _podServices.JoinAsync(podId, member);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Members.Count);
        Assert.Contains(result.Members, m => m.PeerId == member.PeerId);
    }

    [Fact]
    public async Task JoinAsync_VpnPodWithoutPolicy_ThrowsArgumentException()
    {
        // Arrange
        var podId = "vpn-pod";
        var member = new PodMember { PeerId = "peer-123" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = null // Missing policy
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _podServices.JoinAsync(podId, member));

        Assert.Contains("PrivateServicePolicy", exception.Message);
    }

    [Fact]
    public async Task JoinAsync_VpnPodWithDisabledPolicy_ThrowsArgumentException()
    {
        // Arrange
        var podId = "vpn-pod";
        var member = new PodMember { PeerId = "peer-123" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = false, // Disabled
                MaxMembers = 3,
                GatewayPeerId = "gateway-peer"
            }
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _podServices.JoinAsync(podId, member));

        Assert.Contains("VPN gateway", exception.Message.ToLowerInvariant());
        Assert.Contains("disabled", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task JoinAsync_InvalidMemberData_ThrowsArgumentException()
    {
        // Arrange
        var podId = "test-pod";
        var member = new PodMember { PeerId = "" }; // Invalid - empty peer ID

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Members = new List<PodMember>()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _podServices.JoinAsync(podId, member));

        Assert.Contains("PeerId", exception.Message);
    }

    [Fact]
    public async Task JoinAsync_NullMember_ThrowsArgumentNullException()
    {
        // Arrange
        var podId = "test-pod";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _podServices.JoinAsync(podId, null!));
    }

    [Fact]
    public async Task JoinAsync_EmptyPodId_ThrowsArgumentException()
    {
        // Arrange
        var member = new PodMember { PeerId = "peer-123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _podServices.JoinAsync("", member));

        Assert.Contains("podId", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task JoinAsync_RepositoryUpdateFails_PropagatesException()
    {
        // Arrange
        var podId = "test-pod";
        var member = new PodMember { PeerId = "peer-123" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Members = new List<PodMember>()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _podServices.JoinAsync(podId, member));
    }

    [Fact]
    public async Task JoinAsync_MemberRoleAssignment_DefaultsToMember()
    {
        // Arrange
        var podId = "test-pod";
        var member = new PodMember
        {
            PeerId = "peer-123",
            Role = PodMemberRole.Guest // Should be overridden
        };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Members = new List<PodMember>()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        var result = await _podServices.JoinAsync(podId, member);

        // Assert
        var addedMember = result.Members.First(m => m.PeerId == member.PeerId);
        Assert.Equal(PodMemberRole.Member, addedMember.Role);
        Assert.True(addedMember.JoinedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task JoinAsync_GatewayPeerAutoJoin_Succeeds()
    {
        // Arrange
        var podId = "vpn-pod";
        var gatewayMember = new PodMember { PeerId = "gateway-peer" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3,
                GatewayPeerId = "gateway-peer" // Same as joining member
            },
            Members = new List<PodMember>() // Empty initially
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        var result = await _podServices.JoinAsync(podId, gatewayMember);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Members);
        var addedMember = result.Members.First();
        Assert.Equal("gateway-peer", addedMember.PeerId);
        Assert.Equal(PodMemberRole.Admin, addedMember.Role); // Gateway peer should be admin
    }

    [Fact]
    public async Task JoinAsync_LargePodWithoutVpn_Succeeds()
    {
        // Arrange
        var podId = "large-pod";
        var member = new PodMember { PeerId = "peer-123" };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Large Pod",
            Capabilities = new List<PodCapability>(), // No VPN
            Members = Enumerable.Range(1, 50).Select(i => new PodMember
            {
                PeerId = $"member-{i}",
                JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            }).ToList()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        var result = await _podServices.JoinAsync(podId, member);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(51, result.Members.Count); // 50 existing + 1 new
        Assert.Contains(result.Members, m => m.PeerId == member.PeerId);
    }

    [Fact]
    public async Task JoinAsync_MemberWithExistingJoinTime_PreservesTimestamp()
    {
        // Arrange
        var podId = "test-pod";
        var joinTime = DateTimeOffset.UtcNow.AddHours(-1);
        var member = new PodMember
        {
            PeerId = "peer-123",
            JoinedAt = joinTime
        };

        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Members = new List<PodMember>()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        var result = await _podServices.JoinAsync(podId, member);

        // Assert
        var addedMember = result.Members.First(m => m.PeerId == member.PeerId);
        Assert.Equal(joinTime, addedMember.JoinedAt);
    }

    [Fact]
    public async Task JoinAsync_ConcurrentJoins_Succeeds()
    {
        // Arrange
        var podId = "concurrent-pod";
        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Concurrent Pod",
            Members = new List<PodMember>()
        };

        _podRepositoryMock.Setup(x => x.GetAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        _podRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pod pod, CancellationToken ct) => pod);

        // Act - Simulate concurrent joins
        var joinTasks = Enumerable.Range(1, 5).Select(async i =>
        {
            var member = new PodMember { PeerId = $"peer-{i}" };
            return await _podServices.JoinAsync(podId, member);
        });

        var results = await Task.WhenAll(joinTasks);

        // Assert
        Assert.All(results, result => Assert.NotNull(result));
        var finalResult = results.Last();
        Assert.Equal(5, finalResult.Members.Count);
        Assert.All(Enumerable.Range(1, 5), i =>
            Assert.Contains(finalResult.Members, m => m.PeerId == $"peer-{i}"));
    }
}


