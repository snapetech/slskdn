// <copyright file="VirtualSoulfindValidationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Core
{
    using slskd.VirtualSoulfind.Core;
    using Xunit;

    /// <summary>
    ///     Tests for H-VF01: VirtualSoulfind Input Validation & Domain Gating.
    /// </summary>
    public class VirtualSoulfindValidationTests
    {
        [Theory]
        [InlineData(ContentDomain.Music, true)]
        [InlineData(ContentDomain.GenericFile, true)]
        public void IsValidContentDomain_WithSupportedDomains_ReturnsTrue(ContentDomain domain, bool expected)
        {
            // Act
            var result = VirtualSoulfindValidation.IsValidContentDomain(domain);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData((ContentDomain)999, false)] // Invalid enum value
        public void IsValidContentDomain_WithUnsupportedDomains_ReturnsFalse(ContentDomain domain, bool expected)
        {
            // Act
            var result = VirtualSoulfindValidation.IsValidContentDomain(domain);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidContentDomain_WithErrorMessage_ReturnsErrorForUnsupportedDomain()
        {
            // Act
            var result = VirtualSoulfindValidation.IsValidContentDomain((ContentDomain)999, out var errorMessage);

            // Assert
            Assert.False(result);
            Assert.NotNull(errorMessage);
            Assert.Contains("not supported", errorMessage);
        }

        [Fact]
        public void GetSupportedDomains_ReturnsExpectedDomains()
        {
            // Act
            var domains = VirtualSoulfindValidation.GetSupportedDomains();

            // Assert
            Assert.Contains(ContentDomain.Music, domains);
            Assert.Contains(ContentDomain.GenericFile, domains);
            Assert.Equal(2, domains.Length);
        }

        [Theory]
        [InlineData(ContentDomain.Music, true)]
        [InlineData(ContentDomain.GenericFile, false)]
        public void CanDomainUseSoulseek_EnforcesDomainRules(ContentDomain domain, bool expected)
        {
            // Act
            var result = VirtualSoulfindValidation.CanDomainUseSoulseek(domain);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(ContentDomain.Music, "track-123", null, null, true, null)]
        [InlineData(ContentDomain.Music, null, null, null, false, "TrackId is required")]
        [InlineData(ContentDomain.GenericFile, null, "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3", 1024, true, null)]
        [InlineData(ContentDomain.GenericFile, null, null, 1024, false, "FileHash is required")]
        [InlineData(ContentDomain.GenericFile, null, "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3", 0, false, "FileSize is required")]
        public void ValidateRequiredFields_EnforcesFieldRequirements(
            ContentDomain domain,
            string trackId,
            string fileHash,
            long? fileSize,
            bool expectedValid,
            string expectedErrorSubstring)
        {
            // Act
            var result = VirtualSoulfindValidation.ValidateRequiredFields(
                domain, trackId, fileHash, fileSize, out var errorMessage);

            // Assert
            Assert.Equal(expectedValid, result);
            if (!expectedValid)
            {
                Assert.Contains(expectedErrorSubstring, errorMessage);
            }
        }

        [Theory]
        [InlineData(ContentDomain.Music, "550e8400-e29b-41d4-a716-446655440000", true, null)] // Valid UUID
        [InlineData(ContentDomain.Music, "not-a-uuid", false, "must be a valid UUID")]
        [InlineData(ContentDomain.Music, "", false, "cannot be null or empty")]
        [InlineData(ContentDomain.GenericFile, "any-string-works", true, null)] // GenericFile allows any format
        public void ValidateTrackIdFormat_EnforcesFormatRules(
            ContentDomain domain,
            string trackId,
            bool expectedValid,
            string expectedErrorSubstring)
        {
            // Act
            var result = VirtualSoulfindValidation.ValidateTrackIdFormat(domain, trackId, out var errorMessage);

            // Assert
            Assert.Equal(expectedValid, result);
            if (!expectedValid)
            {
                Assert.Contains(expectedErrorSubstring, errorMessage);
            }
        }

        [Theory]
        [InlineData("a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3", true, null)] // Valid SHA256
        [InlineData("not-a-hash", false, "must be a valid SHA256 hash")]
        [InlineData("", false, "cannot be null or empty")]
        [InlineData("a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae", false, "64 hexadecimal characters")] // Too short
        public void ValidateFileHashFormat_EnforcesSHA256Rules(
            string fileHash,
            bool expectedValid,
            string expectedErrorSubstring)
        {
            // Act
            var result = VirtualSoulfindValidation.ValidateFileHashFormat(fileHash, out var errorMessage);

            // Assert
            Assert.Equal(expectedValid, result);
            if (!expectedValid)
            {
                Assert.Contains(expectedErrorSubstring, errorMessage);
            }
        }
    }
}


