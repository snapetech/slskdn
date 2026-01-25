// <copyright file="BridgeFlowEnforcerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Bridge
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Mesh.Realm;
    using slskd.Mesh.Realm.Bridge;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-04: BridgeFlowEnforcer.
    /// </summary>
    public class BridgeFlowEnforcerTests
    {
        private static MultiRealmConfig ConfigWithActivityPubReadAndMetadataAllowed()
        {
            return new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-a", GovernanceRoots = new[] { "root-a" }, Policies = new RealmPolicies() },
                    new RealmConfig { Id = "realm-b", GovernanceRoots = new[] { "root-b" }, Policies = new RealmPolicies() }
                },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "activitypub:read", "metadata:read" },
                    DisallowedFlows = Array.Empty<string>()
                }
            };
        }

        private static MultiRealmConfig ConfigWithActivityPubReadBlocked()
        {
            return new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-a", GovernanceRoots = new[] { "root-a" }, Policies = new RealmPolicies() },
                    new RealmConfig { Id = "realm-b", GovernanceRoots = new[] { "root-b" }, Policies = new RealmPolicies() }
                },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "metadata:read" },
                    DisallowedFlows = Array.Empty<string>()
                }
            };
        }

        private static MultiRealmService CreateMultiRealmService(MultiRealmConfig config)
        {
            return new MultiRealmService(
                Mock.Of<IOptionsMonitor<MultiRealmConfig>>(x => x.CurrentValue == config),
                Mock.Of<ILogger<MultiRealmService>>());
        }

        private static BridgeFlowEnforcer CreateEnforcer(MultiRealmService? multiRealm = null)
        {
            var svc = multiRealm ?? CreateMultiRealmService(ConfigWithActivityPubReadAndMetadataAllowed());
            return new BridgeFlowEnforcer(svc, Mock.Of<ILogger<BridgeFlowEnforcer>>());
        }

        [Fact]
        public void IsActivityPubReadAllowed_WithAllowedFlow_ReturnsTrue()
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act
            var result = enforcer.IsActivityPubReadAllowed("realm-a", "realm-b");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsActivityPubWriteAllowed_WithBlockedFlow_ReturnsFalse()
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act
            var result = enforcer.IsActivityPubWriteAllowed("realm-a", "realm-b");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsMetadataReadAllowed_WithAllowedFlow_ReturnsTrue()
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act
            var result = enforcer.IsMetadataReadAllowed("realm-a", "realm-b");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task PerformActivityPubReadAsync_WithAllowedFlow_ExecutesOperation()
        {
            // Arrange
            var enforcer = CreateEnforcer();
            var operationExecuted = false;

            // Act
            var result = await enforcer.PerformActivityPubReadAsync(
                "realm-a",
                "realm-b",
                () =>
                {
                    operationExecuted = true;
                    return Task.FromResult(BridgeOperationResult.CreateSuccess("test-data"));
                });

            // Assert
            Assert.True(result.Success);
            Assert.True(operationExecuted);
            Assert.Equal("test-data", result.Data);
        }

        [Fact]
        public async Task PerformActivityPubReadAsync_WithBlockedFlow_ReturnsBlocked()
        {
            // Arrange - use config that does not allow activitypub:read
            var enforcer = CreateEnforcer(CreateMultiRealmService(ConfigWithActivityPubReadBlocked()));
            var operationExecuted = false;

            // Act
            var result = await enforcer.PerformActivityPubReadAsync(
                "realm-a",
                "realm-b",
                () =>
                {
                    operationExecuted = true;
                    return Task.FromResult(BridgeOperationResult.CreateSuccess("test-data"));
                });

            // Assert
            Assert.False(result.Success);
            Assert.True(result.WasBlocked);
            Assert.Contains("activitypub:read flow not allowed", result.ErrorMessage);
            Assert.False(operationExecuted); // Operation should not execute
        }

        [Fact]
        public async Task PerformActivityPubWriteAsync_WithBlockedFlow_ReturnsBlocked()
        {
            // Arrange
            var enforcer = CreateEnforcer();
            var operationExecuted = false;

            // Act
            var result = await enforcer.PerformActivityPubWriteAsync(
                "realm-a",
                "realm-b",
                () =>
                {
                    operationExecuted = true;
                    return Task.FromResult(BridgeOperationResult.CreateSuccess("test-data"));
                });

            // Assert
            Assert.False(result.Success);
            Assert.True(result.WasBlocked);
            Assert.Contains("activitypub:write flow not allowed", result.ErrorMessage);
            Assert.False(operationExecuted); // Operation should not execute
        }

        [Fact]
        public async Task PerformMetadataReadAsync_WithAllowedFlow_ExecutesOperation()
        {
            // Arrange
            var enforcer = CreateEnforcer();
            var operationExecuted = false;

            // Act
            var result = await enforcer.PerformMetadataReadAsync(
                "realm-a",
                "realm-b",
                () =>
                {
                    operationExecuted = true;
                    return Task.FromResult(BridgeOperationResult.CreateSuccess("metadata"));
                });

            // Assert
            Assert.True(result.Success);
            Assert.True(operationExecuted);
            Assert.Equal("metadata", result.Data);
        }

        [Fact]
        public void ValidateCrossRealmOperation_WithSameRealm_ReturnsTrue()
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act
            var result = enforcer.ValidateCrossRealmOperation(
                "same-realm", "same-realm", "any:flow", "test operation");

            // Assert
            Assert.True(result); // Same realm operations always allowed
        }

        [Fact]
        public void ValidateCrossRealmOperation_WithForbiddenFlow_ReturnsFalse()
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act
            var result = enforcer.ValidateCrossRealmOperation(
                "realm-a", "realm-b", "governance:root", "dangerous operation");

            // Assert
            Assert.False(result); // Governance flows always forbidden
        }

        [Fact]
        public void ValidateCrossRealmOperation_WithInvalidParameters_ReturnsFalse()
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act & Assert
            Assert.False(enforcer.ValidateCrossRealmOperation(null!, "realm-b", "flow", "test"));
            Assert.False(enforcer.ValidateCrossRealmOperation("realm-a", null!, "flow", "test"));
            Assert.False(enforcer.ValidateCrossRealmOperation("realm-a", "realm-b", null!, "test"));
            Assert.False(enforcer.ValidateCrossRealmOperation("realm-a", "realm-b", "", "test"));
        }

        [Theory]
        [InlineData("activitypub:read", true)]
        [InlineData("activitypub:write", false)]
        [InlineData("metadata:read", true)]
        [InlineData("governance:read", false)] // Always forbidden
        [InlineData("mcp:control", false)] // Always forbidden
        public void ValidateCrossRealmOperation_EnforcesFlowPolicies(string flow, bool expected)
        {
            // Arrange
            var enforcer = CreateEnforcer();

            // Act
            var result = enforcer.ValidateCrossRealmOperation(
                "realm-a", "realm-b", flow, "test operation");

            // Assert
            Assert.Equal(expected, result);
        }
    }
}


