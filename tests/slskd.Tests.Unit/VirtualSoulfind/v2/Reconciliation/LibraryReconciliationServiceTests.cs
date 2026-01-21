// <copyright file="LibraryReconciliationServiceTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Reconciliation
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Reconciliation;
    using Xunit;

    /// <summary>
    ///     Tests for <see cref="LibraryReconciliationService"/>.
    /// </summary>
    public class LibraryReconciliationServiceTests : IDisposable
    {
        private readonly InMemoryCatalogueStore _catalogue;
        private readonly LibraryReconciliationService _service;

        public LibraryReconciliationServiceTests()
        {
            _catalogue = new InMemoryCatalogueStore();
            _service = new LibraryReconciliationService(_catalogue);
        }

        public void Dispose()
        {
            _catalogue.Dispose();
        }

        [Fact]
        public async Task FindMissingTracksForRelease_EmptyRelease_ReturnsEmpty()
        {
            // Arrange
            var releaseId = Guid.NewGuid().ToString();

            // Act
            var missing = await _service.FindMissingTracksForReleaseAsync(releaseId);

            // Assert
            Assert.Empty(missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_AllTracksHaveLocalFiles_ReturnsEmpty()
        {
            // Arrange
            var (release, tracks) = await CreateTestReleaseWithTracks(3);

            // Add local files for all tracks
            foreach (var track in tracks)
            {
                var localFile = CreateLocalFile(track.TrackId);
                await _catalogue.UpsertLocalFileAsync(localFile);
            }

            // Act
            var missing = await _service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Empty(missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_SomeTracksMissing_ReturnsOnlyMissing()
        {
            // Arrange
            var (release, tracks) = await CreateTestReleaseWithTracks(5);

            // Add local files for tracks 0, 1, 2 only
            for (int i = 0; i < 3; i++)
            {
                var localFile = CreateLocalFile(tracks[i].TrackId);
                await _catalogue.UpsertLocalFileAsync(localFile);
            }

            // Act
            var missing = await _service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Equal(2, missing.Count);
            Assert.Contains(tracks[3].TrackId, missing);
            Assert.Contains(tracks[4].TrackId, missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_TracksWithVerifiedCopiesNotMissing()
        {
            // Arrange
            var (release, tracks) = await CreateTestReleaseWithTracks(3);

            // Add verified copy for track 0
            var localFile = CreateLocalFile(tracks[0].TrackId);
            await _catalogue.UpsertLocalFileAsync(localFile);

            var verifiedCopy = new VerifiedCopy
            {
                VerifiedCopyId = Guid.NewGuid().ToString(),
                TrackId = tracks[0].TrackId,
                LocalFileId = localFile.LocalFileId,
                HashPrimary = localFile.HashPrimary,
                DurationSeconds = localFile.DurationSeconds,
                VerificationSource = VerificationSource.Manual,
                VerifiedAt = DateTimeOffset.UtcNow,
            };
            await _catalogue.UpsertVerifiedCopyAsync(verifiedCopy);

            // Act
            var missing = await _service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Equal(2, missing.Count);
            Assert.DoesNotContain(tracks[0].TrackId, missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_AllTracksMissing_ReturnsAll()
        {
            // Arrange
            var (release, tracks) = await CreateTestReleaseWithTracks(4);

            // Act
            var missing = await _service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Equal(4, missing.Count);
            Assert.All(tracks, track => Assert.Contains(track.TrackId, missing));
        }

        [Fact]
        public async Task FindTracksWithoutLocalCopies_NoTracks_ReturnsEmpty()
        {
            // Act
            var result = await _service.FindTracksWithoutLocalCopiesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task FindOrphanedLocalFiles_NoFiles_ReturnsEmpty()
        {
            // Act
            var result = await _service.FindOrphanedLocalFilesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task AnalyzeAllReleases_NoReleases_ReturnsEmpty()
        {
            // Act
            var result = await _service.AnalyzeAllReleasesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ReleaseGapAnalysis_CompletionPercentage_CalculatesCorrectly()
        {
            // Arrange
            var analysis = new ReleaseGapAnalysis
            {
                ReleaseId = "test",
                ReleaseTitle = "Test Album",
                ArtistName = "Test Artist",
                TotalTracks = 10,
                TracksWithLocalCopies = 7,
                TracksWithVerifiedCopies = 5,
                MissingTrackIds = new[] { "1", "2", "3" },
            };

            // Act
            var percentage = analysis.CompletionPercentage;

            // Assert
            Assert.Equal(0.7f, percentage);
        }

        [Fact]
        public async Task ReleaseGapAnalysis_IsPartial_TrueWhenSomeTracksPresent()
        {
            // Arrange
            var analysis = new ReleaseGapAnalysis
            {
                ReleaseId = "test",
                ReleaseTitle = "Test Album",
                ArtistName = "Test Artist",
                TotalTracks = 10,
                TracksWithLocalCopies = 7,
                TracksWithVerifiedCopies = 5,
                MissingTrackIds = new[] { "1", "2", "3" },
            };

            // Act & Assert
            Assert.True(analysis.IsPartial);
        }

        [Fact]
        public async Task ReleaseGapAnalysis_IsPartial_FalseWhenComplete()
        {
            // Arrange
            var analysis = new ReleaseGapAnalysis
            {
                ReleaseId = "test",
                ReleaseTitle = "Test Album",
                ArtistName = "Test Artist",
                TotalTracks = 10,
                TracksWithLocalCopies = 10,
                TracksWithVerifiedCopies = 10,
                MissingTrackIds = Array.Empty<string>(),
            };

            // Act & Assert
            Assert.False(analysis.IsPartial);
        }

        [Fact]
        public async Task ReleaseGapAnalysis_IsPartial_FalseWhenEmpty()
        {
            // Arrange
            var analysis = new ReleaseGapAnalysis
            {
                ReleaseId = "test",
                ReleaseTitle = "Test Album",
                ArtistName = "Test Artist",
                TotalTracks = 10,
                TracksWithLocalCopies = 0,
                TracksWithVerifiedCopies = 0,
                MissingTrackIds = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" },
            };

            // Act & Assert
            Assert.False(analysis.IsPartial);
        }

        [Fact]
        public async Task UpgradeSuggestion_HasAllRequiredProperties()
        {
            // Arrange
            var suggestion = new UpgradeSuggestion
            {
                TrackId = "track1",
                TrackTitle = "Test Track",
                LocalFileId = "file1",
                CurrentQuality = 0.6f,
                TargetQuality = "FLAC",
                QualityImprovement = 0.4f,
                CurrentCodec = "MP3",
                CurrentBitrate = 128,
            };

            // Act & Assert
            Assert.Equal("track1", suggestion.TrackId);
            Assert.Equal("Test Track", suggestion.TrackTitle);
            Assert.Equal(0.6f, suggestion.CurrentQuality);
            Assert.Equal("FLAC", suggestion.TargetQuality);
            Assert.Equal(0.4f, suggestion.QualityImprovement);
            Assert.Equal("MP3", suggestion.CurrentCodec);
            Assert.Equal(128, suggestion.CurrentBitrate);
        }

        // Helper methods

        private async Task<(Release release, Track[] tracks)> CreateTestReleaseWithTracks(int trackCount)
        {
            // Create artist
            var artist = new Artist
            {
                ArtistId = Guid.NewGuid().ToString(),
                MusicBrainzId = null,
                Name = "Test Artist",
                SortName = "Test Artist",
                Tags = null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _catalogue.UpsertArtistAsync(artist);

            // Create release group
            var releaseGroup = new ReleaseGroup
            {
                ReleaseGroupId = Guid.NewGuid().ToString(),
                MusicBrainzId = null,
                ArtistId = artist.ArtistId,
                Title = "Test Album",
                PrimaryType = ReleaseGroupPrimaryType.Album,
                Year = 2024,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _catalogue.UpsertReleaseGroupAsync(releaseGroup);

            // Create release
            var release = new Release
            {
                ReleaseId = Guid.NewGuid().ToString(),
                MusicBrainzId = null,
                ReleaseGroupId = releaseGroup.ReleaseGroupId,
                Title = "Test Album",
                Year = 2024,
                Country = "US",
                Label = "Test Label",
                CatalogNumber = "TEST001",
                MediaCount = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _catalogue.UpsertReleaseAsync(release);

            // Create tracks
            var tracks = new Track[trackCount];
            for (int i = 0; i < trackCount; i++)
            {
                tracks[i] = new Track
                {
                    TrackId = Guid.NewGuid().ToString(),
                    MusicBrainzRecordingId = null,
                    ReleaseId = release.ReleaseId,
                    DiscNumber = 1,
                    TrackNumber = i + 1,
                    Title = $"Track {i + 1}",
                    DurationSeconds = 180,
                    Isrc = null,
                    Tags = null,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                await _catalogue.UpsertTrackAsync(tracks[i]);
            }

            return (release, tracks);
        }

        private LocalFile CreateLocalFile(string? inferredTrackId = null)
        {
            return new LocalFile
            {
                LocalFileId = Guid.NewGuid().ToString(),
                Path = $"/music/test/{Guid.NewGuid()}.flac",
                SizeBytes = 25_000_000,
                DurationSeconds = 180,
                Codec = "FLAC",
                Bitrate = 1411,
                Channels = 2,
                HashPrimary = Guid.NewGuid().ToString("N"),
                HashSecondary = Guid.NewGuid().ToString("N"),
                AudioFingerprintId = null,
                InferredTrackId = inferredTrackId,
                AddedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
