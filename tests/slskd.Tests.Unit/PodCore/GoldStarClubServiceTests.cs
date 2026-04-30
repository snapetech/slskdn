// <copyright file="GoldStarClubServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
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
[Collection("GoldStarClubEnv")]
public class GoldStarClubServiceTests
{
    // HARDENING-2026-04-20 H6: auto-join is default-on but can be disabled via env var. Tests that exercise the disabled path
    // must set it; we wrap in IDisposable to guarantee cleanup even on assertion failure, and
    // serialize via an xUnit [Collection] since env vars are process-global.
    private const string AutoJoinEnvVar = "SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN";

    private sealed class EnvScope : IDisposable
    {
        private readonly string? previous;

        public EnvScope(string value)
        {
            previous = Environment.GetEnvironmentVariable(AutoJoinEnvVar);
            Environment.SetEnvironmentVariable(AutoJoinEnvVar, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(AutoJoinEnvVar, previous);
    }

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

    [CollectionDefinition("GoldStarClubEnv", DisableParallelization = true)]
    public sealed class EnvCollection
    {
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
                p.Tags.Contains("first-250") &&
                p.Tags.Contains("realm-governance") &&
                p.Tags.Contains("testing")),
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

        var members = Enumerable.Range(1, 100)
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

        var members = Enumerable.Range(1, 250)
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

        var members = Enumerable.Range(1, 251)
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
        using var _ = new EnvScope("true");

        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);

        var members = Enumerable.Range(1, 100)
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
        using var _ = new EnvScope("true");

        // Arrange
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);

        var members = Enumerable.Range(1, 250)
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
        using var _ = new EnvScope("true");

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
        using var _ = new EnvScope("true");

        // Arrange: GetMembers returns 249 (under limit), then Join succeeds.
        // GetMembershipCountAsync after join returns 250 (we just filled the last slot).
        var pod = new Pod { PodId = GoldStarClubService.GoldStarClubPodId };
        mockPodService.Setup(s => s.GetPodAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);

        var members249 = Enumerable.Range(1, 249)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();
        var members250 = Enumerable.Range(1, 250)
            .Select(i => new PodMember { PeerId = $"user{i}" })
            .ToList();

        // TryAutoJoin: GetMembers (already-member check + currentCount) -> IsAcceptingMembers -> GetMembershipCount -> GetMembers
        // then if accepting: GetMembers (we use same), Join, GetMembershipCount -> GetMembers
        mockPodService.SetupSequence(s => s.GetMembersAsync(GoldStarClubService.GoldStarClubPodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members249)   // 1) already-member check: 249, under limit
            .ReturnsAsync(members249)   // 2) IsAcceptingMembers/GetMembershipCount: 249
            .ReturnsAsync(members250); // 3) after Join, GetMembershipCount: 250 (we are the 250th)

        mockPodService.Setup(s => s.JoinAsync(
            GoldStarClubService.GoldStarClubPodId,
            It.IsAny<PodMember>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var joined = await goldStarClubService.TryAutoJoinAsync("new-user");

        // Assert: Join succeeds; we were under limit and JoinAsync returned true.
        Assert.True(joined);
    }

    [Fact]
    public async Task TryAutoJoinAsync_ShouldReturnFalseWhenAutoJoinDisabled()
    {
        using var _ = new EnvScope("false");

        var joined = await goldStarClubService.TryAutoJoinAsync("new-user");

        Assert.False(joined);
        mockPodService.Verify(
            s => s.JoinAsync(It.IsAny<string>(), It.IsAny<PodMember>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockPodService.Verify(
            s => s.GetMembersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
