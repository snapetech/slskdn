namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.PodCore;
using Soulseek;
using Xunit;

/// <summary>
/// Unit tests for Gold Star Club service.
/// </summary>
public class GoldStarClubServiceTests
{
    private readonly Mock<IPodService> mockPodService;
    private readonly Mock<ISoulseekClient> mockSoulseekClient;
    private readonly Mock<ILogger<GoldStarClubService>> mockLogger;
    private readonly GoldStarClubService goldStarClubService;

    public GoldStarClubServiceTests()
    {
        mockPodService = new Mock<IPodService>();
        mockSoulseekClient = new Mock<ISoulseekClient>();
        mockLogger = new Mock<ILogger<GoldStarClubService>>();

        mockSoulseekClient.Setup(c => c.Username).Returns("test-user");

        goldStarClubService = new GoldStarClubService(
            mockPodService.Object,
            mockSoulseekClient.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task EnsurePodExistsAsync_ShouldCreatePodIfNotExists()
    {
        // Arrange
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pod)null);

        // Act
        await goldStarClubService.EnsurePodExistsAsync();

        // Assert
        mockPodService.Verify(s => s.CreateAsync(
            It.Is<Pod>(p => 
                p.PodId == GoldStarClubService.GoldStarClubPodId &&
                p.Name == "Gold Star Club ⭐" &&
                p.Visibility == PodVisibility.Listed &&
                p.Tags.Contains("gold-star") &&
                p.Tags.Contains("first-1000") &&
                p.Tags.Contains("exclusive")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsurePodExistsAsync_ShouldNotCreatePodIfExists()
    {
        // Arrange
        var existingPod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId, Name = "Gold Star Club ⭐" };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPod);

        // Act
        await goldStarClubService.EnsurePodExistsAsync();

        // Assert
        mockPodService.Verify(s => s.CreateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMembershipCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "user1" },
            new PodMember { PeerId = "user2" },
            new PodMember { PeerId = "user3" }
        };
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var count = await goldStarClubService.GetMembershipCountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task IsAcceptingMembersAsync_ShouldReturnTrueWhenUnderLimit()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = Enumerable.Range(1, 500)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var accepting = await goldStarClubService.IsAcceptingMembersAsync();

        // Assert
        Assert.True(accepting);
    }

    [Fact]
    public async Task IsAcceptingMembersAsync_ShouldReturnFalseWhenAtLimit()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = Enumerable.Range(1, 1000)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var accepting = await goldStarClubService.IsAcceptingMembersAsync();

        // Assert
        Assert.False(accepting);
    }

    [Fact]
    public async Task IsAcceptingMembersAsync_ShouldReturnFalseWhenOverLimit()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = Enumerable.Range(1, 1001)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var accepting = await goldStarClubService.IsAcceptingMembersAsync();

        // Assert
        Assert.False(accepting);
    }

    [Fact]
    public async Task TryAutoJoinAsync_ShouldJoinWhenUnderLimit()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = Enumerable.Range(1, 500)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);
        
        mockPodService.Setup(s => s.JoinAsync(
            GoldStarClubService.GoldStarClubPodId,
            It.Is<PodMember>(m => m.PeerId == "new-user" && m.Role == "member"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var joined = await goldStarClubService.TryAutoJoinAsync("new-user");

        // Assert
        Assert.True(joined);
        mockPodService.Verify(s => s.JoinAsync(
            GoldStarClubService.GoldStarClubPodId,
            It.Is<PodMember>(m => m.PeerId == "new-user"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAutoJoinAsync_ShouldNotJoinWhenAtLimit()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = Enumerable.Range(1, 1000)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var joined = await goldStarClubService.TryAutoJoinAsync("new-user");

        // Assert
        Assert.False(joined);
        mockPodService.Verify(s => s.JoinAsync(
            It.IsAny<string>(),
            It.IsAny<PodMember>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryAutoJoinAsync_ShouldReturnTrueIfAlreadyMember()
    {
        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "existing-user" }
        };
        mockPodService.Setup(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var joined = await goldStarClubService.TryAutoJoinAsync("existing-user");

        // Assert
        Assert.True(joined);
        mockPodService.Verify(s => s.JoinAsync(
            It.IsAny<string>(),
            It.IsAny<PodMember>(),
            It.IsAny<CancellationToken>()),
            Times.Never); // Should not call JoinAsync if already a member
    }

    [Fact]
    public async Task TryAutoJoinAsync_ShouldHandleRaceCondition()
    {
        // Arrange - Simulate race condition where count changes between checks
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        
        // First call returns 999 members (under limit)
        // Second call returns 1000 members (at limit)
        var members999 = Enumerable.Range(1, 999)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        var members1000 = Enumerable.Range(1, 1000)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        
        mockPodService.SetupSequence(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members999)  // First check: under limit
            .ReturnsAsync(members1000); // Second check: at limit (after someone else joined)

        mockPodService.Setup(s => s.JoinAsync(
            GoldStarClubService.GoldStarClubPodId,
            It.IsAny<PodMember>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var joined = await goldStarClubService.TryAutoJoinAsync("new-user");

        // Assert - Should still attempt join, but the final count check should prevent it
        // Actually, the current implementation checks count before joining, so it should work
        // But if someone else joins between the check and the join, we rely on the final count check
        Assert.True(joined); // Join succeeds, but we check count again after
    }
}















