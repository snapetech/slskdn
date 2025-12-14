// <copyright file="ActivityPubKeyStoreTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-FED01: ActivityPubKeyStore.
    /// </summary>
    public class ActivityPubKeyStoreTests : IDisposable
    {
        private readonly Mock<IDataProtector> _dataProtectorMock = new();
        private readonly Mock<ILogger<ActivityPubKeyStore>> _loggerMock = new();

        public ActivityPubKeyStoreTests()
        {
            // Setup data protector to return input unchanged for testing
            _dataProtectorMock.Setup(x => x.Protect(It.IsAny<string>())).Returns<string>(s => s);
            _dataProtectorMock.Setup(x => x.Unprotect(It.IsAny<string>())).Returns<string>(s => s);
        }

        private ActivityPubKeyStore CreateKeyStore()
        {
            return new ActivityPubKeyStore(_dataProtectorMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task EnsureKeypairAsync_CreatesKeypairForNewActor()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var actorId = "https://example.com/actors/test";

            // Act
            await keyStore.EnsureKeypairAsync(actorId);

            // Assert - Should not throw and keypair should be available
            var publicKey = await keyStore.GetPublicKeyAsync(actorId);
            Assert.NotNull(publicKey);
            Assert.Contains("BEGIN PUBLIC KEY", publicKey);
            Assert.Contains("END PUBLIC KEY", publicKey);
        }

        [Fact]
        public async Task GetPublicKeyAsync_ReturnsPemFormattedKey()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var actorId = "https://example.com/actors/test";
            await keyStore.EnsureKeypairAsync(actorId);

            // Act
            var publicKey = await keyStore.GetPublicKeyAsync(actorId);

            // Assert
            Assert.NotNull(publicKey);
            Assert.True(publicKey.Contains("-----BEGIN PUBLIC KEY-----"));
            Assert.True(publicKey.Contains("-----END PUBLIC KEY-----"));
        }

        [Fact]
        public async Task GetPrivateKeyAsync_ReturnsProtectedKey()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var actorId = "https://example.com/actors/test";
            await keyStore.EnsureKeypairAsync(actorId);

            // Act
            using var privateKey = await keyStore.GetPrivateKeyAsync(actorId);

            // Assert
            Assert.NotNull(privateKey);
            Assert.NotNull(privateKey.PemString);
            Assert.True(privateKey.PemString.Contains("-----BEGIN PRIVATE KEY-----"));
            Assert.True(privateKey.PemString.Contains("-----END PRIVATE KEY-----"));
        }

        [Fact]
        public async Task RotateKeypairAsync_ChangesKeypair()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var actorId = "https://example.com/actors/test";
            await keyStore.EnsureKeypairAsync(actorId);
            var originalPublicKey = await keyStore.GetPublicKeyAsync(actorId);

            // Act
            await keyStore.RotateKeypairAsync(actorId);
            var newPublicKey = await keyStore.GetPublicKeyAsync(actorId);

            // Assert
            Assert.NotEqual(originalPublicKey, newPublicKey);
        }

        [Fact]
        public async Task EnsureKeypairAsync_IdempotentForExistingActor()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var actorId = "https://example.com/actors/test";
            await keyStore.EnsureKeypairAsync(actorId);
            var originalPublicKey = await keyStore.GetPublicKeyAsync(actorId);

            // Act - Call again
            await keyStore.EnsureKeypairAsync(actorId);
            var samePublicKey = await keyStore.GetPublicKeyAsync(actorId);

            // Assert - Should return the same keypair
            Assert.Equal(originalPublicKey, samePublicKey);
        }

        [Fact]
        public async Task GetPublicKeyAsync_ThrowsForUnknownActor()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var unknownActorId = "https://example.com/actors/unknown";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => keyStore.GetPublicKeyAsync(unknownActorId));
        }

        [Fact]
        public async Task GetPrivateKeyAsync_ThrowsForUnknownActor()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var unknownActorId = "https://example.com/actors/unknown";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => keyStore.GetPrivateKeyAsync(unknownActorId));
        }

        [Fact]
        public async Task VerifySignatureAsync_ReturnsFalseForInvalidSignature()
        {
            // Arrange
            var keyStore = CreateKeyStore();
            var actorId = "https://example.com/actors/test";
            await keyStore.EnsureKeypairAsync(actorId);

            // Act
            var result = await keyStore.VerifySignatureAsync(actorId, "invalid", "test");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var keyStore = CreateKeyStore();

            // Act - Dispose should not throw
            keyStore.Dispose();

            // Assert - Should be disposable without issues
            Assert.True(true); // If we get here, Dispose worked
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
