namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using Xunit;

public class PodAffinityScorerTests
{
    private readonly Mock<IPodService> mockPodService;
    private readonly Mock<IPodMessaging> mockPodMessaging;
    private readonly PodAffinityScorer scorer;

    public PodAffinityScorerTests()
    {
        mockPodService = new Mock<IPodService>();
        mockPodMessaging = new Mock<IPodMessaging>();
        scorer = new PodAffinityScorer(
            NullLogger<PodAffinityScorer>.Instance,
            mockPodService.Object,
            mockPodMessaging.Object);
    }

    [Fact]
    public async Task ComputeAffinityAsync_NonExistentPod_ReturnsZero()
    {
        mockPodService.Setup(s => s.GetPodAsync(It.IsAny<string>(), default))
            .ReturnsAsync((Pod)null);

        var affinity = await scorer.ComputeAffinityAsync("pod:nonexistent", "user1");

        Assert.Equal(0.0, affinity);
    }

    [Fact]
    public async Task ComputeAffinityAsync_ActivePodWithTrustedMembers_ReturnsHighScore()
    {
        var pod = CreatePod("pod:active", "Active Pod");
        var members = CreateMembers(10, allVerified: true);
        var messages = CreateRecentMessages(20, hoursAgo: 1);

        mockPodService.Setup(s => s.GetPodAsync("pod:active", default))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:active", default))
            .ReturnsAsync(members);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:active", "general", null, default))
            .ReturnsAsync(messages);

        var affinity = await scorer.ComputeAffinityAsync("pod:active", "user1");

        Assert.True(affinity > 0.7, $"Expected affinity > 0.7, got {affinity}");
    }

    [Fact]
    public async Task ComputeAffinityAsync_InactivePod_ReturnsLowScore()
    {
        var pod = CreatePod("pod:inactive", "Inactive Pod");
        var members = CreateMembers(5, allVerified: false);
        var messages = CreateRecentMessages(0, hoursAgo: 1); // No recent messages

        mockPodService.Setup(s => s.GetPodAsync("pod:inactive", default))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:inactive", default))
            .ReturnsAsync(members);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:inactive", "general", null, default))
            .ReturnsAsync(messages);

        var affinity = await scorer.ComputeAffinityAsync("pod:inactive", "user1");

        Assert.True(affinity < 0.5, $"Expected affinity < 0.5, got {affinity}");
    }

