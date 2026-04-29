// <copyright file="LibraryReconciliationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
    public class LibraryReconciliationServiceTests
    {
        [Fact]
        public async Task FindMissingTracksForRelease_EmptyRelease_ReturnsEmpty()
        {
            // Arrange
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);
            var releaseId = Guid.NewGuid().ToString();

            // Act
            var missing = await service.FindMissingTracksForReleaseAsync(releaseId);

            // Assert
            Assert.Empty(missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_AllTracksHaveLocalFiles_ReturnsEmpty()
        {
            // Arrange
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);
            var (release, tracks) = await CreateTestReleaseWithTracks(catalogue, 3);

            // Add local files for all tracks
            foreach (var track in tracks)
            {
                var localFile = CreateLocalFile(track.TrackId);
                await catalogue.UpsertLocalFileAsync(localFile);
            }

            // Act
            var missing = await service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Empty(missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_SomeTracksMissing_ReturnsOnlyMissing()
        {
            // Arrange
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);
            var (release, tracks) = await CreateTestReleaseWithTracks(catalogue, 5);

            // Add local files for tracks 0, 1, 2 only
            for (int i = 0; i < 3; i++)
            {
                var localFile = CreateLocalFile(tracks[i].TrackId);
                await catalogue.UpsertLocalFileAsync(localFile);
            }

            // Act
            var missing = await service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Equal(2, missing.Count);
            Assert.Contains(tracks[3].TrackId, missing);
            Assert.Contains(tracks[4].TrackId, missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_TracksWithVerifiedCopiesNotMissing()
        {
            // Arrange
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);
            var (release, tracks) = await CreateTestReleaseWithTracks(catalogue, 3);

            // Add verified copy for track 0
            var localFile = CreateLocalFile(tracks[0].TrackId);
            await catalogue.UpsertLocalFileAsync(localFile);

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
            await catalogue.UpsertVerifiedCopyAsync(verifiedCopy);

            // Act
            var missing = await service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Equal(2, missing.Count);
            Assert.DoesNotContain(tracks[0].TrackId, missing);
        }

        [Fact]
        public async Task FindMissingTracksForRelease_AllTracksMissing_ReturnsAll()
        {
            // Arrange
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);
            var (release, tracks) = await CreateTestReleaseWithTracks(catalogue, 4);

            // Act
            var missing = await service.FindMissingTracksForReleaseAsync(release.ReleaseId);

            // Assert
            Assert.Equal(4, missing.Count);
            Assert.All(tracks, track => Assert.Contains(track.TrackId, missing));
        }

        [Fact]
        public async Task FindTracksWithoutLocalCopies_NoTracks_ReturnsEmpty()
        {
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);

            // Act
            var result = await service.FindTracksWithoutLocalCopiesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task FindOrphanedLocalFiles_NoFiles_ReturnsEmpty()
        {
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);

            // Act
            var result = await service.FindOrphanedLocalFilesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task AnalyzeAllReleases_NoReleases_ReturnsEmpty()
        {
            using var catalogue = new InMemoryCatalogueStore();
            var service = new LibraryReconciliationService(catalogue);

            // Act
            var result = await service.AnalyzeAllReleasesAsync();

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

        private static async Task<(Release release, Track[] tracks)> CreateTestReleaseWithTracks(
            InMemoryCatalogueStore catalogue,
            int trackCount)
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
            await catalogue.UpsertArtistAsync(artist);

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
            await catalogue.UpsertReleaseGroupAsync(releaseGroup);

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
            await catalogue.UpsertReleaseAsync(release);

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
                await catalogue.UpsertTrackAsync(tracks[i]);
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
