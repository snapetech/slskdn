// <copyright file="LocalLibraryBackendModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Backends
{
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Shares;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
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
                .Returns(("Music", "album-123", "filename.mp3", true, string.Empty, 1234567890L));

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
                .Returns(("Music", "album-123", "filename.mp3", false, "Blocked by MCP", 1234567890L)); // IsAdvertisable = false, ModerationReason set

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
                .Returns((ValueTuple<string, string, string, bool, string, long>?)null); // No content found

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

            _shareRepositoryMock
                .Setup(x => x.FindContentItem(It.IsAny<string>()))
                .Returns(("Music", "album-123", "filename.mp3", true, string.Empty, 1234567890L)); // CheckedAt is 6th element

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId);

            // Assert: SourceCandidate has Id, ItemId, Backend, BackendRef, ExpectedQuality, TrustScore, LastValidatedAt, LastSeenAt, IsPreferred (no Filename/SizeBytes/PeerId/Uri)
            var candidate = Assert.Single(candidates);
            Assert.Equal(ContentBackendType.LocalLibrary, candidate.Backend);
            Assert.Equal(100f, candidate.ExpectedQuality);
            Assert.Equal(1.0f, candidate.TrustScore);
            Assert.True(candidate.IsPreferred);
            Assert.StartsWith("local:", candidate.Id);
            Assert.Equal(itemId.ToString(), candidate.BackendRef);
        }
    }
}

