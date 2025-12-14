// <copyright file="RealmAwareGovernanceClientTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Governance
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Mesh.Realm;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-03: RealmAwareGovernanceClient.
    /// </summary>
    public class RealmAwareGovernanceClientTests
    {
        private readonly Mock<IRealmService> _realmServiceMock = new();
        private readonly Mock<ILogger<RealmAwareGovernanceClient>> _loggerMock = new();

        public RealmAwareGovernanceClientTests()
        {
            // Setup default realm service
            _realmServiceMock.Setup(x => x.IsSameRealm("test-realm")).Returns(true);
            _realmServiceMock.Setup(x => x.IsTrustedGovernanceRoot("trusted-signer")).Returns(true);
        }

        private RealmAwareGovernanceClient CreateClient()
        {
            return new RealmAwareGovernanceClient(_realmServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ValidateDocumentForRealmAsync_WithValidDocument_ReturnsTrue()
        {
            // Arrange
            var client = CreateClient();
            var document = new GovernanceDocument
            {
                Id = "test-doc",
                Type = "policy",
                Version = 1,
                RealmId = "test-realm",
                Signer = "trusted-signer",
                Signature = "valid-signature" // This would be computed properly in real implementation
            };

            // Act
            var result = await client.ValidateDocumentForRealmAsync(document, "test-realm");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateDocumentForRealmAsync_WithWrongRealm_ReturnsFalse()
        {
            // Arrange
            var client = CreateClient();
            var document = new GovernanceDocument
            {
                Id = "test-doc",
                Type = "policy",
                Version = 1,
                RealmId = "wrong-realm",
                Signer = "trusted-signer"
            };

            // Act
            var result = await client.ValidateDocumentForRealmAsync(document, "test-realm");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateDocumentForRealmAsync_WithUntrustedSigner_ReturnsFalse()
        {
            // Arrange
            _realmServiceMock.Setup(x => x.IsTrustedGovernanceRoot("untrusted-signer")).Returns(false);
            var client = CreateClient();
            var document = new GovernanceDocument
            {
                Id = "test-doc",
                Type = "policy",
                Version = 1,
                RealmId = "test-realm",
                Signer = "untrusted-signer"
            };

            // Act
            var result = await client.ValidateDocumentForRealmAsync(document, "test-realm");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateDocumentForRealmAsync_WithInvalidDocument_ReturnsFalse()
        {
            // Arrange
            var client = CreateClient();
            var document = new GovernanceDocument
            {
                Id = string.Empty, // Invalid
                Type = "policy",
                Version = 1,
                RealmId = "test-realm",
                Signer = "trusted-signer"
            };

            // Act
            var result = await client.ValidateDocumentForRealmAsync(document, "test-realm");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StoreDocumentForRealmAsync_WithValidDocument_StoresSuccessfully()
        {
            // Arrange
            var client = CreateClient();
            var document = new GovernanceDocument
            {
                Id = "test-doc",
                Type = "policy",
                Version = 1,
                RealmId = "test-realm",
                Signer = "trusted-signer"
            };

            // Act
            await client.StoreDocumentForRealmAsync(document, "test-realm");

            // Assert - Document should be retrievable
            var retrieved = await client.GetDocumentAsync("test-doc");
            Assert.NotNull(retrieved);
            Assert.Equal("test-doc", retrieved.Id);
            Assert.Equal("test-realm", retrieved.RealmId);
        }

        [Fact]
        public async Task GetDocumentsForRealmAsync_ReturnsRealmDocuments()
        {
            // Arrange
            var client = CreateClient();

            var doc1 = new GovernanceDocument { Id = "doc1", RealmId = "test-realm" };
            var doc2 = new GovernanceDocument { Id = "doc2", RealmId = "test-realm" };
            var doc3 = new GovernanceDocument { Id = "doc3", RealmId = "other-realm" };

            await client.StoreDocumentForRealmAsync(doc1, "test-realm");
            await client.StoreDocumentForRealmAsync(doc2, "test-realm");
            await client.StoreDocumentForRealmAsync(doc3, "other-realm");

            // Act
            var realmDocuments = await client.GetDocumentsForRealmAsync("test-realm");

            // Assert
            Assert.Equal(2, realmDocuments.Count);
            Assert.Contains(realmDocuments, d => d.Id == "doc1");
            Assert.Contains(realmDocuments, d => d.Id == "doc2");
            Assert.DoesNotContain(realmDocuments, d => d.Id == "doc3");
        }

        [Fact]
        public async Task StoreDocumentForRealmAsync_AutomaticallySetsRealmId()
        {
            // Arrange
            var client = CreateClient();
            var document = new GovernanceDocument
            {
                Id = "test-doc",
                Type = "policy",
                Version = 1,
                RealmId = null, // Not set initially
                Signer = "trusted-signer"
            };

            // Act
            await client.StoreDocumentForRealmAsync(document, "assigned-realm");

            // Assert
            var retrieved = await client.GetDocumentAsync("test-doc");
            Assert.NotNull(retrieved);
            Assert.Equal("assigned-realm", retrieved.RealmId);
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var client = CreateClient();

            // Act - Dispose should not throw
            client.Dispose();

            // Assert - Can dispose multiple times without error
            client.Dispose();
        }
    }
}

