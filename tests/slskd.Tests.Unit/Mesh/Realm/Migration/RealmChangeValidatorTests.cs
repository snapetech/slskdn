// <copyright file="RealmChangeValidatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Migration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Mesh.Realm;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-05: RealmChangeValidator.
    /// </summary>
    public class RealmChangeValidatorTests
    {
        private readonly Mock<IRealmService> _realmServiceMock = new();
        private readonly Mock<ILogger<RealmChangeValidator>> _loggerMock = new();

        public RealmChangeValidatorTests()
        {
            // Setup default realm service
            _realmServiceMock.Setup(x => x.RealmId).Returns("current-realm");
        }

        private RealmChangeValidator CreateValidator()
        {
            return new RealmChangeValidator(_realmServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_WithValidConfigAndConfirmation_ReturnsValid()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "new-realm",
                GovernanceRoots = new[] { "root1" },
                BootstrapNodes = new[] { "node1:1234" },
                Policies = new RealmPolicies()
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("current-realm", result.CurrentRealmId);
            Assert.Equal("new-realm", result.ProposedRealmId);
            Assert.Contains("Realm change approved", result.Warnings);
            Assert.True(result.BreakingChanges.Any());
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_WithInvalidConfig_ReturnsInvalid()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "", // Invalid
                GovernanceRoots = Array.Empty<string>()
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ValidationErrors);
            Assert.True(result.ValidationErrors.Any());
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_WithoutConfirmation_ReturnsInvalidWithRequirements()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "new-realm",
                GovernanceRoots = new[] { "root1" }
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, null);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.RequiresConfirmation);
            Assert.Contains("current-realm", result.Requirements.First());
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_WithWrongConfirmation_ReturnsInvalid()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "new-realm",
                GovernanceRoots = new[] { "root1" }
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, "wrong-confirmation");

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.RequiresConfirmation);
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_WithSameRealmId_ReturnsValidNoChange()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "current-realm", // Same as current
                GovernanceRoots = new[] { "root1" }
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.True(result.IsValid);
            Assert.Contains("No realm change detected", result.Warnings);
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_IncludesBreakingChanges()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "new-realm",
                GovernanceRoots = new[] { "new-root" },
                BootstrapNodes = new[] { "new-node:5678" },
                Policies = new RealmPolicies()
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.True(result.BreakingChanges.Any());
            Assert.Contains("Governance roots changed", result.BreakingChanges);
            Assert.Contains("Bootstrap nodes changed", result.BreakingChanges);
            Assert.Contains("ActivityPub follows", result.BreakingChanges);
        }

        [Fact]
        public async Task ValidateRealmChangeAsync_IncludesStandardWarnings()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new RealmConfig
            {
                Id = "new-realm",
                GovernanceRoots = new[] { "root1" }
            };

            // Act
            var result = await validator.ValidateRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.Contains("disconnect the pod from its current realm", result.Warnings);
            Assert.Contains("cannot be easily undone", result.Warnings);
            Assert.Contains("Backup important data", result.Warnings);
        }

        [Fact]
        public async Task ValidateMultiRealmChangeAsync_WithValidConfig_ReturnsAggregatedResult()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-a", GovernanceRoots = new[] { "root1" } },
                    new RealmConfig { Id = "realm-b", GovernanceRoots = new[] { "root2" } }
                },
                Bridge = new BridgeConfig { Enabled = true }
            };

            // Act
            var result = await validator.ValidateMultiRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.Equal(1, result.CurrentRealmCount);
            Assert.Equal(2, result.ProposedRealmCount);
            Assert.True(result.IsTransitionToMultiRealm);
            Assert.Equal(2, result.RealmChanges.Count);
            Assert.Contains("Multi-realm configuration", result.Warnings);
            Assert.Contains("bridging is enabled", result.Warnings);
        }

        [Fact]
        public async Task ValidateMultiRealmChangeAsync_WithBridgingDisabled_IncludesWarning()
        {
            // Arrange
            var validator = CreateValidator();
            var proposedConfig = new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-a", GovernanceRoots = new[] { "root1" } },
                    new RealmConfig { Id = "realm-b", GovernanceRoots = new[] { "root2" } }
                },
                Bridge = new BridgeConfig { Enabled = false }
            };

            // Act
            var result = await validator.ValidateMultiRealmChangeAsync(proposedConfig, "current-realm");

            // Assert
            Assert.Contains("bridging is disabled", result.Warnings);
        }

        [Fact]
        public void RealmChangeValidationResult_Summary_ProvidesUsefulInformation()
        {
            // Arrange - Valid change
            var result = new RealmChangeValidationResult
            {
                IsValid = true,
                CurrentRealmId = "old-realm",
                ProposedRealmId = "new-realm",
                BreakingChanges = new() { "change1", "change2" },
                Warnings = new() { "warn1", "warn2", "warn3" }
            };

            // Act
            var summary = result.Summary;

            // Assert
            Assert.Contains("Realm change", summary);
            Assert.Contains("2 breaking changes", summary);
            Assert.Contains("3 warnings", summary);
        }

        [Fact]
        public void RealmChangeValidationResult_Summary_WithInvalidResult_ShowsErrors()
        {
            // Arrange - Invalid change
            var result = new RealmChangeValidationResult
            {
                IsValid = false,
                ValidationErrors = new() { "error1", "error2" }
            };

            // Act
            var summary = result.Summary;

            // Assert
            Assert.Contains("Invalid", summary);
            Assert.Contains("error1", summary);
            Assert.Contains("error2", summary);
        }
    }
}
