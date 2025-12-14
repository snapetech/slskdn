// <copyright file="IdentitySeparationEnforcerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security
{
    using slskd.Common.Security;
    using Xunit;

    /// <summary>
    ///     Tests for H-ID01: IdentitySeparationEnforcer implementation.
    /// </summary>
    public class IdentitySeparationEnforcerTests
    {
        [Theory]
        [InlineData("abc123def456", IdentitySeparationEnforcer.IdentityType.Mesh)]
        [InlineData("SGVsbG8gV29ybGQ=", IdentitySeparationEnforcer.IdentityType.Mesh)] // base64
        [InlineData("user123", IdentitySeparationEnforcer.IdentityType.Soulseek)]
        [InlineData("user_name.123", IdentitySeparationEnforcer.IdentityType.Soulseek)]
        [InlineData("pod:abc123def4567890", IdentitySeparationEnforcer.IdentityType.Pod)]
        [InlineData("mesh:self", IdentitySeparationEnforcer.IdentityType.Pod)]
        [InlineData("admin", IdentitySeparationEnforcer.IdentityType.LocalUser)]
        [InlineData("user@example.com", IdentitySeparationEnforcer.IdentityType.LocalUser)]
        [InlineData("@user@domain.com", IdentitySeparationEnforcer.IdentityType.ActivityPub)]
        public void IsValidIdentityFormat_ValidIdentities_ReturnsTrue(string identity, IdentitySeparationEnforcer.IdentityType type)
        {
            // Act
            var result = IdentitySeparationEnforcer.IsValidIdentityFormat(identity, type);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("", IdentitySeparationEnforcer.IdentityType.Mesh)]
        [InlineData(null, IdentitySeparationEnforcer.IdentityType.Mesh)]
        [InlineData("user@domain.com", IdentitySeparationEnforcer.IdentityType.Soulseek)] // invalid chars
        [InlineData("bridge:user123", IdentitySeparationEnforcer.IdentityType.Pod)] // bridge format not allowed
        [InlineData("user@domain.com", IdentitySeparationEnforcer.IdentityType.Soulseek)] // @ not allowed
        public void IsValidIdentityFormat_InvalidIdentities_ReturnsFalse(string identity, IdentitySeparationEnforcer.IdentityType type)
        {
            // Act
            var result = IdentitySeparationEnforcer.IsValidIdentityFormat(identity, type);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasCrossContamination_BridgeIdentityWithSoulseekForbidden_ReturnsTrue()
        {
            // Arrange
            var bridgeIdentity = "bridge:user123";

            // Act
            var result = IdentitySeparationEnforcer.HasCrossContamination(
                bridgeIdentity,
                IdentitySeparationEnforcer.IdentityType.Soulseek);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasCrossContamination_ValidPodIdentity_ReturnsFalse()
        {
            // Arrange
            var podIdentity = "pod:abc123def4567890";

            // Act
            var result = IdentitySeparationEnforcer.HasCrossContamination(
                podIdentity,
                IdentitySeparationEnforcer.IdentityType.Soulseek,
                IdentitySeparationEnforcer.IdentityType.Mesh);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void SanitizePodPeerId_BridgeIdentity_ReturnsSanitizedFormat()
        {
            // Arrange
            var bridgeIdentity = "bridge:user123";

            // Act
            var result = IdentitySeparationEnforcer.SanitizePodPeerId(bridgeIdentity);

            // Assert
            Assert.NotEqual(bridgeIdentity, result);
            Assert.StartsWith("pod:", result);
            Assert.Equal(20, result.Length); // "pod:" + 16 hex chars
        }

        [Fact]
        public void SanitizePodPeerId_AlreadySafeIdentity_ReturnsUnchanged()
        {
            // Arrange
            var safeIdentity = "pod:abc123def4567890";

            // Act
            var result = IdentitySeparationEnforcer.SanitizePodPeerId(safeIdentity);

            // Assert
            Assert.Equal(safeIdentity, result);
        }

        [Theory]
        [InlineData("pod:abc123def4567890", true)]
        [InlineData("mesh:self", true)]
        [InlineData("bridge:user123", false)] // Leaks Soulseek identity
        [InlineData("user@domain.com", false)] // Email-like
        [InlineData("user/domain", false)] // URL-like
        [InlineData("user\\domain", false)] // Windows path-like
        public void IsSafePodPeerId_VariousIdentities_ReturnsExpectedResult(string identity, bool expected)
        {
            // Act
            var result = IdentitySeparationEnforcer.IsSafePodPeerId(identity);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("abc123def456", IdentitySeparationEnforcer.IdentityType.Mesh)]
        [InlineData("user123", IdentitySeparationEnforcer.IdentityType.Soulseek)]
        [InlineData("pod:abc123def4567890", IdentitySeparationEnforcer.IdentityType.Pod)]
        [InlineData("user@example.com", IdentitySeparationEnforcer.IdentityType.LocalUser)]
        [InlineData("@user@domain.com", IdentitySeparationEnforcer.IdentityType.ActivityPub)]
        [InlineData("unknown-format", null)]
        public void DetectIdentityType_VariousIdentities_ReturnsCorrectType(string identity, IdentitySeparationEnforcer.IdentityType? expected)
        {
            // Act
            var result = IdentitySeparationEnforcer.DetectIdentityType(identity);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
