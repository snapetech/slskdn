// <copyright file="ConversationPodCoordinatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh;
using slskd.Messaging;
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

        _podServiceMock.Setup(x => x.GetPodAsync(expectedPodId, default)).ReturnsAsync((Pod?)null);
        _podServiceMock.Setup(x => x.CreateAsync(It.IsAny<Pod>(), default)).Returns<Pod, CancellationToken>((p, _) => Task.FromResult(p));
        _podServiceMock.Setup(x => x.JoinAsync(It.IsAny<string>(), It.IsAny<PodMember>(), default)).ReturnsAsync(true);

        // Act
        var result = await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.Equal((expectedPodId, expectedChannelId), result);

        _podServiceMock.Verify(x => x.GetPodAsync(expectedPodId, default), Times.Once);
        _podServiceMock.Verify(x => x.CreateAsync(It.Is<Pod>(p =>
            p.PodId == expectedPodId &&
            p.Name == username &&
            p.Visibility == PodVisibility.Private &&
            p.Tags != null && p.Tags.Contains("dm") &&
            p.Channels != null && p.Channels.Any(c => c.ChannelId == "dm")
        ), default), Times.Once);
        _podServiceMock.Verify(x => x.JoinAsync(It.IsAny<string>(), It.IsAny<PodMember>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_WithExistingPod_ReturnsExisting()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });
        var expectedChannelId = "dm";

        _podServiceMock.Setup(x => x.GetPodAsync(expectedPodId, default)).ReturnsAsync(new Pod { PodId = expectedPodId });

        // Act
        var result = await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.Equal((expectedPodId, expectedChannelId), result);

        _podServiceMock.Verify(x => x.GetPodAsync(expectedPodId, default), Times.Once);
        _podServiceMock.Verify(x => x.CreateAsync(It.IsAny<Pod>(), default), Times.Never);
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
        _podServiceMock.Setup(x => x.GetPodAsync(expectedPodId, default)).ReturnsAsync((Pod?)null);
        _podServiceMock.Setup(x => x.CreateAsync(It.IsAny<Pod>(), default))
            .Callback<Pod, CancellationToken>((p, _) => createdPod = p)
            .Returns<Pod, CancellationToken>((p, _) => Task.FromResult(p));
        _podServiceMock.Setup(x => x.JoinAsync(It.IsAny<string>(), It.IsAny<PodMember>(), default)).ReturnsAsync(true);

        // Act
        await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.NotNull(createdPod);
        Assert.Equal(expectedPodId, createdPod!.PodId);
        Assert.Equal(username, createdPod.Name);
        Assert.Equal(PodVisibility.Private, createdPod.Visibility);
        Assert.Contains("dm", createdPod.Tags!);

        // Check channels
        Assert.NotNull(createdPod.Channels);
        var dmChannel = createdPod.Channels!.FirstOrDefault(c => c.ChannelId == "dm");
        Assert.NotNull(dmChannel);
        Assert.Equal("DM", dmChannel!.Name);
        Assert.Equal(PodChannelKind.DirectMessage, dmChannel.Kind);
        Assert.Equal($"soulseek-dm:{username}", dmChannel.BindingInfo);
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_CreatesCorrectMembers()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });

        _podServiceMock.Setup(x => x.GetPodAsync(expectedPodId, default)).ReturnsAsync((Pod?)null);
        _podServiceMock.Setup(x => x.CreateAsync(It.IsAny<Pod>(), default)).Returns<Pod, CancellationToken>((p, _) => Task.FromResult(p));
        _podServiceMock.Setup(x => x.JoinAsync(It.IsAny<string>(), It.IsAny<PodMember>(), default)).ReturnsAsync(true);

        // Act
        await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert: coordinator adds self as Owner and remote as Member via JoinAsync
        _podServiceMock.Verify(x => x.JoinAsync(expectedPodId, It.Is<PodMember>(m =>
            m.PeerId == "peer:mesh:self" && m.Role == PodRoles.Owner && m.JoinedAt != null), default), Times.Once);
        _podServiceMock.Verify(x => x.JoinAsync(expectedPodId, It.Is<PodMember>(m =>
            m.PeerId == "bridge:testuser" && m.Role == PodRoles.Member && m.JoinedAt != null), default), Times.Once);
    }

    [Fact]
    public async Task EnsureDirectMessagePodAsync_HandlesPodCreationFailure()
    {
        // Arrange
        var username = "testuser";
        var expectedPodId = PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" });

        _podServiceMock.Setup(x => x.GetPodAsync(expectedPodId, default)).ReturnsAsync((Pod?)null);
        _podServiceMock.Setup(x => x.CreateAsync(It.IsAny<Pod>(), default))
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

        _podServiceMock.SetupSequence(x => x.GetPodAsync(expectedPodId, default))
            .ReturnsAsync((Pod?)null)
            .ReturnsAsync(new Pod { PodId = expectedPodId });
        _podServiceMock.Setup(x => x.CreateAsync(It.IsAny<Pod>(), default)).Returns<Pod, CancellationToken>((p, _) => Task.FromResult(p));
        _podServiceMock.Setup(x => x.JoinAsync(It.IsAny<string>(), It.IsAny<PodMember>(), default)).ReturnsAsync(true);

        // Act - Call multiple times
        var result1 = await _coordinator.EnsureDirectMessagePodAsync(username);
        var result2 = await _coordinator.EnsureDirectMessagePodAsync(username);

        // Assert
        Assert.Equal(result1, result2);
        _podServiceMock.Verify(x => x.CreateAsync(It.IsAny<Pod>(), default), Times.Once);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Act - Should not throw
        _coordinator.Dispose();

        // Assert - No exceptions thrown
    }
}