    [Fact]
    public async Task ComputeAffinityAsync_UserIsMember_ReturnsHighTrustScore()
    {
        var pod = CreatePod("pod:member", "User's Pod");
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "user1", Role = "member" },
            new PodMember { PeerId = "user2", Role = "member" }
        };
        var messages = CreateRecentMessages(5, hoursAgo: 1);

        mockPodService.Setup(s => s.GetPodAsync("pod:member", default))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:member", default))
            .ReturnsAsync(members);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:member", "general", null, default))
            .ReturnsAsync(messages);

        var affinity = await scorer.ComputeAffinityAsync("pod:member", "user1");

        // User is a member → trust score = 1.0 → high overall affinity
        Assert.True(affinity > 0.6, $"Expected affinity > 0.6 for member, got {affinity}");
    }

    [Fact]
    public async Task ComputeAffinityAsync_OptimalSize_ReturnsBetterScore()
    {
        var smallPod = CreatePod("pod:small", "Small Pod");
        var optimalPod = CreatePod("pod:optimal", "Optimal Pod");
        var largePod = CreatePod("pod:large", "Large Pod");

        var smallMembers = CreateMembers(2, allVerified: true);
        var optimalMembers = CreateMembers(25, allVerified: true); // Optimal: 5-50
        var largeMembers = CreateMembers(200, allVerified: true);

        var messages = CreateRecentMessages(10, hoursAgo: 1);

        // Small pod
        mockPodService.Setup(s => s.GetPodAsync("pod:small", It.IsAny<CancellationToken>())).ReturnsAsync(smallPod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:small", It.IsAny<CancellationToken>())).ReturnsAsync(smallMembers);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:small", "general", null, It.IsAny<CancellationToken>())).ReturnsAsync(messages);

        // Optimal pod
        mockPodService.Setup(s => s.GetPodAsync("pod:optimal", It.IsAny<CancellationToken>())).ReturnsAsync(optimalPod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:optimal", It.IsAny<CancellationToken>())).ReturnsAsync(optimalMembers);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:optimal", "general", null, It.IsAny<CancellationToken>())).ReturnsAsync(messages);

        // Large pod
        mockPodService.Setup(s => s.GetPodAsync("pod:large", It.IsAny<CancellationToken>())).ReturnsAsync(largePod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:large", It.IsAny<CancellationToken>())).ReturnsAsync(largeMembers);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:large", "general", null, It.IsAny<CancellationToken>())).ReturnsAsync(messages);

        var smallAffinity = await scorer.ComputeAffinityAsync("pod:small", "user1");
        var optimalAffinity = await scorer.ComputeAffinityAsync("pod:optimal", "user1");
        var largeAffinity = await scorer.ComputeAffinityAsync("pod:large", "user1");

        Assert.True(optimalAffinity > smallAffinity, "Optimal size should beat small");
        Assert.True(optimalAffinity > largeAffinity, "Optimal size should beat large");
    }

    [Fact]
    public async Task GetRecommendationsAsync_ReturnsRankedPods()
    {
        var pods = new List<Pod>
        {
            CreatePod("pod:best", "Best Pod"),
            CreatePod("pod:good", "Good Pod"),
            CreatePod("pod:okay", "Okay Pod")
        };

        mockPodService.Setup(s => s.ListAsync(default)).ReturnsAsync(pods);

        // Setup best pod
        mockPodService.Setup(s => s.GetPodAsync("pod:best", default)).ReturnsAsync(pods[0]);
        mockPodService.Setup(s => s.GetMembersAsync("pod:best", default)).ReturnsAsync(CreateMembers(20, true));
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:best", "general", null, default))
            .ReturnsAsync(CreateRecentMessages(20, 1));

        // Setup good pod
        mockPodService.Setup(s => s.GetPodAsync("pod:good", default)).ReturnsAsync(pods[1]);
        mockPodService.Setup(s => s.GetMembersAsync("pod:good", default)).ReturnsAsync(CreateMembers(10, true));
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:good", "general", null, default))
            .ReturnsAsync(CreateRecentMessages(10, 2));

        // Setup okay pod
        mockPodService.Setup(s => s.GetPodAsync("pod:okay", It.IsAny<CancellationToken>())).ReturnsAsync(pods[2]);
        mockPodService.Setup(s => s.GetMembersAsync("pod:okay", It.IsAny<CancellationToken>())).ReturnsAsync(CreateMembers(5, false));
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:okay", "general", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRecentMessages(2, 12));

        var recommendations = await scorer.GetRecommendationsAsync("user1", limit: 10);

        Assert.Equal(3, recommendations.Count);
        Assert.Equal("pod:best", recommendations[0].PodId);
        Assert.True(recommendations[0].AffinityScore > recommendations[1].AffinityScore);
        Assert.True(recommendations[1].AffinityScore > recommendations[2].AffinityScore);
    }

    [Fact]
    public async Task GetRecommendationsAsync_LimitsResults()
    {
        var pods = Enumerable.Range(0, 20)
            .Select(i => CreatePod($"pod:{i}", $"Pod {i}"))
            .ToList();

        mockPodService.Setup(s => s.ListAsync(default)).ReturnsAsync(pods);

        foreach (var pod in pods)
        {
            mockPodService.Setup(s => s.GetPodAsync(pod.PodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
            mockPodService.Setup(s => s.GetMembersAsync(pod.PodId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateMembers(5, true));
            mockPodMessaging.Setup(s => s.GetMessagesAsync(pod.PodId, "general", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecentMessages(3, 1));
        }

        var recommendations = await scorer.GetRecommendationsAsync("user1", limit: 5);

        Assert.Equal(5, recommendations.Count);
    }

    [Fact]
    public async Task ComputeAffinityAsync_WithBannedMembers_ReducesTrustScore()
    {
        var pod = CreatePod("pod:banned", "Pod with Banned Members");
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "user1", Role = "member", PublicKey = "key1" },
            new PodMember { PeerId = "banned1", Role = "member", IsBanned = true },
            new PodMember { PeerId = "banned2", Role = "member", IsBanned = true }
        };
        var messages = CreateRecentMessages(10, 1);

        mockPodService.Setup(s => s.GetPodAsync("pod:banned", default)).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync("pod:banned", default)).ReturnsAsync(members);
        mockPodMessaging.Setup(s => s.GetMessagesAsync("pod:banned", "general", null, default)).ReturnsAsync(messages);

        var affinity = await scorer.ComputeAffinityAsync("pod:banned", "user1");

        // Should have reduced trust score due to banned members
        Assert.True(affinity < 0.6, $"Expected affinity < 0.6 with banned members, got {affinity}");
    }

    // Helper methods
    private static Pod CreatePod(string podId, string name)
    {
        return new Pod
        {
            PodId = podId,
            Name = name,
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } },
            Tags = new List<string>()
        };
    }

    private static List<PodMember> CreateMembers(int count, bool allVerified)
    {
        var members = new List<PodMember>();
        for (int i = 0; i < count; i++)
        {
            members.Add(new PodMember
            {
                PeerId = $"user{i}",
                Role = "member",
                PublicKey = allVerified ? $"publickey{i}" : null
            });
        }
        return members;
    }

    private static List<PodMessage> CreateRecentMessages(int count, int hoursAgo)
    {
        var messages = new List<PodMessage>();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-hoursAgo).ToUnixTimeMilliseconds();
        
        for (int i = 0; i < count; i++)
        {
            messages.Add(new PodMessage
            {
                SenderPeerId = $"user{i % 5}",
                Body = $"Message {i}",
                TimestampUnixMs = baseTime + (i * 1000)
            });
        }
        return messages;
    }
}
