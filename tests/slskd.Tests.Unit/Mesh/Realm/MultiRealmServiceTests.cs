// <copyright file="MultiRealmServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-02: MultiRealmService.
    /// </summary>
    public class MultiRealmServiceTests
    {
        private readonly Mock<IOptionsMonitor<MultiRealmConfig>> _configMock = new();
        private readonly Mock<ILogger<MultiRealmService>> _loggerMock = new();

        public MultiRealmServiceTests()
        {
            // Setup default valid multi-realm config
            _configMock.Setup(x => x.CurrentValue).Returns(new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig
                    {
                        Id = "realm-alpha",
                        GovernanceRoots = new[] { "alpha-root" },
                        BootstrapNodes = new[] { "alpha-node:1234" },
                        Policies = new RealmPolicies()
                    },
                    new RealmConfig
                    {
                        Id = "realm-beta",
                        GovernanceRoots = new[] { "beta-root" },
                        BootstrapNodes = new[] { "beta-node:5678" },
                        Policies = new RealmPolicies()
                    }
                },
                Bridge = new BridgeConfig
                {
                    Enabled = true,
                    AllowedFlows = new[] { "governance:read", "metadata:read" },
                    DisallowedFlows = new[] { "governance:root" }
                }
            });
        }

        private MultiRealmService CreateService()
        {
            return new MultiRealmService(_configMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task InitializeAsync_WithValidConfig_InitializesAllRealmServices()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.InitializeAsync();

            // Assert
            Assert.Equal(2, service.RealmIds.Count);
            Assert.Contains("realm-alpha", service.RealmIds);
            Assert.Contains("realm-beta", service.RealmIds);

            var allServices = service.GetAllRealmServices();
            Assert.Equal(2, allServices.Count);
            Assert.Contains("realm-alpha", allServices.Keys);
            Assert.Contains("realm-beta", allServices.Keys);
        }

        [Fact]
        public async Task InitializeAsync_WithInvalidConfig_ThrowsException()
        {
            // Arrange - Empty realms array
            _configMock.Setup(x => x.CurrentValue).Returns(new MultiRealmConfig
            {
                Realms = Array.Empty<RealmConfig>(),
                Bridge = new BridgeConfig()
            });

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
        }

        [Fact]
        public async Task GetRealmService_WithExistingRealmId_ReturnsRealmService()
        {
            // Arrange
            var service = CreateService();
            await service.InitializeAsync();

            // Act
            var realmService = service.GetRealmService("realm-alpha");

            // Assert
            Assert.NotNull(realmService);
            Assert.Equal("realm-alpha", realmService.RealmId);
        }

        [Fact]
        public async Task GetRealmService_WithNonExistingRealmId_ReturnsNull()
        {
            // Arrange
            var service = CreateService();
            await service.InitializeAsync();

            // Act
            var realmService = service.GetRealmService("non-existent");

            // Assert
            Assert.Null(realmService);
        }

        [Fact]
        public void IsBridgingEnabled_WithBridgeEnabled_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsBridgingEnabled;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBridgingEnabled_WithBridgeDisabled_ReturnsFalse()
        {
            // Arrange - Disable bridging
            _configMock.Setup(x => x.CurrentValue).Returns(new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig { Enabled = false }
            });

            var service = CreateService();

            // Act
            var result = service.IsBridgingEnabled;

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("governance:read", true)]
        [InlineData("metadata:read", true)]
        [InlineData("federation:activitypub", true)]
        [InlineData("governance:root", false)]
        public void IsFlowAllowed_EnforcesBridgePolicies(string flow, bool expected)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsFlowAllowed(flow);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CreateBridgeScopedId_WithBridgingEnabled_ReturnsScopedId()
        {
            // Arrange
            var service = CreateService();

            // Act
            var scopedId = service.CreateBridgeScopedId("realm-alpha", "test-identifier");

            // Assert
            Assert.NotNull(scopedId);
            Assert.Equal("bridge:realm-alpha:test-identifier", scopedId);
        }

        [Fact]
        public void CreateBridgeScopedId_WithBridgingDisabled_ReturnsNull()
        {
            // Arrange - Disable bridging
            _configMock.Setup(x => x.CurrentValue).Returns(new MultiRealmConfig
            {
                Realms = new[] { new RealmConfig { Id = "test", GovernanceRoots = new[] { "root1" } } },
                Bridge = new BridgeConfig { Enabled = false }
            });

            var service = CreateService();

            // Act
            var scopedId = service.CreateBridgeScopedId("realm-alpha", "test-identifier");

            // Assert
            Assert.Null(scopedId);
        }

        [Theory]
        [InlineData("realm-alpha", "realm-alpha", "governance:read", true)] // Same realm always allowed
        [InlineData("realm-alpha", "realm-beta", "governance:read", true)] // Cross-realm allowed flow
        [InlineData("realm-alpha", "realm-beta", "governance:root", false)] // Cross-realm disallowed flow
        [InlineData("realm-alpha", "realm-beta", "unauthorized:flow", false)] // Cross-realm unauthorized flow
        public void IsCrossRealmOperationPermitted_EnforcesBridgePolicies(
            string sourceRealm,
            string targetRealm,
            string flow,
            bool expected)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.IsCrossRealmOperationPermitted(sourceRealm, targetRealm, flow);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsCrossRealmOperationPermitted_WithBridgingDisabled_BlocksAllCrossRealm()
        {
            // Arrange - Disable bridging
            _configMock.Setup(x => x.CurrentValue).Returns(new MultiRealmConfig
            {
                Realms = new[]
                {
                    new RealmConfig { Id = "realm-alpha", GovernanceRoots = new[] { "root1" } },
                    new RealmConfig { Id = "realm-beta", GovernanceRoots = new[] { "root2" } }
                },
                Bridge = new BridgeConfig { Enabled = false }
            });

            var service = CreateService();

            // Act
            var result = service.IsCrossRealmOperationPermitted("realm-alpha", "realm-beta", "governance:read");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetCrossRealmGovernanceRoots_ReturnsRootsFromAllRealms()
        {
            // Arrange
            var service = CreateService();
            await service.InitializeAsync();

            // Act
            var roots = service.GetCrossRealmGovernanceRoots();

            // Assert
            Assert.Contains("alpha-root", roots);
            Assert.Contains("beta-root", roots);
            Assert.Equal(2, roots.Count);
        }

        [Fact]
        public void Dispose_CleansUpAllRealmServices()
        {
            // Arrange
            var service = CreateService();

            // Act - Dispose should not throw
            service.Dispose();

            // Assert - Service is disposed, no realm services should be accessible
            Assert.Throws<ObjectDisposedException>(() => service.RealmIds);
        }

        [Fact]
        public async Task GetRealmService_AfterDispose_ThrowsException()
        {
            // Arrange
            var service = CreateService();
            await service.InitializeAsync();
            service.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => service.GetRealmService("realm-alpha"));
        }

        [Fact]
        public void CreateBridgeScopedId_WithNullParameters_ThrowsException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.CreateBridgeScopedId(null!, "identifier"));
            Assert.Throws<ArgumentException>(() => service.CreateBridgeScopedId("realm", null!));
            Assert.Throws<ArgumentException>(() => service.CreateBridgeScopedId("", "identifier"));
            Assert.Throws<ArgumentException>(() => service.CreateBridgeScopedId("realm", ""));
        }

        [Fact]
        public void IsCrossRealmOperationPermitted_WithNullParameters_ThrowsException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.IsCrossRealmOperationPermitted(null!, "target", "flow"));
            Assert.Throws<ArgumentException>(() => service.IsCrossRealmOperationPermitted("source", null!, "flow"));
            Assert.Throws<ArgumentException>(() => service.IsCrossRealmOperationPermitted("source", "target", null!));
        }

        [Fact]
        public async Task InitializeAsync_CanOnlyBeCalledOnce()
        {
            // Arrange
            var service = CreateService();
            await service.InitializeAsync();

            // Act & Assert - Second initialization should not throw
            await service.InitializeAsync(); // Should be idempotent
        }
    }
}

