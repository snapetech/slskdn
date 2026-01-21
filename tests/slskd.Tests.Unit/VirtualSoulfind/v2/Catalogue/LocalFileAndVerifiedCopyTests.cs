// <copyright file="LocalFileAndVerifiedCopyTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Catalogue
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using Xunit;

    /// <summary>
    ///     Tests for <see cref="LocalFile"/> and <see cref="VerifiedCopy"/> entities and store operations.
    /// </summary>
    public class LocalFileAndVerifiedCopyTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly SqliteCatalogueStore _store;

        public LocalFileAndVerifiedCopyTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_catalogue_{Guid.NewGuid()}.db");
            _store = new SqliteCatalogueStore(_tempDbPath);
        }

        public void Dispose()
        {
            _store.Dispose();
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }

        // ========== LocalFile Tests ==========

        [Fact]
        public async Task LocalFile_CanBeCreatedAndRetrievedById()
        {
            // Arrange
            var localFile = CreateTestLocalFile();

            // Act
            await _store.UpsertLocalFileAsync(localFile);
            var retrieved = await _store.FindLocalFileByIdAsync(localFile.LocalFileId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(localFile.LocalFileId, retrieved.LocalFileId);
            Assert.Equal(localFile.Path, retrieved.Path);
            Assert.Equal(localFile.SizeBytes, retrieved.SizeBytes);
            Assert.Equal(localFile.DurationSeconds, retrieved.DurationSeconds);
            Assert.Equal(localFile.Codec, retrieved.Codec);
            Assert.Equal(localFile.Bitrate, retrieved.Bitrate);
            Assert.Equal(localFile.Channels, retrieved.Channels);
            Assert.Equal(localFile.HashPrimary, retrieved.HashPrimary);
            Assert.Equal(localFile.HashSecondary, retrieved.HashSecondary);
        }

        [Fact]
        public async Task LocalFile_CanBeFoundByPath()
        {
            // Arrange
            var localFile = CreateTestLocalFile(path: "/music/artist/album/01-track.flac");
            await _store.UpsertLocalFileAsync(localFile);

            // Act
            var retrieved = await _store.FindLocalFileByPathAsync("/music/artist/album/01-track.flac");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(localFile.LocalFileId, retrieved.LocalFileId);
            Assert.Equal("/music/artist/album/01-track.flac", retrieved.Path);
        }

        [Fact]
        public async Task LocalFile_CanBeFoundByHash()
        {
            // Arrange
            var hash = "abc123def456";
            var file1 = CreateTestLocalFile(path: "/music/track1.flac", hashPrimary: hash);
            var file2 = CreateTestLocalFile(path: "/music/track2.flac", hashPrimary: hash);
            await _store.UpsertLocalFileAsync(file1);
            await _store.UpsertLocalFileAsync(file2);

            // Act
            var retrieved = await _store.FindLocalFilesByHashAsync(hash);

            // Assert
            Assert.Equal(2, retrieved.Count);
            Assert.All(retrieved, f => Assert.Equal(hash, f.HashPrimary));
        }

        [Fact]
        public async Task LocalFile_QualityRating_FLAC_Is1_0()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "FLAC", bitrate: 1411);

            // Act & Assert
            Assert.Equal(1.0f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_QualityRating_MP3_320_Is0_9()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "MP3", bitrate: 320);

            // Act & Assert
            Assert.Equal(0.9f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_QualityRating_MP3_256_Is0_8()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "MP3", bitrate: 256);

            // Act & Assert
            Assert.Equal(0.8f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_QualityRating_MP3_192_Is0_7()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "MP3", bitrate: 192);

            // Act & Assert
            Assert.Equal(0.7f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_QualityRating_MP3_128_Is0_6()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "MP3", bitrate: 128);

            // Act & Assert
            Assert.Equal(0.6f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_QualityRating_AAC_256_Is0_85()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "AAC", bitrate: 256);

            // Act & Assert
            Assert.Equal(0.85f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_QualityRating_LowQuality_Is0_5()
        {
            // Arrange
            var localFile = CreateTestLocalFile(codec: "MP3", bitrate: 64);

            // Act & Assert
            Assert.Equal(0.5f, localFile.QualityRating);
        }

        [Fact]
        public async Task LocalFile_CanBeLinkedToTrack()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();

            var localFile = CreateTestLocalFile(inferredTrackId: track.TrackId);
            await _store.UpsertLocalFileAsync(localFile);

            // Act
            var files = await _store.ListLocalFilesForTrackAsync(track.TrackId);

            // Assert
            Assert.Single(files);
            Assert.Equal(localFile.LocalFileId, files[0].LocalFileId);
            Assert.Equal(track.TrackId, files[0].InferredTrackId);
        }

        [Fact]
        public async Task LocalFile_CanBeUpdated()
        {
            // Arrange
            var localFile = CreateTestLocalFile();
            await _store.UpsertLocalFileAsync(localFile);

            // Act - Update bitrate and codec
            var updated = new LocalFile
            {
                LocalFileId = localFile.LocalFileId,
                Path = localFile.Path,
                SizeBytes = localFile.SizeBytes,
                DurationSeconds = localFile.DurationSeconds,
                Codec = "MP3",
                Bitrate = 320,
                Channels = localFile.Channels,
                HashPrimary = localFile.HashPrimary,
                HashSecondary = localFile.HashSecondary,
                AudioFingerprintId = localFile.AudioFingerprintId,
                InferredTrackId = localFile.InferredTrackId,
                AddedAt = localFile.AddedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _store.UpsertLocalFileAsync(updated);

            var retrieved = await _store.FindLocalFileByIdAsync(localFile.LocalFileId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(320, retrieved.Bitrate);
            Assert.Equal("MP3", retrieved.Codec);
            Assert.Equal(0.9f, retrieved.QualityRating);
        }

        [Fact]
        public async Task CountLocalFilesAsync_ReturnsCorrectCount()
        {
            // Arrange
            await _store.UpsertLocalFileAsync(CreateTestLocalFile(path: "/music/track1.flac"));
            await _store.UpsertLocalFileAsync(CreateTestLocalFile(path: "/music/track2.flac"));
            await _store.UpsertLocalFileAsync(CreateTestLocalFile(path: "/music/track3.flac"));

            // Act
            var count = await _store.CountLocalFilesAsync();

            // Assert
            Assert.Equal(3, count);
        }

        // ========== VerifiedCopy Tests ==========

        [Fact]
        public async Task VerifiedCopy_CanBeLinkLocalFileToTrack()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();
            var localFile = CreateTestLocalFile();
            await _store.UpsertLocalFileAsync(localFile);

            var verifiedCopy = new VerifiedCopy
            {
                VerifiedCopyId = Guid.NewGuid().ToString(),
                TrackId = track.TrackId,
                LocalFileId = localFile.LocalFileId,
                HashPrimary = localFile.HashPrimary,
                DurationSeconds = localFile.DurationSeconds,
                VerificationSource = VerificationSource.MultiCheck,
                VerifiedAt = DateTimeOffset.UtcNow,
                Notes = "Automatically verified by match engine",
            };

            // Act
            await _store.UpsertVerifiedCopyAsync(verifiedCopy);
            var retrieved = await _store.FindVerifiedCopyForTrackAsync(track.TrackId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(verifiedCopy.VerifiedCopyId, retrieved.VerifiedCopyId);
            Assert.Equal(track.TrackId, retrieved.TrackId);
            Assert.Equal(localFile.LocalFileId, retrieved.LocalFileId);
            Assert.Equal(VerificationSource.MultiCheck, retrieved.VerificationSource);
        }

        [Fact]
        public async Task VerifiedCopy_FindVerifiedCopyForTrack_ReturnsMostRecent()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();
            var file1 = CreateTestLocalFile(path: "/music/old.flac");
            var file2 = CreateTestLocalFile(path: "/music/new.flac");
            await _store.UpsertLocalFileAsync(file1);
            await _store.UpsertLocalFileAsync(file2);

            var oldVerification = CreateTestVerifiedCopy(track.TrackId, file1.LocalFileId, DateTimeOffset.UtcNow.AddDays(-7));
            var newVerification = CreateTestVerifiedCopy(track.TrackId, file2.LocalFileId, DateTimeOffset.UtcNow);

            await _store.UpsertVerifiedCopyAsync(oldVerification);
            await _store.UpsertVerifiedCopyAsync(newVerification);

            // Act
            var retrieved = await _store.FindVerifiedCopyForTrackAsync(track.TrackId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(newVerification.VerifiedCopyId, retrieved.VerifiedCopyId);
            Assert.Equal(file2.LocalFileId, retrieved.LocalFileId);
        }

        [Fact]
        public async Task VerifiedCopy_ListVerifiedCopiesForTrack_ReturnsAll()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();
            var file1 = CreateTestLocalFile(path: "/music/copy1.flac");
            var file2 = CreateTestLocalFile(path: "/music/copy2.flac");
            await _store.UpsertLocalFileAsync(file1);
            await _store.UpsertLocalFileAsync(file2);

            var verification1 = CreateTestVerifiedCopy(track.TrackId, file1.LocalFileId);
            var verification2 = CreateTestVerifiedCopy(track.TrackId, file2.LocalFileId);

            await _store.UpsertVerifiedCopyAsync(verification1);
            await _store.UpsertVerifiedCopyAsync(verification2);

            // Act
            var retrieved = await _store.ListVerifiedCopiesForTrackAsync(track.TrackId);

            // Assert
            Assert.Equal(2, retrieved.Count);
            Assert.Contains(retrieved, v => v.LocalFileId == file1.LocalFileId);
            Assert.Contains(retrieved, v => v.LocalFileId == file2.LocalFileId);
        }

        [Fact]
        public async Task VerifiedCopy_CanBeFoundById()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();
            var localFile = CreateTestLocalFile();
            await _store.UpsertLocalFileAsync(localFile);

            var verifiedCopy = CreateTestVerifiedCopy(track.TrackId, localFile.LocalFileId);
            await _store.UpsertVerifiedCopyAsync(verifiedCopy);

            // Act
            var retrieved = await _store.FindVerifiedCopyByIdAsync(verifiedCopy.VerifiedCopyId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(verifiedCopy.VerifiedCopyId, retrieved.VerifiedCopyId);
        }

        [Fact]
        public async Task VerifiedCopy_CanBeDeleted()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();
            var localFile = CreateTestLocalFile();
            await _store.UpsertLocalFileAsync(localFile);

            var verifiedCopy = CreateTestVerifiedCopy(track.TrackId, localFile.LocalFileId);
            await _store.UpsertVerifiedCopyAsync(verifiedCopy);

            // Act
            await _store.DeleteVerifiedCopyAsync(verifiedCopy.VerifiedCopyId);
            var retrieved = await _store.FindVerifiedCopyByIdAsync(verifiedCopy.VerifiedCopyId);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task VerifiedCopy_SupportsAllVerificationSources()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();

            var sources = new[]
            {
                VerificationSource.Manual,
                VerificationSource.MultiCheck,
                VerificationSource.Fingerprint,
                VerificationSource.Imported,
            };

            // Act & Assert
            foreach (var source in sources)
            {
                var localFile = CreateTestLocalFile(path: $"/music/{source}.flac");
                await _store.UpsertLocalFileAsync(localFile);

                var verifiedCopy = CreateTestVerifiedCopy(track.TrackId, localFile.LocalFileId, verificationSource: source);
                await _store.UpsertVerifiedCopyAsync(verifiedCopy);

                var retrieved = await _store.FindVerifiedCopyByIdAsync(verifiedCopy.VerifiedCopyId);
                Assert.NotNull(retrieved);
                Assert.Equal(source, retrieved.VerificationSource);
            }
        }

        [Fact]
        public async Task CountVerifiedCopiesAsync_ReturnsCorrectCount()
        {
            // Arrange
            var track1 = await CreateAndInsertTestTrackWithDependencies();
            var track2 = await CreateAndInsertTestTrackWithDependencies();
            var file1 = CreateTestLocalFile(path: "/music/track1.flac");
            var file2 = CreateTestLocalFile(path: "/music/track2.flac");

            await _store.UpsertLocalFileAsync(file1);
            await _store.UpsertLocalFileAsync(file2);

            await _store.UpsertVerifiedCopyAsync(CreateTestVerifiedCopy(track1.TrackId, file1.LocalFileId));
            await _store.UpsertVerifiedCopyAsync(CreateTestVerifiedCopy(track2.TrackId, file2.LocalFileId));

            // Act
            var count = await _store.CountVerifiedCopiesAsync();

            // Assert
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task VerifiedCopy_IntegrityCheck_HashAndDurationMatch()
        {
            // Arrange
            var track = await CreateAndInsertTestTrackWithDependencies();
            var localFile = CreateTestLocalFile(hashPrimary: "abc123", durationSeconds: 180);
            await _store.UpsertLocalFileAsync(localFile);

            var verifiedCopy = new VerifiedCopy
            {
                VerifiedCopyId = Guid.NewGuid().ToString(),
                TrackId = track.TrackId,
                LocalFileId = localFile.LocalFileId,
                HashPrimary = "abc123",
                DurationSeconds = 180,
                VerificationSource = VerificationSource.MultiCheck,
                VerifiedAt = DateTimeOffset.UtcNow,
            };

            // Act
            await _store.UpsertVerifiedCopyAsync(verifiedCopy);
            var retrieved = await _store.FindVerifiedCopyByIdAsync(verifiedCopy.VerifiedCopyId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(localFile.HashPrimary, retrieved.HashPrimary);
            Assert.Equal(localFile.DurationSeconds, retrieved.DurationSeconds);
        }

        // ========== Helper Methods ==========

        private static LocalFile CreateTestLocalFile(
            string? path = null,
            string? codec = null,
            int? bitrate = null,
            string? hashPrimary = null,
            int? durationSeconds = null,
            string? inferredTrackId = null)
        {
            return new LocalFile
            {
                LocalFileId = Guid.NewGuid().ToString(),
                Path = path ?? $"/music/test/{Guid.NewGuid()}.flac",
                SizeBytes = 25_000_000,
                DurationSeconds = durationSeconds ?? 180,
                Codec = codec ?? "FLAC",
                Bitrate = bitrate ?? 1411,
                Channels = 2,
                HashPrimary = hashPrimary ?? Guid.NewGuid().ToString("N"),
                HashSecondary = Guid.NewGuid().ToString("N"),
                AudioFingerprintId = null,
                InferredTrackId = inferredTrackId,
                AddedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }

        private static VerifiedCopy CreateTestVerifiedCopy(
            string trackId,
            string localFileId,
            DateTimeOffset? verifiedAt = null,
            VerificationSource verificationSource = VerificationSource.MultiCheck)
        {
            return new VerifiedCopy
            {
                VerifiedCopyId = Guid.NewGuid().ToString(),
                TrackId = trackId,
                LocalFileId = localFileId,
                HashPrimary = Guid.NewGuid().ToString("N"),
                DurationSeconds = 180,
                VerificationSource = verificationSource,
                VerifiedAt = verifiedAt ?? DateTimeOffset.UtcNow,
                Notes = "Test verification",
            };
        }

        private static Track CreateTestTrack()
        {
            var releaseId = Guid.NewGuid().ToString();
            return new Track
            {
                TrackId = Guid.NewGuid().ToString(),
                MusicBrainzRecordingId = null, // Avoid MBID constraint issues
                ReleaseId = releaseId,
                DiscNumber = 1,
                TrackNumber = 1,
                Title = "Test Track",
                DurationSeconds = 180,
                Isrc = null,
                Tags = null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }

        private async Task<Track> CreateAndInsertTestTrackWithDependencies()
        {
            // Create full hierarchy: Artist -> ReleaseGroup -> Release -> Track
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
            await _store.UpsertArtistAsync(artist);

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
            await _store.UpsertReleaseGroupAsync(releaseGroup);

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
            await _store.UpsertReleaseAsync(release);

            var track = new Track
            {
                TrackId = Guid.NewGuid().ToString(),
                MusicBrainzRecordingId = null,
                ReleaseId = release.ReleaseId,
                DiscNumber = 1,
                TrackNumber = 1,
                Title = "Test Track",
                DurationSeconds = 180,
                Isrc = null,
                Tags = null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _store.UpsertTrackAsync(track);

            return track;
        }
    }
}
