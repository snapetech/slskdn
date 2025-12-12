// <copyright file="SimpleMatchEngineTests.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Matching
{
    using System;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Matching;
    using Xunit;

    /// <summary>
    ///     Tests for T-V2-P3-01: Match & Verification Engine.
    /// </summary>
    public class SimpleMatchEngineTests
    {
        [Fact]
        public async Task Match_MBID_And_Duration_ReturnsStrong()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Comfortably Numb",
                DurationSeconds = 382,
                MusicBrainzRecordingId = "mbid-recording-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "05 - Comfortably Numb.flac",
                Extension = ".flac",
                Size = 35_000_000,
                DurationSeconds = 384, // Within tolerance
                Embedded = new EmbeddedMetadata
                {
                    Title = "Comfortably Numb",
                    MusicBrainzRecordingId = "mbid-recording-123",
                },
            };

            // Act
            var result = await engine.MatchAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.Strong, result.Confidence);
            Assert.True(result.IsStrong);
            Assert.Contains("MBID", result.Reason);
        }

        [Fact]
        public async Task Match_TitleAndDuration_ReturnsMedium()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Wish You Were Here",
                DurationSeconds = 334,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "Wish You Were Here.mp3",
                Extension = ".mp3",
                Size = 8_000_000,
                DurationSeconds = 336,
                Embedded = new EmbeddedMetadata
                {
                    Title = "Wish You Were Here",
                },
            };

            // Act
            var result = await engine.MatchAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.Medium, result.Confidence);
            Assert.True(result.IsUsable);
            Assert.Contains("Title", result.Reason);
        }

        [Fact]
        public async Task Match_FilenameOnly_ReturnsWeak()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Shine On You Crazy Diamond",
                DurationSeconds = 810,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "01_Shine_On_You_Crazy_Diamond_Parts_1-5.flac",
                Extension = ".flac",
                Size = 65_000_000,
                // No embedded metadata, no duration
            };

            // Act
            var result = await engine.MatchAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.Weak, result.Confidence);
            Assert.False(result.IsUsable); // Weak is NOT usable (requires user confirmation)
            Assert.False(result.IsStrong);
        }

        [Fact]
        public async Task Match_DurationMismatch_ReturnsNone()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Time",
                DurationSeconds = 414,
                MusicBrainzRecordingId = "mbid-time-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "Time.mp3",
                Extension = ".mp3",
                Size = 10_000_000,
                DurationSeconds = 250, // Way too short
                Embedded = new EmbeddedMetadata
                {
                    Title = "Time",
                    MusicBrainzRecordingId = "mbid-time-123",
                },
            };

            // Act
            var result = await engine.MatchAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.None, result.Confidence);
            Assert.False(result.IsUsable);
        }

        [Fact]
        public async Task Match_NoMetadata_ReturnsNone()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Money",
                DurationSeconds = 382,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "unknown_file.mp3",
                Extension = ".mp3",
                Size = 5_000_000,
                // No duration, no embedded metadata
            };

            // Act
            var result = await engine.MatchAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.None, result.Confidence);
            Assert.False(result.IsUsable);
        }

        [Fact]
        public async Task Verify_StrongMatch_Succeeds()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Us and Them",
                DurationSeconds = 460,
                MusicBrainzRecordingId = "mbid-usandthem-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "Us and Them.flac",
                Extension = ".flac",
                Size = 40_000_000,
                DurationSeconds = 462,
                Embedded = new EmbeddedMetadata
                {
                    Title = "Us and Them",
                    MusicBrainzRecordingId = "mbid-usandthem-123",
                },
            };

            // Act
            var result = await engine.VerifyAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.Strong, result.Confidence);
            Assert.True(result.IsStrong);
        }

        [Fact]
        public async Task Verify_MediumMatch_Fails()
        {
            // Arrange
            var engine = new SimpleMatchEngine();
            var track = new Track
            {
                TrackId = "track:1",
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Brain Damage",
                DurationSeconds = 228,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var candidate = new CandidateFileMetadata
            {
                Filename = "Brain Damage.mp3",
                Extension = ".mp3",
                Size = 5_500_000,
                DurationSeconds = 230,
                Embedded = new EmbeddedMetadata
                {
                    Title = "Brain Damage",
                    // No MBID = only Medium confidence
                },
            };

            // Act
            var result = await engine.VerifyAsync(track, candidate);

            // Assert
            Assert.Equal(MatchConfidence.None, result.Confidence);
            Assert.False(result.IsStrong);
            Assert.Contains("Verification failed", result.Reason);
        }
    }
}
