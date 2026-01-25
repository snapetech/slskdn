// <copyright file="PodsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh;
using slskd.Messaging;
using slskd.PodCore;
using System.Security.Claims;
using Xunit;
using slskd.API.Native;

namespace slskd.Tests.Unit.PodCore;

public class PodsControllerTests
{
    private readonly Mock<ILogger<PodsController>> _loggerMock;
    private readonly Mock<IPodService> _podServiceMock;
    private readonly Mock<IPodMessaging> _podMessagingMock;
    private readonly Mock<ISoulseekChatBridge> _chatBridgeMock;
    private readonly Mock<IConversationService> _conversationServiceMock;
    private readonly PodsController _controller;

    public PodsControllerTests()
    {
        _loggerMock = new Mock<ILogger<PodsController>>();
        _podServiceMock = new Mock<IPodService>();
        _podMessagingMock = new Mock<IPodMessaging>();
        _chatBridgeMock = new Mock<ISoulseekChatBridge>();
        _conversationServiceMock = new Mock<IConversationService>();
        var meshOptions = Mock.Of<IOptionsMonitor<MeshOptions>>(x => x.CurrentValue == new MeshOptions { SelfPeerId = "peer-mesh-self" });

        _controller = new PodsController(
            _podServiceMock.Object,
            _podMessagingMock.Object,
            _chatBridgeMock.Object,
            _loggerMock.Object,
            _conversationServiceMock.Object,
            meshOptions);

        // Set up controller context
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }))
            }
        };
    }

    [Fact]
    public async Task ListPods_ReturnsOkResult()
    {
        // Arrange
        var pods = new List<Pod>
        {
            new Pod { PodId = "pod:00000000000000000000000000000001", Name = "Test Pod 1" },
            new Pod { PodId = "pod:00000000000000000000000000000002", Name = "Test Pod 2" }
        };

        _podServiceMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pods);

        // Act
        var result = await _controller.ListPods();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedPods = Assert.IsAssignableFrom<IReadOnlyList<Pod>>(okResult.Value);
        Assert.Equal(2, returnedPods.Count);
    }

    [Fact]
    public async Task GetPod_WithValidPodId_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:test123";
        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Description = "A test pod"
        };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);

        // Act
        var result = await _controller.GetPod(podId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedPod = Assert.IsType<Pod>(okResult.Value);
        Assert.Equal(podId, returnedPod.PodId);
    }

    [Fact]
    public async Task GetPod_WithInvalidPodId_ReturnsNotFound()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000000";
        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync((Pod?)null);

        // Act
        var result = await _controller.GetPod(podId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreatePod_WithValidPod_ReturnsCreatedResult()
    {
        // Arrange
        var podId = "pod:test123";
        var pod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Visibility = PodVisibility.Private
        };
        var request = new CreatePodRequest(pod, "peer:creator");

        _podServiceMock.Setup(x => x.CreateAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.JoinAsync(podId, It.IsAny<PodMember>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _controller.CreatePod(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal("GetPod", createdResult.ActionName);
        Assert.Equal(podId, createdResult.RouteValues!["podId"]);
    }

    [Fact]
    public async Task CreatePod_WithNullPod_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.CreatePod(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeletePod_WithValidPodId_ReturnsNoContent()
    {
        var podId = "pod:00000000000000000000000000000001";
        _podServiceMock.Setup(x => x.DeletePodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.DeletePod(podId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeletePod_WithInvalidPodId_ReturnsNotFound()
    {
        var podId = "pod:00000000000000000000000000000000";
        _podServiceMock.Setup(x => x.DeletePodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.DeletePod(podId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task GetMessages_WithValidPodAndChannel_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000001";
        var channelId = "general";
        var messages = new List<PodMessage>
        {
            new PodMessage { MessageId = Guid.NewGuid().ToString("N"), PodId = podId, ChannelId = channelId, Body = "Hello", SenderPeerId = "peer:1", TimestampUnixMs = 1 },
            new PodMessage { MessageId = Guid.NewGuid().ToString("N"), PodId = podId, ChannelId = channelId, Body = "World", SenderPeerId = "peer:2", TimestampUnixMs = 2 }
        };

        _podMessagingMock.Setup(x => x.GetMessagesAsync(podId, channelId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessages(podId, channelId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedMessages = Assert.IsAssignableFrom<IReadOnlyList<PodMessage>>(okResult.Value);
        Assert.Equal(2, returnedMessages.Count);
    }

    [Fact]
    public async Task GetMessages_WithSoulseekDmBinding_ReturnsConversationMessages()
    {
        var podId = "pod:00000000000000000000000000000001";
        var channelId = "dm";
        var pod = new Pod
        {
            PodId = podId,
            Name = "DM",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = channelId, Name = "DM", BindingInfo = "soulseek-dm:remoteuser" } }
        };
        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);

        var conv = new Conversation { Username = "remoteuser", Messages = new[]
        {
            new PrivateMessage { Id = 1, Username = "remoteuser", Direction = MessageDirection.In, Message = "Hi from Soulseek", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        }};
        _conversationServiceMock.Setup(x => x.FindAsync("remoteuser", true, true)).ReturnsAsync(conv);

        var result = await _controller.GetMessages(podId, channelId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<PodMessage>>(ok.Value);
        Assert.NotEmpty(list);
        Assert.Contains(list, m => m.Body == "Hi from Soulseek" && m.SenderPeerId == "bridge:remoteuser");
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000001";
        var channelId = "general";
        var request = new SendMessageRequest("Test message", "peer:mesh:self");

        _podMessagingMock.Setup(x => x.SendAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _controller.SendMessage(podId, channelId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SendMessage_WithSoulseekDmBinding_SendsConversationMessage()
    {
        var podId = "pod:00000000000000000000000000000001";
        var channelId = "dm";
        var pod = new Pod
        {
            PodId = podId,
            Name = "DM",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = channelId, Name = "DM", BindingInfo = "soulseek-dm:remoteuser" } }
        };
        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _conversationServiceMock.Setup(x => x.SendMessageAsync("remoteuser", "Hello via DM")).Returns(Task.CompletedTask);

        var result = await _controller.SendMessage(podId, channelId, new SendMessageRequest("Hello via DM", "peer-mesh-self"));

        Assert.IsType<OkObjectResult>(result);
        _conversationServiceMock.Verify(x => x.SendMessageAsync("remoteuser", "Hello via DM"), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithNullMessage_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.SendMessage("pod:00000000000000000000000000000001", "general", null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task JoinPod_WithValidPodId_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000001";
        var request = new JoinPodRequest("peer:joiner");
        _podServiceMock.Setup(x => x.JoinAsync(podId, It.IsAny<PodMember>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _controller.JoinPod(podId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task JoinPod_WithInvalidPodId_ReturnsBadRequest()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000000";
        var request = new JoinPodRequest("peer:joiner");
        _podServiceMock.Setup(x => x.JoinAsync(podId, It.IsAny<PodMember>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _controller.JoinPod(podId, request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task LeavePod_WithValidPodId_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000001";
        var request = new LeavePodRequest("peer:leaver");
        _podServiceMock.Setup(x => x.LeaveAsync(podId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _controller.LeavePod(podId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task LeavePod_WithInvalidPodId_ReturnsNotFound()
    {
        // Arrange
        var podId = "pod:00000000000000000000000000000000";
        var request = new LeavePodRequest("peer:leaver");
        _podServiceMock.Setup(x => x.LeaveAsync(podId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _controller.LeavePod(podId, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePod_ValidRequest_ReturnsOk()
    {
        // Arrange
        var podId = "pod:test";
        var pod = new Pod { PodId = podId, Name = "Test Pod" };
        var request = new UpdatePodRequest(pod, "peer:gateway");

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PodMember>());
        _podServiceMock.Setup(x => x.UpdateAsync(pod, It.IsAny<CancellationToken>())).ReturnsAsync(pod);

        // Act
        var result = await _controller.UpdatePod(podId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(pod, okResult.Value);
    }

    [Fact]
    public async Task UpdatePod_NullRequest_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.UpdatePod("pod:test", null!);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("required", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdatePod_MissingRequestingPeerId_ReturnsBadRequest()
    {
        // Arrange
        var pod = new Pod { PodId = "pod:test", Name = "Test Pod" };
        var request = new UpdatePodRequest(pod, "");

        // Act
        var result = await _controller.UpdatePod("pod:test", request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("RequestingPeerId", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdatePod_PodIdMismatch_ReturnsBadRequest()
    {
        // Arrange
        var pod = new Pod { PodId = "pod:different", Name = "Test Pod" };
        var request = new UpdatePodRequest(pod, "peer:gateway");

        // Act
        var result = await _controller.UpdatePod("pod:test", request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("PodId in URL must match", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdatePod_PodNotFound_ReturnsNotFound()
    {
        // Arrange
        var pod = new Pod { PodId = "pod:test", Name = "Test Pod" };
        var request = new UpdatePodRequest(pod, "peer:gateway");

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test", It.IsAny<CancellationToken>())).ReturnsAsync((Pod?)null);

        // Act
        var result = await _controller.UpdatePod("pod:test", request);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdatePod_EnableVpnAsNonGateway_ReturnsForbidden()
    {
        // Arrange
        var podId = "pod:test";
        var existingPod = new Pod { PodId = podId, Name = "Test Pod" };
        var updatedPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway"
            }
        };
        var request = new UpdatePodRequest(updatedPod, "peer:non-gateway"); // Not the gateway

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(existingPod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PodMember>
        {
            new PodMember { PeerId = "peer:non-gateway", Role = "member" }
        });

        // Act
        var result = await _controller.UpdatePod(podId, request);

        // Assert
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Contains("designated gateway peer", forbidden.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdatePod_EnableVpnAsGateway_ReturnsOk()
    {
        // Arrange
        var podId = "pod:test";
        var existingPod = new Pod { PodId = podId, Name = "Test Pod" };
        var updatedPod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "printer.local", Port = 9100 }
                }
            }
        };
        var request = new UpdatePodRequest(updatedPod, "peer:gateway");

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(existingPod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PodMember>
        {
            new PodMember { PeerId = "peer:gateway", Role = "owner" }
        });
        _podServiceMock.Setup(x => x.UpdateAsync(updatedPod, It.IsAny<CancellationToken>())).ReturnsAsync(updatedPod);

        // Act
        var result = await _controller.UpdatePod(podId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePod_NonMemberTriesUpdate_ReturnsForbidden()
    {
        // Arrange: controller only enforces "must be member" when pod has PrivateServiceGateway
        var podId = "pod:test";
        var existingPod = new Pod
        {
            PodId = podId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy { GatewayPeerId = "peer:gateway" }
        };
        var updatedPod = new Pod
        {
            PodId = podId,
            Name = "Updated Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy { GatewayPeerId = "peer:gateway" }
        };
        var request = new UpdatePodRequest(updatedPod, "peer:outsider");

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(existingPod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PodMember>
        {
            new PodMember { PeerId = "peer:member1", Role = "member" }
        });

        // Act
        var result = await _controller.UpdatePod(podId, request);

        // Assert
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Contains("pod members", forbidden.Value?.ToString() ?? "");
    }
}
