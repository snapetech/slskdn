// <copyright file="ActivityPubBridgeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Bridge
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.SocialFederation;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-04: ActivityPubBridge.
    /// </summary>
    public class ActivityPubBridgeTests
    {
        private readonly Mock<BridgeFlowEnforcer> _flowEnforcerMock = new();
        private readonly Mock<FederationService> _federationServiceMock = new();
        private readonly Mock<ILogger<ActivityPubBridge>> _loggerMock = new();

        public ActivityPubBridgeTests()
        {
            // Setup default flow enforcer behavior
            _flowEnforcerMock.Setup(x => x.IsActivityPubReadAllowed("realm-a", "realm-b")).Returns(true);
            _flowEnforcerMock.Setup(x => x.IsActivityPubWriteAllowed("realm-a", "realm-b")).Returns(false);
            _flowEnforcerMock.Setup(x => x.PerformActivityPubReadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task<BridgeOperationResult>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (string local, string remote, Func<Task<BridgeOperationResult>> op, CancellationToken ct) => await op());
            _flowEnforcerMock.Setup(x => x.PerformActivityPubWriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task<BridgeOperationResult>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (string local, string remote, Func<Task<BridgeOperationResult>> op, CancellationToken ct) => await op());
        }

        private ActivityPubBridge CreateBridge()
        {
            return new ActivityPubBridge(_flowEnforcerMock.Object, _federationServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task FollowRemoteActorAsync_WithAllowedFlow_Succeeds()
        {
            // Arrange
            var bridge = CreateBridge();

            // Act
            var result = await bridge.FollowRemoteActorAsync("realm-a", "realm-b", "actor-123");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task MirrorRemotePostAsync_WithAllowedFlow_Succeeds()
        {
            // Arrange
            var bridge = CreateBridge();

            // Act
            var result = await bridge.MirrorRemotePostAsync("realm-a", "realm-b", "post-123", true);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task ShareToRemoteRealmAsync_WithBlockedFlow_ReturnsBlocked()
        {
            // Arrange - Block ActivityPub write
            _flowEnforcerMock.Setup(x => x.IsActivityPubWriteAllowed("realm-a", "realm-b")).Returns(false);
            _flowEnforcerMock.Setup(x => x.PerformActivityPubWriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task<BridgeOperationResult>>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(BridgeOperationResult.Blocked("activitypub:write flow not allowed")));

            var bridge = CreateBridge();

            // Act
            var result = await bridge.ShareToRemoteRealmAsync("realm-a", "realm-b", new { content = "test" });

            // Assert
            Assert.False(result.Success);
            Assert.True(result.WasBlocked);
            Assert.Contains("activitypub:write flow not allowed", result.ErrorMessage);
        }

        [Fact]
        public async Task AnnounceRemoteContentAsync_WithBlockedFlow_ReturnsBlocked()
        {
            // Arrange - Block ActivityPub write
            _flowEnforcerMock.Setup(x => x.IsActivityPubWriteAllowed("realm-a", "realm-b")).Returns(false);
            _flowEnforcerMock.Setup(x => x.PerformActivityPubWriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task<BridgeOperationResult>>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(BridgeOperationResult.Blocked("activitypub:write flow not allowed")));

            var bridge = CreateBridge();

            // Act
            var result = await bridge.AnnounceRemoteContentAsync("realm-a", "realm-b", "post-123", "Great post!");

            // Assert
            Assert.False(result.Success);
            Assert.True(result.WasBlocked);
            Assert.Contains("activitypub:write flow not allowed", result.ErrorMessage);
        }

        [Fact]
        public void IsRemoteRealmAccessible_WithReadAllowed_ReturnsTrue()
        {
            // Arrange
            var bridge = CreateBridge();

            // Act
            var result = bridge.IsRemoteRealmAccessible("realm-a", "realm-b");

            // Assert
            Assert.True(result); // Read is allowed
        }

        [Fact]
        public void IsRemoteRealmAccessible_WithNoAccess_ReturnsFalse()
        {
            // Arrange - Block both read and write
            _flowEnforcerMock.Setup(x => x.IsActivityPubReadAllowed("realm-a", "realm-b")).Returns(false);
            _flowEnforcerMock.Setup(x => x.IsActivityPubWriteAllowed("realm-a", "realm-b")).Returns(false);

            var bridge = CreateBridge();

            // Act
            var result = bridge.IsRemoteRealmAccessible("realm-a", "realm-b");

            // Assert
            Assert.False(result); // No access allowed
        }

        [Fact]
        public void GetRemoteRealmCapabilities_WithMixedPermissions_ReturnsCorrectCapabilities()
        {
            // Arrange - Allow read but not write
            _flowEnforcerMock.Setup(x => x.IsActivityPubReadAllowed("realm-a", "realm-b")).Returns(true);
            _flowEnforcerMock.Setup(x => x.IsActivityPubWriteAllowed("realm-a", "realm-b")).Returns(false);

            var bridge = CreateBridge();

            // Act
            var capabilities = bridge.GetRemoteRealmCapabilities("realm-a", "realm-b");

            // Assert
            Assert.True(capabilities.CanRead);
            Assert.False(capabilities.CanWrite);
            Assert.True(capabilities.CanFollow); // Follow uses read permission
            Assert.True(capabilities.CanMirror); // Mirror uses read permission
            Assert.False(capabilities.CanShare); // Share uses write permission
            Assert.False(capabilities.CanAnnounce); // Announce uses write permission
            Assert.True(capabilities.AnyAllowed);
        }

        [Fact]
        public void GetRemoteRealmCapabilities_WithNoPermissions_ReturnsAllFalse()
        {
            // Arrange - Block all ActivityPub operations
            _flowEnforcerMock.Setup(x => x.IsActivityPubReadAllowed("realm-a", "realm-b")).Returns(false);
            _flowEnforcerMock.Setup(x => x.IsActivityPubWriteAllowed("realm-a", "realm-b")).Returns(false);

            var bridge = CreateBridge();

            // Act
            var capabilities = bridge.GetRemoteRealmCapabilities("realm-a", "realm-b");

            // Assert
            Assert.False(capabilities.CanRead);
            Assert.False(capabilities.CanWrite);
            Assert.False(capabilities.CanFollow);
            Assert.False(capabilities.CanMirror);
            Assert.False(capabilities.CanShare);
            Assert.False(capabilities.CanAnnounce);
            Assert.False(capabilities.AnyAllowed);
        }
    }
}

