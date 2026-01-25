// <copyright file="RealmIsolationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Mesh.Realm;
    using Xunit;

    /// <summary>
    ///     Integration tests for T-REALM-01: Realm isolation verification.
    ///     Tests that pods with different realm IDs do not share overlays.
    /// </summary>
    public class RealmIsolationTests
    {
        [Fact]
        public async Task DifferentRealmIds_ProduceDifferentNamespaceSalts()
        {
            // Arrange - Two different realm configurations
            var config1 = new RealmConfig
            {
                Id = "realm-alpha",
                GovernanceRoots = new[] { "root1" },
                BootstrapNodes = Array.Empty<string>(),
                Policies = new RealmPolicies()
            };

            var config2 = new RealmConfig
            {
                Id = "realm-beta",
                GovernanceRoots = new[] { "root2" },
                BootstrapNodes = Array.Empty<string>(),
                Policies = new RealmPolicies()
            };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                Mock.Of<ILogger<RealmService>>());

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                Mock.Of<ILogger<RealmService>>());

            // Act
            await service1.InitializeAsync();
            await service2.InitializeAsync();

            // Assert - Different realms produce different namespace salts
            Assert.NotEqual(service1.RealmId, service2.RealmId);
            Assert.NotEqual(service1.NamespaceSalt, service2.NamespaceSalt);
        }

        [Fact]
        public void RealmService_IsSameRealm_CorrectlyIdentifiesRealmMembership()
        {
            // Arrange - Two services with different realm IDs
            var config1 = new RealmConfig { Id = "realm-one", GovernanceRoots = new[] { "root1" } };
            var config2 = new RealmConfig { Id = "realm-two", GovernanceRoots = new[] { "root2" } };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                Mock.Of<ILogger<RealmService>>());

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                Mock.Of<ILogger<RealmService>>());

            // Act & Assert
            Assert.True(service1.IsSameRealm("realm-one"));
            Assert.False(service1.IsSameRealm("realm-two"));

            Assert.True(service2.IsSameRealm("realm-two"));
            Assert.False(service2.IsSameRealm("realm-one"));
        }

        [Fact]
        public void RealmScopedIds_AreProperlyNamespaced()
        {
            // Arrange - Two services with different realms
            var config1 = new RealmConfig { Id = "realm-alpha", GovernanceRoots = new[] { "root1" } };
            var config2 = new RealmConfig { Id = "realm-beta", GovernanceRoots = new[] { "root2" } };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                Mock.Of<ILogger<RealmService>>());

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                Mock.Of<ILogger<RealmService>>());

            var baseId = "governance-doc-123";

            // Act
            var scopedId1 = service1.CreateRealmScopedId(baseId);
            var scopedId2 = service2.CreateRealmScopedId(baseId);

            // Assert - Same base ID produces different scoped IDs in different realms
            Assert.Equal("realm:realm-alpha:governance-doc-123", scopedId1);
            Assert.Equal("realm:realm-beta:governance-doc-123", scopedId2);
            Assert.NotEqual(scopedId1, scopedId2);

            // Verify realm membership
            Assert.True(service1.IsRealmScopedId(scopedId1));
            Assert.False(service1.IsRealmScopedId(scopedId2));

            Assert.True(service2.IsRealmScopedId(scopedId2));
            Assert.False(service2.IsRealmScopedId(scopedId1));
        }

        [Fact]
        public void GovernanceRootTrust_IsRealmSpecific()
        {
            // Arrange - Two realms with different trusted roots
            var config1 = new RealmConfig
            {
                Id = "realm-one",
                GovernanceRoots = new[] { "alpha-root", "shared-root" }
            };

            var config2 = new RealmConfig
            {
                Id = "realm-two",
                GovernanceRoots = new[] { "beta-root", "shared-root" }
            };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                Mock.Of<ILogger<RealmService>>());

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                Mock.Of<ILogger<RealmService>>());

            // Act & Assert
            // Realm 1 trusts alpha-root and shared-root
            Assert.True(service1.IsTrustedGovernanceRoot("alpha-root"));
            Assert.True(service1.IsTrustedGovernanceRoot("shared-root"));
            Assert.False(service1.IsTrustedGovernanceRoot("beta-root"));

            // Realm 2 trusts beta-root and shared-root
            Assert.True(service2.IsTrustedGovernanceRoot("beta-root"));
            Assert.True(service2.IsTrustedGovernanceRoot("shared-root"));
            Assert.False(service2.IsTrustedGovernanceRoot("alpha-root"));
        }

        [Fact]
        public void BootstrapNodes_AreRealmSpecific()
        {
            // Arrange - Two realms with different bootstrap nodes
            var config1 = new RealmConfig
            {
                Id = "realm-one",
                GovernanceRoots = new[] { "root1" },
                BootstrapNodes = new[] { "alpha-node:1234", "shared-node:5678" }
            };

            var config2 = new RealmConfig
            {
                Id = "realm-two",
                GovernanceRoots = new[] { "root2" },
                BootstrapNodes = new[] { "beta-node:9012", "shared-node:5678" }
            };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                Mock.Of<ILogger<RealmService>>());

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                Mock.Of<ILogger<RealmService>>());

            // Act
            var nodes1 = service1.GetBootstrapNodes();
            var nodes2 = service2.GetBootstrapNodes();

            // Assert - Different realms have different bootstrap nodes
            Assert.Contains("alpha-node:1234", nodes1);
            Assert.Contains("shared-node:5678", nodes1);
            Assert.DoesNotContain("beta-node:9012", nodes1);

            Assert.Contains("beta-node:9012", nodes2);
            Assert.Contains("shared-node:5678", nodes2);
            Assert.DoesNotContain("alpha-node:1234", nodes2);
        }

        [Fact]
        public void RealmPolicies_AreRealmSpecific()
        {
            // Arrange - Two realms with different policies
            var config1 = new RealmConfig
            {
                Id = "realm-one",
                GovernanceRoots = new[] { "root1" },
                Policies = new RealmPolicies
                {
                    GossipEnabled = true,
                    ReplicationEnabled = true,
                    MaxGossipHops = 5
                }
            };

            var config2 = new RealmConfig
            {
                Id = "realm-two",
                GovernanceRoots = new[] { "root2" },
                Policies = new RealmPolicies
                {
                    GossipEnabled = false,
                    ReplicationEnabled = true,
                    MaxGossipHops = 3
                }
            };

            var service1 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config1),
                Mock.Of<ILogger<RealmService>>());

            var service2 = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config2),
                Mock.Of<ILogger<RealmService>>());

            // Act
            var policies1 = service1.GetPolicies();
            var policies2 = service2.GetPolicies();

            // Assert - Different realms have different policies
            Assert.True(policies1.GossipEnabled);
            Assert.Equal(5, policies1.MaxGossipHops);

            Assert.False(policies2.GossipEnabled);
            Assert.Equal(3, policies2.MaxGossipHops);

            // Both have replication enabled
            Assert.True(policies1.ReplicationEnabled);
            Assert.True(policies2.ReplicationEnabled);
        }

        [Fact]
        public async Task RealmInitialization_LogsWarningsForGenericIds()
        {
            // Arrange - Use a generic realm ID
            var config = new RealmConfig
            {
                Id = "default", // Generic ID that should trigger warning
                GovernanceRoots = new[] { "root1" }
            };

            var loggerMock = new Mock<ILogger<RealmService>>();
            var service = new RealmService(
                Mock.Of<IOptionsMonitor<RealmConfig>>(x => x.CurrentValue == config),
                loggerMock.Object);

            // Act
            await service.InitializeAsync();

            // Assert - Should log a warning about generic realm ID
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("generic realm ID")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}


