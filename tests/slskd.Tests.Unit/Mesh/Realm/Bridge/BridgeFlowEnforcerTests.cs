// <copyright file="BridgeFlowEnforcerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Bridge
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Mesh.Realm;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-04: BridgeFlowEnforcer.
    /// </summary>
    public class BridgeFlowEnforcerTests
    {
        private readonly Mock<MultiRealmService> _multiRealmServiceMock = new();
        private readonly Mock<ILogger<BridgeFlowEnforcer>> _loggerMock = new();

        public BridgeFlowEnforcerTests()
        {
            // Setup default multi-realm service
            _multiRealmServiceMock.Setup(x => x.IsCrossRealmOperationPermitted("realm-a", "realm-b", "activitypub:read")).Returns(true);
            _multiRealmServiceMock.Setup(x => x.IsCrossRealmOperationPermitted("realm-a", "realm-b", "activitypub:write")).Returns(false);
            _multiRealmServiceMock.Setup(x => x.IsCrossRealmOperationPermitted("realm-a", "realm-b", "metadata:read")).Returns(true);
        }

        private BridgeFlowEnforcer CreateEnforcer()
        {
            return new BridgeFlowEnforcer(_multiRealmServiceMock.Object, _loggerMock.Object);
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
                    return Task.FromResult(BridgeOperationResult.Success("test-data"));
                });

            // Assert
            Assert.True(result.Success);
            Assert.True(operationExecuted);
            Assert.Equal("test-data", result.Data);
        }

        [Fact]
        public async Task PerformActivityPubReadAsync_WithBlockedFlow_ReturnsBlocked()
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
                    return Task.FromResult(BridgeOperationResult.Success("test-data"));
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
                    return Task.FromResult(BridgeOperationResult.Success("test-data"));
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
                    return Task.FromResult(BridgeOperationResult.Success("metadata"));
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
