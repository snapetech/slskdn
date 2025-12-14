// <copyright file="IdentitySeparationValidatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security
{
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Security;
    using Xunit;

    /// <summary>
    ///     Tests for H-ID01: IdentitySeparationValidator implementation.
    /// </summary>
    public class IdentitySeparationValidatorTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public IdentitySeparationValidatorTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public void ValidateIdentities_NoViolations_ReturnsValidResult()
        {
            // Arrange
            var identities = new Dictionary<string, string>
            {
                ["mesh"] = "abc123def456",
                ["soulseek"] = "user123",
                ["pod"] = "pod:abc123def4567890",
                ["localuser"] = "admin"
            };

            // Act
            var result = IdentitySeparationValidator.ValidateIdentities(identities, _loggerMock.Object);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void ValidateIdentities_WithCrossContamination_ReturnsInvalidResult()
        {
            // Arrange
            var identities = new Dictionary<string, string>
            {
                ["pod"] = "bridge:user123", // This leaks Soulseek identity into pod context
                ["soulseek"] = "user123"
            };

            // Act
            var result = IdentitySeparationValidator.ValidateIdentities(identities, _loggerMock.Object);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Violations);
            Assert.Equal("pod", result.Violations[0].Context);
            Assert.Equal("bridge:user123", result.Violations[0].Identity);
        }

        [Fact]
        public void AuditPodPeerIds_NoUnsafeIds_ReturnsCleanResult()
        {
            // Arrange
            var peerIds = new[] { "pod:abc123def4567890", "mesh:self", "pod:fed456cba0987654" };

            // Act
            var result = IdentitySeparationValidator.AuditPodPeerIds(peerIds, _loggerMock.Object);

            // Assert
            Assert.Equal(3, result.TotalAudited);
            Assert.Equal(0, result.UnsafeCount);
            Assert.Empty(result.UnsafeIds);
        }

        [Fact]
        public void AuditPodPeerIds_WithUnsafeIds_ReturnsViolations()
        {
            // Arrange
            var peerIds = new[]
            {
                "pod:abc123def4567890", // Safe
                "bridge:user123",       // Unsafe - leaks Soulseek identity
                "user@domain.com",      // Unsafe - email-like
                "pod:fed456cba0987654"  // Safe
            };

            // Act
            var result = IdentitySeparationValidator.AuditPodPeerIds(peerIds, _loggerMock.Object);

            // Assert
            Assert.Equal(4, result.TotalAudited);
            Assert.Equal(2, result.UnsafeCount);
            Assert.Contains("bridge:user123", result.UnsafeIds);
            Assert.Contains("user@domain.com", result.UnsafeIds);
            Assert.Equal(2, result.ViolationStats["Soulseek"]);
        }

        [Fact]
        public void AuditPodPeerIds_WithEmptyList_ReturnsEmptyResult()
        {
            // Arrange
            var peerIds = System.Array.Empty<string>();

            // Act
            var result = IdentitySeparationValidator.AuditPodPeerIds(peerIds, _loggerMock.Object);

            // Assert
            Assert.Equal(0, result.TotalAudited);
            Assert.Equal(0, result.UnsafeCount);
            Assert.Empty(result.UnsafeIds);
        }
    }
}

