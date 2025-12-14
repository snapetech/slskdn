// <copyright file="PodIdFactoryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodIdFactoryTests
{
    [Fact]
    public void ConversationPodId_WithTwoPeers_ReturnsDeterministicId()
    {
        // Arrange
        var peerIds = new[] { "peer:mesh:self", "bridge:user1" };

        // Act
        var podId1 = PodIdFactory.ConversationPodId(peerIds);
        var podId2 = PodIdFactory.ConversationPodId(peerIds);

        // Assert
        Assert.Equal(podId1, podId2);
        Assert.StartsWith("pod:", podId1);
        Assert.True(podId1.Length > 4); // Should have content after "pod:"
    }

    [Fact]
    public void ConversationPodId_WithDifferentOrder_ReturnsSameId()
    {
        // Arrange
        var peerIds1 = new[] { "peer:mesh:self", "bridge:user1" };
        var peerIds2 = new[] { "bridge:user1", "peer:mesh:self" };

        // Act
        var podId1 = PodIdFactory.ConversationPodId(peerIds1);
        var podId2 = PodIdFactory.ConversationPodId(peerIds2);

        // Assert
        Assert.Equal(podId1, podId2);
    }

    [Fact]
    public void ConversationPodId_WithDifferentPeers_ReturnsDifferentId()
    {
        // Arrange
        var peerIds1 = new[] { "peer:mesh:self", "bridge:user1" };
        var peerIds2 = new[] { "peer:mesh:self", "bridge:user2" };

        // Act
        var podId1 = PodIdFactory.ConversationPodId(peerIds1);
        var podId2 = PodIdFactory.ConversationPodId(peerIds2);

        // Assert
        Assert.NotEqual(podId1, podId2);
    }

    [Fact]
    public void ConversationPodId_WithEmptyArray_ThrowsArgumentException()
    {
        // Arrange
        var peerIds = Array.Empty<string>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PodIdFactory.ConversationPodId(peerIds));
    }

    [Fact]
    public void ConversationPodId_WithSinglePeer_ThrowsArgumentException()
    {
        // Arrange
        var peerIds = new[] { "peer:mesh:self" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PodIdFactory.ConversationPodId(peerIds));
    }

    [Fact]
    public void ConversationPodId_WithNullArray_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PodIdFactory.ConversationPodId(null!));
    }

    [Fact]
    public void ConversationPodId_WithNullPeerId_ThrowsArgumentException()
    {
        // Arrange
        var peerIds = new[] { "peer:mesh:self", null! };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PodIdFactory.ConversationPodId(peerIds));
    }

    [Fact]
    public void ConversationPodId_WithEmptyPeerId_ThrowsArgumentException()
    {
        // Arrange
        var peerIds = new[] { "peer:mesh:self", "" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PodIdFactory.ConversationPodId(peerIds));
    }

    [Fact]
    public void ConversationPodId_ReturnsValidPodIdFormat()
    {
        // Arrange
        var peerIds = new[] { "peer:mesh:self", "bridge:user1" };

        // Act
        var podId = PodIdFactory.ConversationPodId(peerIds);

        // Assert
        Assert.Matches(@"^pod:[a-f0-9]{32}$", podId); // Should be "pod:" followed by 32 hex chars
    }
}

