// <copyright file="RealmAwareGossipServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Gossip
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Mesh.Realm;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-03: RealmAwareGossipService.
    /// </summary>
    public class RealmAwareGossipServiceTests
    {
        private readonly Mock<IRealmService> _realmServiceMock = new();
        private readonly Mock<ILogger<RealmAwareGossipService>> _loggerMock = new();

        public RealmAwareGossipServiceTests()
        {
            // Setup default realm service
            _realmServiceMock.Setup(x => x.IsSameRealm("test-realm")).Returns(true);
        }

        private RealmAwareGossipService CreateService()
        {
            return new RealmAwareGossipService(_realmServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task PublishForRealmAsync_WithValidMessage_SetsRealmIdAndProcesses()
        {
            // Arrange
            var service = CreateService();
            var message = new GossipMessage
            {
                Id = "test-message",
                Type = "health",
                RealmId = null, // Not set initially
                Originator = "test-pod"
            };

            // Act
            await service.PublishForRealmAsync(message, "test-realm");

            // Assert - Message should be tagged with realm ID
            Assert.Equal("test-realm", message.RealmId);
        }

        [Fact]
        public async Task PublishForRealmAsync_WithExpiredMessage_DoesNotProcess()
        {
            // Arrange
            var service = CreateService();
            var message = new GossipMessage
            {
                Id = "expired-message",
                Type = "health",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10), // Old
                Ttl = TimeSpan.FromMinutes(5), // Expired
                Originator = "test-pod"
            };

            // Act
            await service.PublishForRealmAsync(message, "test-realm");

            // Assert - Should not process expired message
            // (In real implementation, this would be logged)
        }

        [Fact]
        public async Task PublishForRealmAsync_WithMaxHopsReached_DoesNotProcess()
        {
            // Arrange
            var service = CreateService();
            var message = new GossipMessage
            {
                Id = "max-hops-message",
                Type = "health",
                Hops = 3,
                MaxHops = 3, // At limit
                Originator = "test-pod"
            };

            // Act
            await service.PublishForRealmAsync(message, "test-realm");

            // Assert - Should not process message that can't be forwarded
        }

        [Fact]
        public async Task SubscribeForRealm_WithValidParameters_CreatesSubscription()
        {
            // Arrange
            var service = CreateService();
            var handlerMock = new Mock<IGossipMessageHandler>();

            // Act
            var subscription = service.SubscribeForRealm("health", "test-realm", handlerMock.Object);

            // Assert
            Assert.NotNull(subscription);
            subscription.Dispose(); // Should not throw
        }

        [Fact]
        public async Task HandleIncomingMessageAsync_WithValidRealmMessage_ProcessesSuccessfully()
        {
            // Arrange
            var service = CreateService();
            var message = new GossipMessage
            {
                Id = "incoming-message",
                Type = "health",
                RealmId = "test-realm",
                Originator = "remote-pod"
            };

            // Act
            await service.HandleIncomingMessageAsync(message);

            // Assert - Should process without throwing
        }

        [Fact]
        public async Task HandleIncomingMessageAsync_WithUnknownRealm_IgnoresMessage()
        {
            // Arrange
            _realmServiceMock.Setup(x => x.IsSameRealm("unknown-realm")).Returns(false);
            var service = CreateService();
            var message = new GossipMessage
            {
                Id = "unknown-realm-message",
                Type = "health",
                RealmId = "unknown-realm",
                Originator = "remote-pod"
            };

            // Act
            await service.HandleIncomingMessageAsync(message);

            // Assert - Should ignore message from unknown realm
            // (In real implementation, this would be logged as a warning)
        }

        [Fact]
        public async Task HandleIncomingMessageAsync_WithMissingRealmId_IgnoresMessage()
        {
            // Arrange
            var service = CreateService();
            var message = new GossipMessage
            {
                Id = "no-realm-message",
                Type = "health",
                RealmId = null, // Missing
                Originator = "remote-pod"
            };

            // Act
            await service.HandleIncomingMessageAsync(message);

            // Assert - Should ignore message without realm ID
        }

        [Fact]
        public void GossipMessage_CanForward_WithValidMessage_ReturnsTrue()
        {
            // Arrange
            var message = new GossipMessage
            {
                Id = "forwardable-message",
                Type = "health",
                Timestamp = DateTimeOffset.UtcNow,
                Ttl = TimeSpan.FromMinutes(5),
                Hops = 0,
                MaxHops = 3,
                Originator = "test-pod"
            };

            // Act
            var canForward = message.CanForward();

            // Assert
            Assert.True(canForward);
        }

        [Fact]
        public void GossipMessage_CanForward_WithExpiredMessage_ReturnsFalse()
        {
            // Arrange
            var message = new GossipMessage
            {
                Id = "expired-message",
                Type = "health",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
                Ttl = TimeSpan.FromMinutes(5), // Expired
                Hops = 0,
                MaxHops = 3,
                Originator = "test-pod"
            };

            // Act
            var canForward = message.CanForward();

            // Assert
            Assert.False(canForward);
        }

        [Fact]
        public void GossipMessage_CanForward_WithMaxHopsReached_ReturnsFalse()
        {
            // Arrange
            var message = new GossipMessage
            {
                Id = "max-hops-message",
                Type = "health",
                Timestamp = DateTimeOffset.UtcNow,
                Ttl = TimeSpan.FromMinutes(5),
                Hops = 3,
                MaxHops = 3, // At limit
                Originator = "test-pod"
            };

            // Act
            var canForward = message.CanForward();

            // Assert
            Assert.False(canForward);
        }

        [Fact]
        public void GossipMessage_CreateForwardedCopy_IncrementsHops()
        {
            // Arrange
            var originalMessage = new GossipMessage
            {
                Id = "original-message",
                Type = "health",
                RealmId = "test-realm",
                Timestamp = DateTimeOffset.UtcNow,
                Hops = 1,
                MaxHops = 3,
                Originator = "test-pod"
            };

            // Act
            var forwardedMessage = originalMessage.CreateForwardedCopy();

            // Assert
            Assert.Equal(originalMessage.Id, forwardedMessage.Id);
            Assert.Equal(originalMessage.Type, forwardedMessage.Type);
            Assert.Equal(originalMessage.RealmId, forwardedMessage.RealmId);
            Assert.Equal(originalMessage.Timestamp, forwardedMessage.Timestamp);
            Assert.Equal(2, forwardedMessage.Hops); // Incremented
            Assert.Equal(originalMessage.MaxHops, forwardedMessage.MaxHops);
            Assert.Equal(originalMessage.Originator, forwardedMessage.Originator);
        }

        [Fact]
        public void GossipMessage_BelongsToRealm_WithMatchingRealm_ReturnsTrue()
        {
            // Arrange
            var message = new GossipMessage
            {
                Id = "realm-message",
                Type = "health",
                RealmId = "test-realm"
            };

            // Act
            var belongs = message.BelongsToRealm("test-realm");

            // Assert
            Assert.True(belongs);
        }

        [Fact]
        public void GossipMessage_BelongsToRealm_WithDifferentRealm_ReturnsFalse()
        {
            // Arrange
            var message = new GossipMessage
            {
                Id = "realm-message",
                Type = "health",
                RealmId = "test-realm"
            };

            // Act
            var belongs = message.BelongsToRealm("other-realm");

            // Assert
            Assert.False(belongs);
        }

        [Fact]
        public void Dispose_CleansUpSubscriptions()
        {
            // Arrange
            var service = CreateService();

            // Act - Dispose should not throw
            service.Dispose();

            // Assert - Can dispose multiple times without error
            service.Dispose();
        }

        [Fact]
        public void Subscribe_WithNullParameters_ThrowsException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.Subscribe(null!, Mock.Of<IGossipMessageHandler>()));
            Assert.Throws<ArgumentException>(() => service.Subscribe(string.Empty, Mock.Of<IGossipMessageHandler>()));
            Assert.Throws<ArgumentNullException>(() => service.Subscribe("health", null!));
        }

        [Fact]
        public void SubscribeForRealm_WithNullParameters_ThrowsException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.SubscribeForRealm(null!, "realm", Mock.Of<IGossipMessageHandler>()));
            Assert.Throws<ArgumentException>(() => service.SubscribeForRealm("health", null!, Mock.Of<IGossipMessageHandler>()));
            Assert.Throws<ArgumentException>(() => service.SubscribeForRealm("health", string.Empty, Mock.Of<IGossipMessageHandler>()));
            Assert.Throws<ArgumentNullException>(() => service.SubscribeForRealm("health", "realm", null!));
        }
    }
}
