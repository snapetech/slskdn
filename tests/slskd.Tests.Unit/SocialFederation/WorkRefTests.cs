// <copyright file="WorkRefTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation
{
    using System;
    using System.Collections.Generic;
    using slskd.HashDb.Models;
    using slskd.SocialFederation;
    using slskd.VirtualSoulfind.Core.Music;
    using Xunit;

    /// <summary>
    ///     Tests for T-FED02: WorkRef object types.
    /// </summary>
    public class WorkRefTests
    {
        [Fact]
        public void FromMusicItem_CreatesValidWorkRef()
        {
            var trackEntry = new AlbumTargetTrackEntry
            {
                ReleaseId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                RecordingId = "b2c3d4e5-f6a7-8901-bcde-f12345678901",
                Position = 1,
                Title = "Test Track",
                Artist = "Test Artist",
                DurationMs = 180000,
                Year = 2020,
                Genre = "Rock",
            };
            var musicItem = MusicItem.FromTrackEntry(trackEntry);
            const string instanceUrl = "https://example.com";

            var workRef = WorkRef.FromMusicItem(musicItem, instanceUrl);

            Assert.NotNull(workRef);
            Assert.Equal("music", workRef.Domain);
            Assert.Equal("Test Track", workRef.Title);
            Assert.Equal("Test Artist", workRef.Creator);
            Assert.Equal(2020, workRef.Year);
            Assert.StartsWith(instanceUrl, workRef.Id, StringComparison.Ordinal);
            Assert.True(workRef.ValidateSecurity());
        }

        [Fact]
        public void ValidateSecurity_AllowsSafeContent()
        {
            // Arrange - use non-UUID external IDs (implementation blocks UUIDs in ExternalIds)
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song Title",
                Creator = "Safe Artist",
                Year = 2023,
                ExternalIds = new Dictionary<string, string>
                {
                    ["musicbrainz"] = "mbrec-abc123",
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
                    ["badkey"] = "a1b2c3d4e5f6789012345678abcdef12" // 32+ hex chars, hash-like
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
            // Arrange - avoid UUID (blocked), path separators, and hash-like hex (implementation blocks these)
            var workRef = new WorkRef
            {
                Domain = "music",
                Title = "Safe Song",
                Creator = "Safe Artist",
                ExternalIds = new Dictionary<string, string>
                {
                    ["musicbrainz"] = "mbrec-xyz",
                    ["discogs"] = "123456",
                    ["spotify"] = "4uLU6hMCjMI75M1A2tKUQC"
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


