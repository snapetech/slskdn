// <copyright file="RealmChangeValidatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Migration
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Mesh.Realm;
    using slskd.Mesh.Realm.Migration;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-05: RealmChangeValidator.
    /// </summary>
    public class RealmChangeValidatorTests
    {
        private static IRealmService CreateRealmService(string realmId = "current-realm")
        {
            return new RealmServiceStub(realmId);
        }

        private static ILogger<RealmChangeValidator> CreateLogger() => Mock.Of<ILogger<RealmChangeValidator>>();

        private RealmChangeValidator CreateValidator(string currentRealmId = "current-realm")
        {
            return new RealmChangeValidator(CreateRealmService(currentRealmId), CreateLogger());
        }

        private sealed class RealmServiceStub : IRealmService
        {
            private readonly string _realmId;
            public RealmServiceStub(string realmId) => _realmId = realmId;
            public string CurrentRealmId => _realmId;
            public bool IsTrustedGovernanceRoot(string _) => false;
            public Task<bool> IsPeerAllowedInRealmAsync(string _, CancellationToken __) => Task.FromResult(true);
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
            Assert.True(result.Warnings.Any(w => w.Contains("Realm change approved", StringComparison.Ordinal)));
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
            Assert.True(result.Requirements.Count > 0, "Confirmation flow must populate Requirements");
            Assert.True(result.Requirements.Any(r => r.Contains("current-realm", StringComparison.Ordinal)));
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
            Assert.True(result.Requirements.Count > 0, "Wrong confirmation must populate Requirements");
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
            Assert.True(result.Warnings.Any(w => w.Contains("No realm change detected", StringComparison.Ordinal)));
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
            Assert.True(result.BreakingChanges.Any(b => b.Contains("Governance roots changed", StringComparison.Ordinal)));
            Assert.True(result.BreakingChanges.Any(b => b.Contains("Bootstrap nodes changed", StringComparison.Ordinal)));
            Assert.True(result.BreakingChanges.Any(b => b.Contains("ActivityPub follows", StringComparison.Ordinal)));
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
            Assert.True(result.Warnings.Any(w => w.Contains("disconnect the pod from its current realm", StringComparison.Ordinal)));
            Assert.True(result.Warnings.Any(w => w.Contains("cannot be easily undone", StringComparison.Ordinal)));
            Assert.True(result.Warnings.Any(w => w.Contains("Backup important data", StringComparison.Ordinal)));
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
            Assert.True(result.Warnings.Any(w => w.Contains("Multi-realm configuration", StringComparison.Ordinal)));
            Assert.True(result.Warnings.Any(w => w.Contains("bridging is enabled", StringComparison.OrdinalIgnoreCase)));
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
            Assert.True(result.Warnings.Any(w => w.Contains("bridging is disabled", StringComparison.OrdinalIgnoreCase)));
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

