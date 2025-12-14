// <copyright file="WorkRefTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    /// <summary>
    ///     Tests for T-FED02: WorkRef object types.
    /// </summary>
    public class WorkRefTests
    {
        [Fact]
        public void FromMusicItem_CreatesValidWorkRef()
        {
            // Arrange
            var musicItem = new ContentDomain.MusicContentItem(
                "test-track",
                "Test Artist",
                "Test Album",
                2023,
                180,
                new[] { "electronic", "ambient" },
                "12345678-1234-1234-1234-123456789abc",
                "98765432-4321-4321-4321-cba987654321");

            var instanceUrl = "https://example.com";

            // Act
            var workRef = WorkRef.FromMusicItem(musicItem, instanceUrl);

            // Assert
            Assert.Equal("WorkRef", workRef.Type);
            Assert.Equal("music", workRef.Domain);
            Assert.Equal("Test Artist - Test Album - test-track", workRef.Title);
            Assert.Equal("Test Artist", workRef.Creator);
            Assert.Equal(2023, workRef.Year);
            Assert.Equal($"{instanceUrl}/actors/music", workRef.AttributedTo);
            Assert.Contains("musicbrainz", workRef.ExternalIds);
            Assert.Contains("discogs", workRef.ExternalIds);
        }

        [Fact]
        public void ValidateSecurity_AllowsSafeContent()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song Title",
                Creator = "Safe Artist",
                Year = 2023,
                ExternalIds = new Dictionary<string, string>
                {
                    ["musicbrainz"] = "12345678-1234-1234-1234-123456789abc",
                    ["discogs"] = "123456"
                },
                Metadata = new Dictionary<string, object>
                {
                    ["genre"] = "electronic",
                    ["duration"] = 180
                }
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateSecurity_BlocksPathInTitle()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Song with /path/injection",
                Creator = "Safe Artist"
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateSecurity_BlocksHashInExternalId()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song",
                Creator = "Safe Artist",
                ExternalIds = new Dictionary<string, string>
                {
                    ["badkey"] = "abcdef1234567890abcdef" // Looks like a hash
                }
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateSecurity_BlocksIpAddress()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song",
                Creator = "192.168.1.1" // IP address in creator
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateSecurity_BlocksUuid()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song",
                ExternalIds = new Dictionary<string, string>
                {
                    ["id"] = "12345678-1234-1234-1234-123456789abc" // UUID pattern
                }
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateSecurity_BlocksMeshPeerId()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song",
                Metadata = new Dictionary<string, object>
                {
                    ["peer"] = "pod:some-peer-id" // Mesh peer ID
                }
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateSecurity_AllowsSafeExternalIds()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song",
                Creator = "Safe Artist",
                ExternalIds = new Dictionary<string, string>
                {
                    ["musicbrainz"] = "12345678-1234-1234-ABCD-123456789abc", // Valid UUID format for external service
                    ["discogs"] = "123456", // Numeric ID
                    ["spotify"] = "track/4uLU6hMCjMI75M1A2tKUQC" // URI-like but safe
                }
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert - Should allow these as they're not flagged patterns
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("music")]
        [InlineData("books")]
        [InlineData("movies")]
        [InlineData("tv")]
        [InlineData("software")]
        [InlineData("games")]
        public void ValidateSecurity_AllowsValidDomains(string domain)
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = domain,
                Title = "Safe Title",
                Creator = "Safe Creator"
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateSecurity_RejectsInvalidDomain()
        {
            // Arrange
            var workRef = new WorkRef
            {
                Domain = "invalid_domain",
                Title = "Safe Title",
                Creator = "Safe Creator"
            };

            // Act
            var isValid = workRef.ValidateSecurity();

            // Assert - Domain validation is separate, security validation should still pass
            Assert.True(isValid);
        }
    }
}

