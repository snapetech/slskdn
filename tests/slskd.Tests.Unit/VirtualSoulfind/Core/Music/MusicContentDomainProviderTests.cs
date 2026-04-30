// <copyright file="MusicContentDomainProviderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.VirtualSoulfind.Core.Music
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Moderation;
    using slskd.HashDb;
    using slskd.HashDb.Models;
    using slskd.VirtualSoulfind.Core.Music;
    using Xunit;

    /// <summary>
    ///     Tests for T-VC02: Music Domain Provider implementation.
    /// </summary>
    public class MusicContentDomainProviderTests
    {
        private readonly Mock<ILogger<MusicContentDomainProvider>> _loggerMock;
        private readonly Mock<IHashDbService> _hashDbMock;

        public MusicContentDomainProviderTests()
        {
            _loggerMock = new Mock<ILogger<MusicContentDomainProvider>>();
            _hashDbMock = new Mock<IHashDbService>();
        }

        [Fact]
        public async Task TryGetWorkByReleaseIdAsync_WithValidReleaseId_ReturnsMusicWork()
        {
            // Arrange
            var releaseId = "12345678-1234-1234-1234-123456789abc";
            var albumEntry = new AlbumTargetEntry
            {
                ReleaseId = releaseId,
                Title = "Test Album",
                Artist = "Test Artist"
            };

            _hashDbMock.Setup(h => h.GetAlbumTargetAsync(releaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(albumEntry);

            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryGetWorkByReleaseIdAsync(releaseId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Album", result.Title);
            Assert.Equal("Test Artist", result.Creator);
        }

        [Fact]
        public async Task TryGetWorkByReleaseIdAsync_WithInvalidReleaseId_ReturnsNull()
        {
            // Arrange
            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryGetWorkByReleaseIdAsync(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetWorkByReleaseIdAsync_WhenAlbumNotFound_ReturnsNull()
        {
            // Arrange
            var releaseId = "12345678-1234-1234-1234-123456789abc";

            _hashDbMock.Setup(h => h.GetAlbumTargetAsync(releaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AlbumTargetEntry?)null);

            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryGetWorkByReleaseIdAsync(releaseId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetWorkByTitleArtistAsync_ReturnsExactAlbumMatch()
        {
            // Arrange
            _hashDbMock.Setup(h => h.GetAlbumTargetsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new AlbumTargetEntry
                    {
                        ReleaseId = "12345678-1234-1234-1234-123456789abc",
                        Title = "Test Album",
                        Artist = "Test Artist",
                        ReleaseDate = "2020-01-01"
                    }
                });
            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryGetWorkByTitleArtistAsync("Test Album", "Test Artist", 2020);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Album", result.Title);
            Assert.Equal("Test Artist", result.Creator);
        }

        [Fact]
        public async Task TryGetItemByRecordingIdAsync_ReturnsTrack()
        {
            // Arrange
            var recordingId = "12345678-1234-1234-1234-123456789abc";
            var releaseId = "22345678-1234-1234-1234-123456789abc";
            _hashDbMock.Setup(h => h.LookupHashesByRecordingIdAsync(recordingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new HashDbEntry { MusicBrainzId = recordingId } });
            _hashDbMock.Setup(h => h.GetVariantsByRecordingAsync(recordingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<slskd.Audio.AudioVariant>());
            _hashDbMock.Setup(h => h.GetAlbumTargetsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new AlbumTargetEntry { ReleaseId = releaseId, Title = "Album", Artist = "Artist" } });
            _hashDbMock.Setup(h => h.GetAlbumTracksAsync(releaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new AlbumTargetTrackEntry { ReleaseId = releaseId, RecordingId = recordingId, Title = "Track", Artist = "Artist", Position = 1 }
                });
            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryGetItemByRecordingIdAsync(recordingId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Track", result.Title);
            Assert.True(result.IsAdvertisable);
        }

        [Fact]
        public async Task TryGetItemByLocalMetadataAsync_ReturnsExactTagMatch()
        {
            // Arrange
            var fileMetadata = new LocalFileMetadata { Id = "test.flac", SizeBytes = 1024L };
            var tags = new AudioTags("Test Track", "Test Artist", "Test Album", null, null, null, null, null, null, null, null, null, null, null);
            var releaseId = "32345678-1234-1234-1234-123456789abc";
            var recordingId = "42345678-1234-1234-1234-123456789abc";
            _hashDbMock.Setup(h => h.GetAlbumTargetsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new AlbumTargetEntry { ReleaseId = releaseId, Title = "Test Album", Artist = "Test Artist" } });
            _hashDbMock.Setup(h => h.GetAlbumTracksAsync(releaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new AlbumTargetTrackEntry { ReleaseId = releaseId, RecordingId = recordingId, Title = "Test Track", Artist = "Test Artist", Position = 1 }
                });
            _hashDbMock.Setup(h => h.LookupHashesByRecordingIdAsync(recordingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new HashDbEntry { MusicBrainzId = recordingId } });

            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryGetItemByLocalMetadataAsync(fileMetadata, tags);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Track", result.Title);
        }

        [Fact]
        public async Task TryMatchTrackByFingerprintAsync_ReturnsClosestDurationMatch()
        {
            // Arrange
            var recordingId = "52345678-1234-1234-1234-123456789abc";
            _hashDbMock.Setup(h => h.LookupHashesByAudioFingerprintAsync("fingerprint123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new HashDbEntry { MusicBrainzId = recordingId, DurationMs = 200_000, QualityScore = 1.0, UseCount = 5 }
                });
            _hashDbMock.Setup(h => h.LookupHashesByRecordingIdAsync(recordingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new HashDbEntry { MusicBrainzId = recordingId } });
            _hashDbMock.Setup(h => h.GetVariantsByRecordingAsync(recordingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<slskd.Audio.AudioVariant>
                {
                    new() { MusicBrainzRecordingId = recordingId, VariantId = "Fingerprint Track", DurationMs = 200_000, QualityScore = 1.0 }
                });
            _hashDbMock.Setup(h => h.GetAlbumTargetsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AlbumTargetEntry>());
            var provider = new MusicContentDomainProvider(_loggerMock.Object, _hashDbMock.Object);

            // Act
            var result = await provider.TryMatchTrackByFingerprintAsync("fingerprint123", 200);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Fingerprint Track", result.Title);
        }
    }
}
