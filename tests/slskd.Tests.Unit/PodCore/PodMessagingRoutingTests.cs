namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
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
/// Unit tests for PodMessaging decentralized routing functionality.
/// </summary>
public class PodMessagingRoutingTests
{
    private readonly Mock<IPodService> mockPodService;
    private readonly Mock<IPodMembershipVerifier> mockMembershipVerifier;
    private readonly Mock<IPodMessageRouter> mockMessageRouter;
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
        mockMessageRouter = new Mock<IPodMessageRouter>();

        podMessaging = new PodMessaging(
            mockPodService.Object,
            mockMembershipVerifier.Object,
            mockMessageRouter.Object,
            mockChatBridge.Object,
            mockLogger.Object,
            mockMeshSync.Object,
            mockSoulseekClient.Object,
            mockOverlayClient.Object);
    }

    [Fact]
    public async Task SendAsync_ShouldRouteMessageToMembers()
    {
        // Arrange
        var podId = "pod:test-123";
        var channelId = $"{podId}:general";
        var senderPeerId = "peer-sender";
        var recipientPeerId = "peer-recipient";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "General" }
            }
        };

        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = recipientPeerId, Role = "member" }
        };

        var message = new PodMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Mock mesh peers (GetMeshPeers already filters for mesh-capable peers)
        var meshPeers = new List<MeshPeerInfo>
        {
            new MeshPeerInfo { Username = recipientPeerId }
        };
        mockMeshSync.Setup(m => m.GetMeshPeers())
            .Returns(meshPeers);

        mockSoulseekClient.Setup(c => c.SendPrivateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await podMessaging.SendAsync(message);

        // Assert
        Assert.True(result);
        
        // Verify message was routed to recipient (excluding sender)
        mockSoulseekClient.Verify(
            c => c.SendPrivateMessageAsync(
                recipientPeerId,
                It.Is<string>(msg => msg.StartsWith("PODMSG:")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldNotRouteToSender()
    {
        // Arrange
        var podId = "pod:test-123";
        var channelId = $"{podId}:general";
        var senderPeerId = "peer-sender";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "General" }
            }
        };

        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" }
        };

        var message = new PodMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        mockMeshSync.Setup(m => m.GetMeshPeers())
            .Returns(new List<MeshPeerInfo>());

        // Act
        var result = await podMessaging.SendAsync(message);

        // Assert
        Assert.True(result);
        
        // Verify sender was not routed to
        mockSoulseekClient.Verify(
            c => c.SendPrivateMessageAsync(
                senderPeerId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_ShouldNotRouteToBannedMembers()
    {
        // Arrange
        var podId = "pod:test-123";
        var channelId = $"{podId}:general";
        var senderPeerId = "peer-sender";
        var bannedPeerId = "peer-banned";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "General" }
            }
        };

        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member", IsBanned = false },
            new PodMember { PeerId = bannedPeerId, Role = "member", IsBanned = true }
        };

        var message = new PodMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members.Where(m => !m.IsBanned).ToList());

        mockMeshSync.Setup(m => m.GetMeshPeers())
            .Returns(new List<MeshPeerInfo>());

        // Act
        var result = await podMessaging.SendAsync(message);

        // Assert
        Assert.True(result);
        
        // Verify banned member was not routed to
        mockSoulseekClient.Verify(
            c => c.SendPrivateMessageAsync(
                bannedPeerId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_ShouldOnlyRouteToMeshCapablePeers()
    {
        // Arrange
        var podId = "pod:test-123";
        var channelId = $"{podId}:general";
        var senderPeerId = "peer-sender";
        var meshCapablePeerId = "peer-mesh";
        var nonMeshPeerId = "peer-nonmesh";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "General" }
            }
        };

        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = meshCapablePeerId, Role = "member" },
            new PodMember { PeerId = nonMeshPeerId, Role = "member" }
        };

        var message = new PodMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Only meshCapablePeerId is in mesh peers list (GetMeshPeers already filters for mesh-capable peers)
        var meshPeers = new List<MeshPeerInfo>
        {
            new MeshPeerInfo { Username = meshCapablePeerId }
        };
        mockMeshSync.Setup(m => m.GetMeshPeers())
            .Returns(meshPeers);

        mockSoulseekClient.Setup(c => c.SendPrivateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await podMessaging.SendAsync(message);

        // Assert
        Assert.True(result);
        
        // Verify only mesh-capable peer was routed to
        mockSoulseekClient.Verify(
            c => c.SendPrivateMessageAsync(
                meshCapablePeerId,
                It.Is<string>(msg => msg.StartsWith("PODMSG:")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify non-mesh peer was not routed to
        mockSoulseekClient.Verify(
            c => c.SendPrivateMessageAsync(
                nonMeshPeerId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_ShouldHandleRoutingFailuresGracefully()
    {
        // Arrange
        var podId = "pod:test-123";
        var channelId = $"{podId}:general";
        var senderPeerId = "peer-sender";
        var recipientPeerId = "peer-recipient";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "General" }
            }
        };

        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = recipientPeerId, Role = "member" }
        };

        var message = new PodMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = "Test message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var meshPeers = new List<MeshPeerInfo>
        {
            new MeshPeerInfo { Username = recipientPeerId }
        };
        mockMeshSync.Setup(m => m.GetMeshPeers())
            .Returns(meshPeers);

        // Simulate routing failure
        mockSoulseekClient.Setup(c => c.SendPrivateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Routing failed"));

        // Act
        var result = await podMessaging.SendAsync(message);

        // Assert
        // Message should still be accepted and stored even if routing fails
        Assert.True(result);
    }

    [Fact]
    public async Task SendAsync_ShouldSerializeMessageAsJson()
    {
        // Arrange
        var podId = "pod:test-123";
        var channelId = $"{podId}:general";
        var senderPeerId = "peer-sender";
        var recipientPeerId = "peer-recipient";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "General" }
            }
        };

        var members = new List<PodMember>
        {
            new PodMember { PeerId = senderPeerId, Role = "member" },
            new PodMember { PeerId = recipientPeerId, Role = "member" }
        };

        var message = new PodMessage
        {
            MessageId = "test-message-id",
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = "Test message body",
            TimestampUnixMs = 1234567890L,
            Signature = "test-signature"
        };

        mockPodService.Setup(s => s.GetPodAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pod);
        mockPodService.Setup(s => s.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var meshPeers = new List<MeshPeerInfo>
        {
            new MeshPeerInfo { Username = recipientPeerId }
        };
        mockMeshSync.Setup(m => m.GetMeshPeers())
            .Returns(meshPeers);

        string routedMessage = null;
        mockSoulseekClient.Setup(c => c.SendPrivateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((user, msg, ct) => routedMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await podMessaging.SendAsync(message);

        // Assert
        Assert.NotNull(routedMessage);
        Assert.StartsWith("PODMSG:", routedMessage);
        
        // Verify JSON contains message fields
        Assert.Contains("test-message-id", routedMessage);
        Assert.Contains("Test message body", routedMessage);
    }
}

