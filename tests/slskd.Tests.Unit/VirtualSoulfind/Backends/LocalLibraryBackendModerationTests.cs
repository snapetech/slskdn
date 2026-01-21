// <copyright file="LocalLibraryBackendModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Backends
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Shares;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP03: Moderation filtering in LocalLibraryBackend.
    /// </summary>
    public class LocalLibraryBackendModerationTests
    {
        private readonly Mock<IShareRepository> _shareRepositoryMock = new();

        [Fact]
        public async Task FindCandidatesAsync_WithAdvertisableContent_ReturnsCandidates()
        {
            // Arrange
            var backend = new LocalLibraryBackend(_shareRepositoryMock.Object);
            var itemId = ContentItemId.Parse("550e8400-e29b-41d4-a716-446655440000");

            _shareRepositoryMock
                .Setup(x => x.FindContentItem(It.IsAny<string>()))
                .Returns(("Music", "album-123", "filename.mp3", true, null, 1234567890L));

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId);

            // Assert
            Assert.NotEmpty(candidates);
            var candidate = Assert.Single(candidates);
            Assert.Equal(ContentBackendType.LocalLibrary, candidate.Backend);
        }

        [Fact]
        public async Task FindCandidatesAsync_WithNonAdvertisableContent_ReturnsEmpty()
        {
            // Arrange
            var backend = new LocalLibraryBackend(_shareRepositoryMock.Object);
            var itemId = ContentItemId.Parse("550e8400-e29b-41d4-a716-446655440000");

            _shareRepositoryMock
                .Setup(x => x.FindContentItem(It.IsAny<string>()))
                .Returns(("Music", "album-123", "filename.mp3", false, "Blocked by MCP", 1234567890L)); // IsAdvertisable = false

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId);

            // Assert
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task FindCandidatesAsync_WithNoLocalContent_ReturnsEmpty()
        {
            // Arrange
            var backend = new LocalLibraryBackend(_shareRepositoryMock.Object);
            var itemId = ContentItemId.Parse("550e8400-e29b-41d4-a716-446655440000");

            _shareRepositoryMock
                .Setup(x => x.FindContentItem(It.IsAny<string>()))
                .Returns<(string, string, string, bool, string, long)?>(null); // No content found

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId);

            // Assert
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task FindCandidatesAsync_CreatesProperSourceCandidate()
        {
            // Arrange
            var backend = new LocalLibraryBackend(_shareRepositoryMock.Object);
            var itemId = ContentItemId.Parse("550e8400-e29b-41d4-a716-446655440000");
            const string expectedFilename = "filename.mp3";
            const long expectedSize = 1234567890L;

            _shareRepositoryMock
                .Setup(x => x.FindContentItem(It.IsAny<string>()))
                .Returns(("Music", "album-123", expectedFilename, true, null, expectedSize));

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId);

            // Assert
            var candidate = Assert.Single(candidates);
            Assert.Equal(ContentBackendType.LocalLibrary, candidate.Backend);
            Assert.Equal(expectedFilename, candidate.Filename);
            Assert.Equal(expectedSize, candidate.SizeBytes);
            Assert.Null(candidate.PeerId); // Local content has no peer
            Assert.Null(candidate.Uri); // Local content has no URI
        }
    }
}

