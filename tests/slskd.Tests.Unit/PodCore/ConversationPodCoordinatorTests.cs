// <copyright file="ConversationPodCoordinatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh;
using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class ConversationPodCoordinatorTests : IDisposable
{
    private readonly Mock<ILogger<ConversationPodCoordinator>> _loggerMock;
    private readonly Mock<IOptionsMonitor<MeshOptions>> _meshOptionsMock;
    private readonly Mock<IPodService> _podServiceMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly ConversationPodCoordinator _coordinator;

    public ConversationPodCoordinatorTests()
    {
        _loggerMock = new Mock<ILogger<ConversationPodCoordinator>>();
        _meshOptionsMock = new Mock<IOptionsMonitor<MeshOptions>>();
        _podServiceMock = new Mock<IPodService>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();

        _meshOptionsMock.Setup(x => x.CurrentValue).Returns(new MeshOptions
        {
            SelfPeerId = "peer:mesh:self"
        });

        _coordinator = new ConversationPodCoordinator(
            _loggerMock.Object,
            _meshOptionsMock.Object,
            _podServiceMock.Object,
            _scopeFactoryMock.Object);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_WithValidUsername_CreatesPod()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });
        var expectedChannelId = "dm";

        _podServiceMock.Setup(x => x.PodExistsAsync(expectedPodId)).ReturnsAsync(false);
        _podServiceMock.Setup(x => x.CreatePodAsync(It.IsAny<Pod>())).ReturnsAsync(expectedPodId);

        // Act
        var result = await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.Equal((expectedPodId, expectedChannelId), result);

        _podServiceMock.Verify(x => x.PodExistsAsync(expectedPodId), Times.Once);
        _podServiceMock.Verify(x => x.CreatePodAsync(It.Is<Pod>(p =>
            p.PodId == expectedPodId &&
            p.Name == username &&
            p.Visibility == Visibility.Private &&
            p.Tags != null && p.Tags.Contains("dm") &&
            p.Channels != null && p.Channels.Any(c => c.ChannelId == "dm")
        )), Times.Once);
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_WithExistingPod_ReturnsExisting()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });
        var expectedChannelId = "dm";

        _podServiceMock.Setup(x => x.PodExistsAsync(expectedPodId)).ReturnsAsync(true);

        // Act
        var result = await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.Equal((expectedPodId, expectedChannelId), result);

        _podServiceMock.Verify(x => x.PodExistsAsync(expectedPodId), Times.Once);
        _podServiceMock.Verify(x => x.CreatePodAsync(It.IsAny<Pod>()), Times.Never);
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_WithEmptyUsername_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _coordinator.EnsureDirectMessagePodAsync(""));
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_WithNullUsername_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _coordinator.EnsureDirectMessagePodAsync(null!));
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_CreatesCorrectPodStructure()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });

        Pod? createdPod = null;
        _podServiceMock.Setup(x => x.PodExistsAsync(expectedPodId)).ReturnsAsync(false);
        _podServiceMock.Setup(x => x.CreatePodAsync(It.IsAny<Pod>()))
            .Callback<Pod>(p => createdPod = p)
            .ReturnsAsync(expectedPodId);

        // Act
        await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.NotNull(createdPod);
        Assert.Equal(expectedPodId, createdPod!.PodId);
        Assert.Equal(username, createdPod.Name);
        Assert.Equal(Visibility.Private, createdPod.Visibility);
        Assert.Contains("dm", createdPod.Tags!);

        // Check channels
        Assert.NotNull(createdPod.Channels);
        var dmChannel = createdPod.Channels!.FirstOrDefault(c => c.ChannelId == "dm");
        Assert.NotNull(dmChannel);
        Assert.Equal("DM", dmChannel!.Name);
        Assert.Equal(ChannelKind.DirectMessage, dmChannel.Kind);
        Assert.Equal($"soulseek-dm:{username}", dmChannel.BindingInfo);
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_CreatesCorrectMembers()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });

        Pod? createdPod = null;
        _podServiceMock.Setup(x => x.PodExistsAsync(expectedPodId)).ReturnsAsync(false);
        _podServiceMock.Setup(x => x.CreatePodAsync(It.IsAny<Pod>()))
            .Callback<Pod>(p => createdPod = p)
            .ReturnsAsync(expectedPodId);

        // Act
        await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.NotNull(createdPod);
        Assert.NotNull(createdPod!.Members);

        var selfMember = createdPod.Members!.FirstOrDefault(m => m.PeerId == "peer:mesh:self");
        var otherMember = createdPod.Members!.FirstOrDefault(m => m.PeerId == "bridge:testuser");

        Assert.NotNull(selfMember);
        Assert.NotNull(otherMember);

        Assert.Equal(PodRole.Owner, selfMember!.Role);
        Assert.Equal(PodRole.Member, otherMember!.Role);

        Assert.NotNull(selfMember.JoinedAt);
        Assert.NotNull(otherMember.JoinedAt);
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_HandlesPodCreationFailure()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });

        _podServiceMock.Setup(x => x.PodExistsAsync(expectedPodId)).ReturnsAsync(false);
        _podServiceMock.Setup(x => x.CreatePodAsync(It.IsAny<Pod>()))
            .ThrowsAsync(new InvalidOperationException("Creation failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _coordinator.EnsureDirectMessagePodAsync(username));
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_IsIdempotent()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });

        _podServiceMock.Setup(x => x.PodExistsAsync(expectedPodId)).ReturnsAsync(false);
        _podServiceMock.Setup(x => x.CreatePodAsync(It.IsAny<Pod>())).ReturnsAsync(expectedPodId);

        // Act - Call multiple times
        var result1 = await _coordinator.EnsureDirectMessagePodAsync(username);
        var result2 = await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.Equal(result1, result2);
        _podServiceMock.Verify(x => x.CreatePodAsync(It.IsAny<Pod>()), Times.Once);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Act - Should not throw
        _coordinator.Dispose();

        // Assert - No exceptions thrown
    }
}
