// <copyright file="RealmConfigTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm
{
    using System.Linq;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-01: RealmConfig.
    /// </summary>
    public class RealmConfigTests
    {
        [Fact]
        public void GetNamespaceSalt_WithValidRealmId_ReturnsConsistentSalt()
        {
            // Arrange
            var config = new RealmConfig { Id = "test-realm" };

            // Act
            var salt1 = config.GetNamespaceSalt();
            var salt2 = config.GetNamespaceSalt();

            // Assert
            Assert.NotNull(salt1);
            Assert.Equal(32, salt1.Length); // SHA256 hash size
            Assert.Equal(salt1, salt2); // Should be deterministic
        }

        [Fact]
        public void GetNamespaceSalt_WithEmptyRealmId_ThrowsException()
        {
            // Arrange
            var config = new RealmConfig { Id = string.Empty };

            // Act & Assert
            Assert.Throws<System.InvalidOperationException>(() => config.GetNamespaceSalt());
        }

        [Fact]
        public void IsTrustedGovernanceRoot_WithTrustedRoot_ReturnsTrue()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "test-realm",
                GovernanceRoots = new[] { "root1", "root2" }
            };

            // Act
            var result = config.IsTrustedGovernanceRoot("root1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsTrustedGovernanceRoot_WithUntrustedRoot_ReturnsFalse()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "test-realm",
                GovernanceRoots = new[] { "root1", "root2" }
            };

            // Act
            var result = config.IsTrustedGovernanceRoot("root3");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsTrustedGovernanceRoot_WithNullOrEmpty_ReturnsFalse()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "test-realm",
                GovernanceRoots = new[] { "root1" }
            };

            // Act & Assert
            Assert.False(config.IsTrustedGovernanceRoot(null!));
            Assert.False(config.IsTrustedGovernanceRoot(string.Empty));
            Assert.False(config.IsTrustedGovernanceRoot("   "));
        }

        [Fact]
        public void Validate_WithValidConfig_ReturnsNoErrors()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "valid-realm-id",
                GovernanceRoots = new[] { "trusted-root-1" },
                BootstrapNodes = new[] { "node1:1234" },
                Policies = new RealmPolicies()
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }

        [Fact]
        public void Validate_WithEmptyRealmId_ReturnsError()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = string.Empty,
                GovernanceRoots = new[] { "trusted-root-1" }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("Realm ID is required", errors[0].ErrorMessage);
            Assert.Contains(nameof(RealmConfig.Id), errors[0].MemberNames);
        }

        [Fact]
        public void Validate_WithNoGovernanceRoots_ReturnsError()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "test-realm",
                GovernanceRoots = System.Array.Empty<string>()
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("governance root", errors[0].ErrorMessage.ToLowerInvariant());
        }

        [Fact]
        public void Validate_WithInvalidRealmIdCharacters_ReturnsError()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "invalid realm with spaces",
                GovernanceRoots = new[] { "trusted-root-1" }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("Realm ID must be", errors[0].ErrorMessage);
        }

        [Fact]
        public void Validate_WithRealmIdTooShort_ReturnsError()
        {
            // Arrange
            var config = new RealmConfig
            {
                Id = "ab", // Too short
                GovernanceRoots = new[] { "trusted-root-1" }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("Realm ID must be", errors[0].ErrorMessage);
        }

        [Fact]
        public void Validate_WithProblematicRealmIdPatterns_ReturnsError()
        {
            // Arrange - Test consecutive periods
            var config = new RealmConfig
            {
                Id = "test..realm",
                GovernanceRoots = new[] { "trusted-root-1" }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert
            Assert.Single(errors);
            Assert.Contains("consecutive periods", errors[0].ErrorMessage);
        }

        [Theory]
        [InlineData("default")]
        [InlineData("realm")]
        [InlineData("main")]
        [InlineData("test")]
        [InlineData("dev")]
        [InlineData("prod")]
        public void Validate_WithGenericRealmId_DoesNotReturnError(string genericId)
        {
            // Arrange - Generic IDs should be allowed but logged as warnings
            var config = new RealmConfig
            {
                Id = genericId,
                GovernanceRoots = new[] { "trusted-root-1" }
            };

            // Act
            var errors = config.Validate().ToList();

            // Assert - Should not fail validation (warnings are logged elsewhere)
            Assert.Empty(errors);
        }

        [Fact]
        public void DifferentRealmIds_ProduceDifferentNamespaceSalts()
        {
            // Arrange
            var config1 = new RealmConfig { Id = "realm-one" };
            var config2 = new RealmConfig { Id = "realm-two" };

            // Act
            var salt1 = config1.GetNamespaceSalt();
            var salt2 = config2.GetNamespaceSalt();

            // Assert
            Assert.NotEqual(salt1, salt2);
        }
    }
}


