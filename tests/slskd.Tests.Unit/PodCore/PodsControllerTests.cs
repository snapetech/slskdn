// <copyright file="PodsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Conversation;
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
    private readonly PodsController _controller;

    public PodsControllerTests()
    {
        _loggerMock = new Mock<ILogger<PodsController>>();
        _podServiceMock = new Mock<IPodService>();
        _podMessagingMock = new Mock<IPodMessaging>();
        _chatBridgeMock = new Mock<ISoulseekChatBridge>();

        _controller = new PodsController(
            _podServiceMock.Object,
            _podMessagingMock.Object,
            _chatBridgeMock.Object,
            _loggerMock.Object);

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
    public async Task GetPods_ReturnsOkResult()
    {
        // Arrange
        var pods = new List<Pod>
        {
            new Pod { PodId = "pod:1", Name = "Test Pod 1" },
            new Pod { PodId = "pod:2", Name = "Test Pod 2" }
        };

        _podServiceMock.Setup(x => x.ListPodsAsync()).ReturnsAsync(pods);

        // Act
        var result = await _controller.GetPods();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedPods = Assert.IsType<List<Pod>>(okResult.Value);
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

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(pod);

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
        var podId = "pod:nonexistent";
        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync((Pod?)null);

        // Act
        var result = await _controller.GetPod(podId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
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

        _podServiceMock.Setup(x => x.CreateAsync(pod)).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.JoinAsync(podId, It.IsAny<PodMember>())).ReturnsAsync(true);

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
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task DeletePod_WithValidPodId_ReturnsNoContent()
    {
        // Arrange
        var podId = "pod:test123";
        _podServiceMock.Setup(x => x.DeletePodAsync(podId)).ReturnsAsync(true);

        // Act
        var result = await _controller.DeletePod(podId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeletePod_WithInvalidPodId_ReturnsNotFound()
    {
        // Arrange
        var podId = "pod:nonexistent";
        _podServiceMock.Setup(x => x.DeletePodAsync(podId)).ReturnsAsync(false);

        // Act
        var result = await _controller.DeletePod(podId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetMessages_WithValidPodAndChannel_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "general";
        var messages = new List<PodMessage>
        {
            new PodMessage { Id = Guid.NewGuid(), Body = "Hello", SenderPeerId = "peer:1" },
            new PodMessage { Id = Guid.NewGuid(), Body = "World", SenderPeerId = "peer:2" }
        };

        _podMessagingMock.Setup(x => x.GetMessagesAsync(podId, channelId, null, null))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessages(podId, channelId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedMessages = Assert.IsType<List<PodMessage>>(okResult.Value);
        Assert.Equal(2, returnedMessages.Count);
    }

    [Fact]
    public async Task GetMessages_WithSoulseekDmBinding_ReturnsConversationMessages()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "dm";
        var username = "testuser";

        var pod = new Pod
        {
            PodId = podId,
            Channels = new List<PodChannel>
            {
                new PodChannel
                {
                    ChannelId = channelId,
                    BindingInfo = $"soulseek-dm:{username}"
                }
            }
        };

        var conversationMessages = new List<ConversationMessage>
        {
            new ConversationMessage { Id = Guid.NewGuid(), Message = "Hello from Soulseek", Username = username },
            new ConversationMessage { Id = Guid.NewGuid(), Message = "Reply", Username = "self" }
        };

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(pod);
        _conversationServiceMock.Setup(x => x.GetMessagesAsync(username))
            .ReturnsAsync(conversationMessages);

        // Act
        var result = await _controller.GetMessages(podId, channelId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedMessages = Assert.IsType<List<PodMessage>>(okResult.Value);
        Assert.Equal(2, returnedMessages.Count);
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_ReturnsCreatedResult()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "general";
        var message = new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            Body = "Test message",
            SenderPeerId = "peer:mesh:self"
        };

        _podMessagingMock.Setup(x => x.SendAsync(message)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SendMessage(podId, channelId, message);

        // Assert
        Assert.IsType<CreatedResult>(result);
    }

    [Fact]
    public async Task SendMessage_WithSoulseekDmBinding_SendsConversationMessage()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "dm";
        var username = "testuser";
        var messageBody = "Hello from pod!";

        var pod = new Pod
        {
            PodId = podId,
            Channels = new List<PodChannel>
            {
                new PodChannel
                {
                    ChannelId = channelId,
                    BindingInfo = $"soulseek-dm:{username}"
                }
            }
        };

        var message = new PodMessage
        {
            Body = messageBody
        };

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(pod);
        _conversationServiceMock.Setup(x => x.SendMessageAsync(username, messageBody))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SendMessage(podId, channelId, message);

        // Assert
        Assert.IsType<CreatedResult>(result);
        _conversationServiceMock.Verify(x => x.SendMessageAsync(username, messageBody), Times.Once);
        _podMessagingMock.Verify(x => x.SendAsync(It.IsAny<PodMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithNullMessage_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.SendMessage("pod:test", "general", null!);

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task JoinPod_WithValidPodId_ReturnsOkResult()
    {
        // Arrange
        var podId = "pod:test123";
        _podServiceMock.Setup(x => x.JoinPodAsync(podId, It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _controller.JoinPod(podId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task JoinPod_WithInvalidPodId_ReturnsNotFound()
    {
        // Arrange
        var podId = "pod:nonexistent";
        _podServiceMock.Setup(x => x.JoinPodAsync(podId, It.IsAny<string>())).ReturnsAsync(false);

        // Act
        var result = await _controller.JoinPod(podId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task LeavePod_WithValidPodId_ReturnsNoContent()
    {
        // Arrange
        var podId = "pod:test123";
        _podServiceMock.Setup(x => x.LeavePodAsync(podId, It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _controller.LeavePod(podId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task LeavePod_WithInvalidPodId_ReturnsNotFound()
    {
        // Arrange
        var podId = "pod:nonexistent";
        _podServiceMock.Setup(x => x.LeavePodAsync(podId, It.IsAny<string>())).ReturnsAsync(false);

        // Act
        var result = await _controller.LeavePod(podId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdatePod_ValidRequest_ReturnsOk()
    {
        // Arrange
        var podId = "pod:test";
        var pod = new Pod { PodId = podId, Name = "Test Pod" };
        var request = new UpdatePodRequest(pod, "peer:gateway");

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId)).ReturnsAsync(new List<PodMember>());
        _podServiceMock.Setup(x => x.UpdateAsync(pod)).ReturnsAsync(pod);

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

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync((Pod?)null);

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

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(existingPod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId)).ReturnsAsync(new List<PodMember>
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

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(existingPod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId)).ReturnsAsync(new List<PodMember>
        {
            new PodMember { PeerId = "peer:gateway", Role = "owner" }
        });
        _podServiceMock.Setup(x => x.UpdateAsync(updatedPod)).ReturnsAsync(updatedPod);

        // Act
        var result = await _controller.UpdatePod(podId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePod_NonMemberTriesUpdate_ReturnsForbidden()
    {
        // Arrange
        var podId = "pod:test";
        var existingPod = new Pod { PodId = podId, Name = "Test Pod" };
        var updatedPod = new Pod { PodId = podId, Name = "Updated Pod" };
        var request = new UpdatePodRequest(updatedPod, "peer:outsider");

        _podServiceMock.Setup(x => x.GetPodAsync(podId)).ReturnsAsync(existingPod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId)).ReturnsAsync(new List<PodMember>
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
