// <copyright file="ActivityPubBridgeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Bridge
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Mesh.Realm;
    using slskd.Mesh.Realm.Bridge;
    using slskd.SocialFederation;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-04: ActivityPubBridge.
    /// </summary>
    public class ActivityPubBridgeTests
    {
        private static MultiRealmConfig ConfigWithReadAllowedWriteBlocked()
        {
            return new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-a", GovernanceRoots = new[] { "r" }, Policies = new RealmPolicies() },
                    new RealmConfig { Id = "realm-b", GovernanceRoots = new[] { "r" }, Policies = new RealmPolicies() }
                },
                Bridge = new BridgeConfig { Enabled = true, AllowedFlows = new[] { "activitypub:read" }, DisallowedFlows = Array.Empty<string>() }
            };
        }

        private static MultiRealmConfig ConfigWithNoAccess()
        {
            return new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-a", GovernanceRoots = new[] { "r" }, Policies = new RealmPolicies() },
                    new RealmConfig { Id = "realm-b", GovernanceRoots = new[] { "r" }, Policies = new RealmPolicies() }
                },
                Bridge = new BridgeConfig { Enabled = true, AllowedFlows = new[] { "metadata:read" }, DisallowedFlows = Array.Empty<string>() }
            };
        }

        private static BridgeFlowEnforcer CreateFlowEnforcer(MultiRealmConfig config)
        {
            var multiRealm = new MultiRealmService(
                Mock.Of<IOptionsMonitor<MultiRealmConfig>>(x => x.CurrentValue == config),
                Mock.Of<ILogger<MultiRealmService>>());
            return new BridgeFlowEnforcer(multiRealm, Mock.Of<ILogger<BridgeFlowEnforcer>>());
        }

        private static FederationService CreateFederationService()
        {
            var fedOpts = Mock.Of<IOptionsMonitor<slskd.Common.SocialFederationOptions>>(x => x.CurrentValue == new slskd.Common.SocialFederationOptions());
            var pubOpts = Mock.Of<IOptionsMonitor<FederationPublishingOptions>>(x => x.CurrentValue == new FederationPublishingOptions());
            var keyStore = Mock.Of<IActivityPubKeyStore>();
            var loggerFactory = new LoggerFactory();
            var libLogger = loggerFactory.CreateLogger<LibraryActorService>();
            var libActor = new LibraryActorService(fedOpts, keyStore, musicActor: null, libLogger, loggerFactory);
            var http = new HttpClient();
            var delivery = new ActivityDeliveryService(http, fedOpts, pubOpts, keyStore, Mock.Of<ILogger<ActivityDeliveryService>>());
            return new FederationService(fedOpts, pubOpts, libActor, keyStore, delivery, Mock.Of<ILogger<FederationService>>());
        }

        private static ActivityPubBridge CreateBridge(BridgeFlowEnforcer? enforcer = null)
        {
            var e = enforcer ?? CreateFlowEnforcer(ConfigWithReadAllowedWriteBlocked());
            return new ActivityPubBridge(e, CreateFederationService(), Mock.Of<ILogger<ActivityPubBridge>>());
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
            // Arrange - ConfigWithReadAllowedWriteBlocked already blocks write (activitypub:write not in AllowedFlows)
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
            // Arrange - ConfigWithReadAllowedWriteBlocked already blocks write
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
            // Arrange - use config that allows neither activitypub:read nor activitypub:write
            var enforcer = CreateFlowEnforcer(ConfigWithNoAccess());
            var bridge = CreateBridge(enforcer);

            // Act
            var result = bridge.IsRemoteRealmAccessible("realm-a", "realm-b");

            // Assert
            Assert.False(result); // No access allowed
        }

        [Fact]
        public void GetRemoteRealmCapabilities_WithMixedPermissions_ReturnsCorrectCapabilities()
        {
            // Arrange - ConfigWithReadAllowedWriteBlocked gives read true, write false
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
            // Arrange - ConfigWithNoAccess blocks both activitypub read and write
            var enforcer = CreateFlowEnforcer(ConfigWithNoAccess());
            var bridge = CreateBridge(enforcer);

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


