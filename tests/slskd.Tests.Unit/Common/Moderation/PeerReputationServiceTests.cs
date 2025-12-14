// <copyright file="PeerReputationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Moderation;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP04: Peer Reputation Service implementation.
    /// </summary>
    public class PeerReputationServiceTests
    {
        private readonly Mock<ILogger<PeerReputationService>> _loggerMock;
        private readonly Mock<IPeerReputationStore> _storeMock;
        private readonly PeerReputationService _service;

        public PeerReputationServiceTests()
        {
            _loggerMock = new Mock<ILogger<PeerReputationService>>();
            _storeMock = new Mock<IPeerReputationStore>();
            _service = new PeerReputationService(_loggerMock.Object, _storeMock.Object);
        }

        [Fact]
        public async Task RecordBlockedContentAssociationAsync_WithValidParameters_RecordsEvent()
        {
            // Arrange
            var peerId = "test-peer";
            var contentId = "content-123";
            var metadata = "additional info";

            // Act
            await _service.RecordBlockedContentAssociationAsync(peerId, contentId, metadata);

            // Assert
            _storeMock.Verify(s => s.RecordEventAsync(It.Is<PeerReputationEvent>(e =>
                e.PeerId == peerId &&
                e.EventType == PeerReputationEventType.AssociatedWithBlockedContent &&
                e.ContentId == contentId &&
                e.Metadata == metadata), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordBlockedContentRequestAsync_WithValidParameters_RecordsEvent()
        {
            // Arrange
            var peerId = "test-peer";
            var contentId = "content-123";

            // Act
            await _service.RecordBlockedContentRequestAsync(peerId, contentId);

            // Assert
            _storeMock.Verify(s => s.RecordEventAsync(It.Is<PeerReputationEvent>(e =>
                e.PeerId == peerId &&
                e.EventType == PeerReputationEventType.RequestedBlockedContent &&
                e.ContentId == contentId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordBadCopyServedAsync_WithValidParameters_RecordsEvent()
        {
            // Arrange
            var peerId = "test-peer";
            var contentId = "content-123";
            var metadata = "hash mismatch";

            // Act
            await _service.RecordBadCopyServedAsync(peerId, contentId, metadata);

            // Assert
            _storeMock.Verify(s => s.RecordEventAsync(It.Is<PeerReputationEvent>(e =>
                e.PeerId == peerId &&
                e.EventType == PeerReputationEventType.ServedBadCopy &&
                e.ContentId == contentId &&
                e.Metadata == metadata), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordAbusiveBehaviorAsync_WithValidParameters_RecordsEvent()
        {
            // Arrange
            var peerId = "test-peer";
            var metadata = "excessive requests";

            // Act
            await _service.RecordAbusiveBehaviorAsync(peerId, metadata);

            // Assert
            _storeMock.Verify(s => s.RecordEventAsync(It.Is<PeerReputationEvent>(e =>
                e.PeerId == peerId &&
                e.EventType == PeerReputationEventType.AbusiveBehavior &&
                e.Metadata == metadata), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordProtocolViolationAsync_WithValidParameters_RecordsEvent()
        {
            // Arrange
            var peerId = "test-peer";
            var metadata = "malformed message";

            // Act
            await _service.RecordProtocolViolationAsync(peerId, metadata);

            // Assert
            _storeMock.Verify(s => s.RecordEventAsync(It.Is<PeerReputationEvent>(e =>
                e.PeerId == peerId &&
                e.EventType == PeerReputationEventType.ProtocolViolation &&
                e.Metadata == metadata), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task IsPeerAllowedForPlanningAsync_WhenPeerNotBanned_ReturnsTrue()
        {
            // Arrange
            var peerId = "good-peer";
            _storeMock.Setup(s => s.IsPeerBannedAsync(peerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.IsPeerAllowedForPlanningAsync(peerId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsPeerAllowedForPlanningAsync_WhenPeerBanned_ReturnsFalse()
        {
            // Arrange
            var peerId = "bad-peer";
            _storeMock.Setup(s => s.IsPeerBannedAsync(peerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsPeerAllowedForPlanningAsync(peerId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetReputationScoreAsync_ForwardsToStore()
        {
            // Arrange
            var peerId = "test-peer";
            var expectedScore = -5;
            _storeMock.Setup(s => s.GetReputationScoreAsync(peerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedScore);

            // Act
            var result = await _service.GetReputationScoreAsync(peerId);

            // Assert
            Assert.Equal(expectedScore, result);
        }

        [Fact]
        public async Task PerformMaintenanceAsync_ForwardsToStore()
        {
            // Act
            await _service.PerformMaintenanceAsync();

            // Assert
            _storeMock.Verify(s => s.DecayAndCleanupAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetStatsAsync_ForwardsToStore()
        {
            // Arrange
            var expectedStats = new PeerReputationStats
            {
                TotalEvents = 100,
                UniquePeers = 50,
                BannedPeers = 5
            };
            _storeMock.Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _service.GetStatsAsync();

            // Assert
            Assert.Equal(expectedStats, result);
        }
    }
}
