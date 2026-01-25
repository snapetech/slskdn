namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Overlay;
using slskd.PodCore;
using Soulseek;
using Xunit;

/// <summary>
/// Unit tests for PodMessaging: SendAsync validates message, runs membership/signature verification,
/// stores the message, and delegates routing to IPodMessageRouter.
/// </summary>
public class PodMessagingRoutingTests
{
    private readonly Mock<IPodService> mockPodService;
    private readonly Mock<IPodMembershipVerifier> mockMembershipVerifier;
    private readonly Mock<IPodMessageRouter> mockMessageRouter;
    private readonly Mock<IMessageSigner> mockMessageSigner;
    private readonly Mock<ISoulseekChatBridge> mockChatBridge;
    private readonly Mock<ILogger<PodMessaging>> mockLogger;
    private readonly Mock<IMeshSyncService> mockMeshSync;
    private readonly Mock<ISoulseekClient> mockSoulseekClient;
    private readonly Mock<IOverlayClient> mockOverlayClient;
    private readonly PodMessaging podMessaging;

    public PodMessagingRoutingTests()
    {
        mockPodService = new Mock<IPodService>();
        mockChatBridge = new Mock<ISoulseekChatBridge>();
        mockLogger = new Mock<ILogger<PodMessaging>>();
        mockMeshSync = new Mock<IMeshSyncService>();
        mockSoulseekClient = new Mock<ISoulseekClient>();
        mockOverlayClient = new Mock<IOverlayClient>();
        mockMembershipVerifier = new Mock<IPodMembershipVerifier>();
        mockMessageSigner = new Mock<IMessageSigner>();
        mockMessageRouter = new Mock<IPodMessageRouter>();

        mockMembershipVerifier
            .Setup(v => v.VerifyMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageVerificationResult(IsValid: true, IsFromValidMember: true, HasValidSignature: true, IsNotBanned: true));
        mockMessageSigner
            .Setup(s => s.VerifyMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockMessageRouter
            .Setup(r => r.RouteMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessageRoutingResult(Success: true, MessageId: "", PodId: "", TargetPeerCount: 0, SuccessfullyRoutedCount: 0, FailedRoutingCount: 0, TimeSpan.Zero));

        podMessaging = new PodMessaging(
            mockPodService.Object,
            mockMembershipVerifier.Object,
            mockMessageRouter.Object,
            mockMessageSigner.Object,
            mockChatBridge.Object,
            mockLogger.Object,
            mockMeshSync.Object,
            mockSoulseekClient.Object,
            mockOverlayClient.Object);
    }

    [Fact]
    public async Task SendAsync_ShouldRouteMessageToMembers()
    {
        var podId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var senderPeerId = "peer-sender";
        var recipientPeerId = "peer-recipient";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };
        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = recipientPeerId, Role = "member" }
        };
        var message = new PodMessage
        {
            PodId = podId,
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = "general",
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await podMessaging.SendAsync(message);

        Assert.True(result);
        mockMessageRouter.Verify(
            r => r.RouteMessageAsync(It.Is<PodMessage>(m => m.MessageId == message.MessageId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldNotRouteToSender()
    {
        var podId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var senderPeerId = "peer-sender";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };
        var members = new List<PodMember> { new PodMember { PeerId = senderPeerId, Role = "member" } };
        var message = new PodMessage
        {
            PodId = podId,
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = "general",
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await podMessaging.SendAsync(message);

        Assert.True(result);
        mockMessageRouter.Verify(r => r.RouteMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldNotRouteToBannedMembers()
    {
        var podId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var senderPeerId = "peer-sender";
        var bannedPeerId = "peer-banned";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };
        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member", IsBanned = false },
            new PodMember { PeerId = bannedPeerId, Role = "member", IsBanned = true }
        };
        var message = new PodMessage
        {
            PodId = podId,
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = "general",
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await podMessaging.SendAsync(message);

        Assert.True(result);
        mockMessageRouter.Verify(r => r.RouteMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldOnlyRouteToMeshCapablePeers()
    {
        var podId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var senderPeerId = "peer-sender";
        var meshCapablePeerId = "peer-mesh";
        var nonMeshPeerId = "peer-nonmesh";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };
        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = meshCapablePeerId, Role = "member" },
            new PodMember { PeerId = nonMeshPeerId, Role = "member" }
        };
        var message = new PodMessage
        {
            PodId = podId,
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = "general",
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await podMessaging.SendAsync(message);

        Assert.True(result);
        mockMessageRouter.Verify(r => r.RouteMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldHandleRoutingFailuresGracefully()
    {
        var podId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var senderPeerId = "peer-sender";
        var recipientPeerId = "peer-recipient";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };
        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = recipientPeerId, Role = "member" }
        };
        var message = new PodMessage
        {
            PodId = podId,
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = "general",
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);
        mockMessageRouter.Setup(r => r.RouteMessageAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Routing failed"));

        var result = await podMessaging.SendAsync(message);

        Assert.True(result);
    }

    [Fact]
    public async Task SendAsync_ShouldPassMessageThroughToRouter()
    {
        var podId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var senderPeerId = "peer-sender";
        var recipientPeerId = "peer-recipient";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };
        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = recipientPeerId, Role = "member" }
        };
        var message = new PodMessage
        {
            PodId = podId,
            MessageId = "test-message-id",
            ChannelId = "general",
            SenderPeerId = senderPeerId,
            Body = "Test message body",
            TimestampUnixMs = 1234567890L,
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        await podMessaging.SendAsync(message);

        mockMessageRouter.Verify(
            r => r.RouteMessageAsync(
                It.Is<PodMessage>(m => m.MessageId == "test-message-id" && m.Body == "Test message body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
