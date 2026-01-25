// <copyright file="MultiRealmConfigTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm
{
    using System;
    using System.Linq;
    using slskd.Mesh.Realm;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-02: MultiRealmConfig.
    /// </summary>
    public class MultiRealmConfigTests
    {
        [Fact]
        public void Validate_WithValidMultiRealmConfig_ReturnsNoErrors()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig
                    {
                        Id = "realm-alpha",
                        GovernanceRoots = new[] { "root1" },
                        BootstrapNodes = new[] { "node1:1234" },
                        Policies = new RealmPolicies()
                    },
                    new RealmConfig
                    {
                        Id = "realm-beta",
                        GovernanceRoots = new[] { "root2" },
                        BootstrapNodes = new[] { "node2:5678" },
                        Policies = new RealmPolicies()
                    }
                },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "governance:read", "metadata:read" },
                    DisallowedFlows = new[] { "governance:root" }
                }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }

        [Fact]
        public void Validate_WithEmptyRealms_ReturnsError()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = Array.Empty<RealmConfig>(),
                Bridge = new BridgeConfig()
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("At least one realm configuration is required", errors[0].ErrorMessage);
        }

        [Fact]
        public void Validate_WithDuplicateRealmIds_ReturnsError()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "duplicate", GovernanceRoots = new[] { "root1" } },
                    new RealmConfig { Id = "duplicate", GovernanceRoots = new[] { "root2" } }
                },
                Bridge = new BridgeConfig()
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("Duplicate realm ID", errors[0].ErrorMessage);
        }

        [Fact]
        public void Validate_WithInvalidRealmConfig_ReturnsErrors()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = string.Empty, GovernanceRoots = Array.Empty<string>() }, // Invalid
                    new RealmConfig { Id = "valid", GovernanceRoots = new[] { "root1" } }
                },
                Bridge = new BridgeConfig()
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.True(errors.Count >= 2); // At least realm ID and governance root errors
        }

        [Fact]
        public void Validate_WithConflictingFlows_ReturnsError()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "governance:read", "metadata:read" },
                    DisallowedFlows = new[] { "metadata:read", "governance:root" } // metadata:read is in both
                }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("Conflicting flows", errors[0].ErrorMessage);
        }

        [Fact]
        public void Validate_WithInvalidFlowFormat_ReturnsError()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "invalid-format-no-colon" }
                }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("Invalid flow format", errors[0].ErrorMessage);
        }

        [Fact]
        public void GetRealm_WithExistingRealmId_ReturnsRealm()
        {
            // Arrange
            var realmAlpha = new RealmConfig { Id = "realm-alpha", GovernanceRoots = new[] { "root1" } };
            var realmBeta = new RealmConfig { Id = "realm-beta", GovernanceRoots = new[] { "root2" } };

            var config = new MultiRealmConfig
            {
                Realms = new[] { realmAlpha, realmBeta },
                Bridge = new BridgeConfig()
            };

            // Act
            var result = config.GetRealm("realm-alpha");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("realm-alpha", result.Id);
            Assert.Equal(realmAlpha, result);
        }

        [Fact]
        public void GetRealm_WithNonExistingRealmId_ReturnsNull()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "realm-alpha", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig()
            };

            // Act
            var result = config.GetRealm("non-existent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void RealmIds_ReturnsAllConfiguredRealmIds()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-alpha", GovernanceRoots = new[] { "root1" } },
                    new RealmConfig { Id = "realm-beta", GovernanceRoots = new[] { "root2" } }
                },
                Bridge = new BridgeConfig()
            };

            // Act
            var realmIds = config.RealmIds;

            // Assert
            Assert.Equal(2, realmIds.Count);
            Assert.Contains("realm-alpha", realmIds);
            Assert.Contains("realm-beta", realmIds);
        }

        [Fact]
        public void IsBridgingEnabled_WithBridgeEnabled_ReturnsTrue()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig { Enabled = true }
            };

            // Act
            var result = config.IsBridgingEnabled;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBridgingEnabled_WithBridgeDisabled_ReturnsFalse()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig { Enabled = false }
            };

            // Act
            var result = config.IsBridgingEnabled;

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("governance:read", true)]
        [InlineData("metadata:read", true)]
        [InlineData("federation:activitypub", true)]
        public void IsFlowAllowed_WithAllowedFlow_ReturnsTrue(string flow, bool expected)
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "governance:read", "metadata:read", "federation:activitypub" }
                }
            };

            // Act
            var result = config.IsFlowAllowed(flow);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsFlowAllowed_WithBridgingDisabled_ReturnsFalse()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig { Enabled = false }
            };

            // Act
            var result = config.IsFlowAllowed("governance:read");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsFlowAllowed_WithDisallowedFlow_ReturnsFalse()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "governance:read", "metadata:read" },
                    DisallowedFlows = new[] { "governance:root" }
                }
            };

            // Act & Assert
            Assert.False(config.IsFlowAllowed("governance:root")); // Explicitly disallowed
            Assert.True(config.IsFlowAllowed("governance:read")); // Allowed
        }

        [Fact]
        public void IsFlowAllowed_WithNoAllowedFlowsSpecified_AllowsAllExceptDisallowed()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = Array.Empty<string>(), // Empty = allow all
                    DisallowedFlows = new[] { "governance:root" }
                }
            };

            // Act & Assert
            Assert.True(config.IsFlowAllowed("governance:read"));
            Assert.True(config.IsFlowAllowed("metadata:read"));
            Assert.False(config.IsFlowAllowed("governance:root")); // Still disallowed
        }

        [Fact]
        public void IsFlowAllowed_WithNullOrEmptyFlow_ReturnsFalse()
        {
            // Arrange
            var config = new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig { Enabled = true }
            };

            // Act & Assert
            Assert.False(config.IsFlowAllowed(null!));
            Assert.False(config.IsFlowAllowed(string.Empty));
            Assert.False(config.IsFlowAllowed("   "));
        }
    }
}


