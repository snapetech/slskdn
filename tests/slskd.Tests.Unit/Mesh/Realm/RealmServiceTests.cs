// <copyright file="RealmServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-01: RealmService.
    /// </summary>
    public class RealmServiceTests
    {
        private readonly Mock<IOptionsMonitor<RealmConfig>> _configMock = new();
        private readonly Mock<ILogger<RealmService>> _loggerMock = new();

        public RealmServiceTests()
        {
            // Setup default valid config
            _configMock.Setup(x => x.CurrentValue).Returns(new RealmConfig
            {
                Id = "test-realm",
                GovernanceRoots = new[] { "trusted-root-1" },
                BootstrapNodes = new[] { "node1:1234" },
                Policies = new RealmPolicies()
            });
        }

        private RealmService CreateService()
        {
            return new RealmService(_configMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task InitializeAsync_WithValidConfig_Succeeds()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.InitializeAsync();

            // Assert
            Assert.Equal("test-realm", service.RealmId);
            Assert.NotNull(service.NamespaceSalt);
            Assert.Equal(32, service.NamespaceSalt.Length); // SHA256
        }

        [Fact]
        public async Task InitializeAsync_WithInvalidConfig_ThrowsException()
        {
            // Arrange
            _configMock.Setup(x => x.CurrentValue).Returns(new RealmConfig
            {
                Id = string.Empty, // Invalid
                GovernanceRoots = System.Array.Empty<string>()
            });

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
        }

        [Fact]
        public void IsSameRealm_WithMatchingRealmId_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsSameRealm("test-realm");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsSameRealm_WithDifferentRealmId_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsSameRealm("different-realm");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSameRealm_WithDifferentCase_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsSameRealm("TEST-REALM");

            // Assert
            Assert.True(result); // Case insensitive
        }

        [Fact]
        public void IsTrustedGovernanceRoot_WithTrustedRoot_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsTrustedGovernanceRoot("trusted-root-1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsTrustedGovernanceRoot_WithUntrustedRoot_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsTrustedGovernanceRoot("untrusted-root");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetBootstrapNodes_ReturnsConfiguredNodes()
        {
            // Arrange
            var service = CreateService();

            // Act
            var nodes = service.GetBootstrapNodes();

            // Assert
            Assert.Single(nodes);
            Assert.Equal("node1:1234", nodes[0]);
        }

        [Fact]
        public void GetPolicies_ReturnsConfiguredPolicies()
        {
            // Arrange
            var service = CreateService();

            // Act
            var policies = service.GetPolicies();

            // Assert
            Assert.NotNull(policies);
            Assert.True(policies.GossipEnabled); // Default value
        }

        [Fact]
        public void CreateRealmScopedId_WithValidIdentifier_ReturnsScopedId()
        {
            // Arrange
            var service = CreateService();

            // Act
            var scopedId = service.CreateRealmScopedId("test-identifier");

            // Assert
            Assert.Equal("realm:test-realm:test-identifier", scopedId);
        }

        [Fact]
        public void CreateRealmScopedId_WithNullIdentifier_ThrowsException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.CreateRealmScopedId(null!));
        }

        [Fact]
        public void CreateRealmScopedId_WithEmptyIdentifier_ThrowsException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.CreateRealmScopedId(string.Empty));
        }

        [Theory]
        [InlineData("realm:test-realm:identifier", "test-realm", "identifier")]
        [InlineData("realm:other-realm:doc", "other-realm", "doc")]
        [InlineData("realm:complex.realm.id:some:id", "complex.realm.id", "some:id")]
        public void TryParseRealmScopedId_WithValidScopedId_ParsesCorrectly(string scopedId, string expectedRealmId, string expectedIdentifier)
        {
            // Act
            var result = RealmService.TryParseRealmScopedId(scopedId, out var realmId, out var identifier);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedRealmId, realmId);
            Assert.Equal(expectedIdentifier, identifier);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("realm:")]
        [InlineData("realm:realmid")]
        [InlineData("notrealm:realmid:identifier")]
        public void TryParseRealmScopedId_WithInvalidScopedId_ReturnsFalse(string scopedId)
        {
            // Act
            var result = RealmService.TryParseRealmScopedId(scopedId, out var realmId, out var identifier);

            // Assert
            Assert.False(result);
            Assert.Empty(realmId);
            Assert.Empty(identifier);
        }

        [Fact]
        public void IsRealmScopedId_WithMatchingRealm_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();
            var scopedId = "realm:test-realm:some-doc";

            // Act
            var result = service.IsRealmScopedId(scopedId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRealmScopedId_WithDifferentRealm_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            var scopedId = "realm:other-realm:some-doc";

            // Act
            var result = service.IsRealmScopedId(scopedId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsRealmScopedId_WithInvalidFormat_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            var scopedId = "invalid-format";

            // Act
            var result = service.IsRealmScopedId(scopedId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void NamespaceSalt_IsDeterministic()
        {
            // Arrange
            var service = CreateService();

            // Act
            var salt1 = service.NamespaceSalt;
            var salt2 = service.NamespaceSalt;

            // Assert
            Assert.Equal(salt1, salt2);
        }

        [Fact]
        public void DifferentRealmConfigs_ProduceDifferentSalts()
        {
            // Arrange
            var config1 = new RealmConfig { Id = "realm-one" };
            var config2 = new RealmConfig { Id = "realm-two" };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                _loggerMock.Object);

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                _loggerMock.Object);

            // Act
            var salt1 = service1.NamespaceSalt;
            var salt2 = service2.NamespaceSalt;

            // Assert
            Assert.NotEqual(salt1, salt2);
        }
    }
}


