// <copyright file="PeerReputationStoreTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Moderation;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP04: Peer Reputation Store implementation.
    /// </summary>
    public class PeerReputationStoreTests : IDisposable
    {
        private readonly Mock<ILogger<PeerReputationStore>> _loggerMock;
        private readonly Mock<IDataProtector> _dataProtectorMock;
        private readonly string _testStoragePath;
        private readonly PeerReputationStore _store;

        public PeerReputationStoreTests()
        {
            _loggerMock = new Mock<ILogger<PeerReputationStore>>();
            _dataProtectorMock = new Mock<IDataProtector>();

            // Mock data protector to return the same data (no actual encryption for tests)
            _dataProtectorMock.Setup(p => p.Protect(It.IsAny<byte[]>()))
                .Returns<byte[]>(data => data);
            _dataProtectorMock.Setup(p => p.Unprotect(It.IsAny<byte[]>()))
                .Returns<byte[]>(data => data);

            _testStoragePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "reputation.db");
            Directory.CreateDirectory(Path.GetDirectoryName(_testStoragePath)!);

            _store = new PeerReputationStore(_loggerMock.Object, _dataProtectorMock.Object, _testStoragePath);
        }

        [Fact]
        public async Task RecordEventAsync_WithValidEvent_RecordsSuccessfully()
        {
            // Arrange
            var peerId = "test-peer";
            var @event = new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.AssociatedWithBlockedContent,
                contentId: "content-123");

            // Act
            await _store.RecordEventAsync(@event);

            // Assert
            var score = await _store.GetReputationScoreAsync(peerId);
            Assert.True(score < 0); // Negative score for reputation events
        }

        [Fact]
        public async Task GetReputationScoreAsync_WithMultipleEvents_CalculatesCorrectScore()
        {
            // Arrange
            var peerId = "test-peer";

            // Record multiple events
            await _store.RecordEventAsync(new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.AssociatedWithBlockedContent));

            await _store.RecordEventAsync(new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.ServedBadCopy));

            // Act
            var score = await _store.GetReputationScoreAsync(peerId);

            // Assert
            // AssociatedWithBlockedContent = -3, ServedBadCopy = -2, Total = -5
            Assert.Equal(-5, score);
        }

        [Fact]
        public async Task IsPeerBannedAsync_WithScoreBelowThreshold_ReturnsTrue()
        {
            // Arrange
            var peerId = "bad-peer";

            // Record 10 events (exactly the ban threshold)
            for (int i = 0; i < 10; i++)
            {
                await _store.RecordEventAsync(new PeerReputationEvent(
                    peerId: peerId,
                    eventType: PeerReputationEventType.AssociatedWithBlockedContent));
            }

            // Act
            var isBanned = await _store.IsPeerBannedAsync(peerId);

            // Assert
            Assert.True(isBanned);
        }

        [Fact]
        public async Task IsPeerBannedAsync_WithScoreAboveThreshold_ReturnsFalse()
        {
            // Arrange
            var peerId = "good-peer";

            // Record only 5 events (below ban threshold)
            for (int i = 0; i < 5; i++)
            {
                await _store.RecordEventAsync(new PeerReputationEvent(
                    peerId: peerId,
                    eventType: PeerReputationEventType.AssociatedWithBlockedContent));
            }

            // Act
            var isBanned = await _store.IsPeerBannedAsync(peerId);

            // Assert
            Assert.False(isBanned);
        }

        [Fact]
        public async Task GetRecentEventsAsync_WithMultipleEvents_ReturnsRecentEvents()
        {
            // Arrange
            var peerId = "test-peer";

            // Record events with different timestamps
            await _store.RecordEventAsync(new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.AssociatedWithBlockedContent,
                timestamp: DateTimeOffset.UtcNow.AddHours(-1)));

            await _store.RecordEventAsync(new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.ServedBadCopy,
                timestamp: DateTimeOffset.UtcNow));

            // Act
            var events = await _store.GetRecentEventsAsync(peerId, maxEvents: 10);

            // Assert
            Assert.Equal(2, events.Count);
            Assert.Equal(PeerReputationEventType.ServedBadCopy, events[0].EventType); // Most recent first
            Assert.Equal(PeerReputationEventType.AssociatedWithBlockedContent, events[1].EventType);
        }

        [Fact]
        public async Task DecayAndCleanupAsync_WithOldEvents_DecaysAndRemovesOldEvents()
        {
            // Arrange
            var peerId = "test-peer";

            // Record an old event (beyond decay period)
            await _store.RecordEventAsync(new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.AssociatedWithBlockedContent,
                timestamp: DateTimeOffset.UtcNow.AddDays(-100))); // Very old

            // Record a recent event
            await _store.RecordEventAsync(new PeerReputationEvent(
                peerId: peerId,
                eventType: PeerReputationEventType.ServedBadCopy,
                timestamp: DateTimeOffset.UtcNow)); // Recent

            // Act
            await _store.DecayAndCleanupAsync();

            // Assert
            var score = await _store.GetReputationScoreAsync(peerId);
            var events = await _store.GetRecentEventsAsync(peerId);

            // Old event should be removed, recent event should be decayed but still present
            Assert.Equal(1, events.Count);
            Assert.Equal(PeerReputationEventType.ServedBadCopy, events[0].EventType);
        }

        [Fact]
        public async Task GetStatsAsync_WithMultiplePeers_ReturnsCorrectStatistics()
        {
            // Arrange
            var peer1 = "peer1";
            var peer2 = "peer2";

            // Peer1: 5 events
            for (int i = 0; i < 5; i++)
            {
                await _store.RecordEventAsync(new PeerReputationEvent(
                    peerId: peer1,
                    eventType: PeerReputationEventType.AssociatedWithBlockedContent));
            }

            // Peer2: 15 events (banned)
            for (int i = 0; i < 15; i++)
            {
                await _store.RecordEventAsync(new PeerReputationEvent(
                    peerId: peer2,
                    eventType: PeerReputationEventType.AssociatedWithBlockedContent));
            }

            // Act
            var stats = await _store.GetStatsAsync();

            // Assert
            Assert.Equal(20, stats.TotalEvents);
            Assert.Equal(2, stats.UniquePeers);
            Assert.Equal(1, stats.BannedPeers);
            Assert.Equal(3, stats.EventsByType[PeerReputationEventType.AssociatedWithBlockedContent]);
            Assert.True(stats.AverageReputationScore < 0);
        }

        public void Dispose()
        {
            // Clean up test files
            try
            {
                if (File.Exists(_testStoragePath))
                {
                    File.Delete(_testStoragePath);
                }

                var directory = Path.GetDirectoryName(_testStoragePath);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}

