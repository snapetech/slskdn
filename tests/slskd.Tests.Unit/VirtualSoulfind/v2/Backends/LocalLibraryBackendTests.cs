// <copyright file="LocalLibraryBackendTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.MediaCore;
    using slskd.Shares;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     Tests for T-V2-P4-01: LocalLibrary Backend.
    /// </summary>
    public class LocalLibraryBackendTests
    {
        private static async Task<LocalLibraryBackend> CreateBackendAsync(Mock<IShareRepository> mockRepo, ContentItemId itemId, string contentId)
        {
            var registry = new ContentIdRegistry();
            await registry.RegisterAsync("mb:recording:" + itemId.Value, contentId);
            return new LocalLibraryBackend(mockRepo.Object, registry);
        }

        [Fact]
        public async Task FindCandidates_ContentExists_ReturnsCandidate()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var contentId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(contentId);
            mockRepo.Setup(r => r.FindContentItem(contentId))
                .Returns(("Music", "work:123", "file.flac", true, string.Empty, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            var backend = await CreateBackendAsync(mockRepo, itemId, contentId);

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId, CancellationToken.None);

            // Assert
            Assert.Single(candidates);
            var candidate = candidates.First();
            Assert.Equal(ContentBackendType.LocalLibrary, candidate.Backend);
            Assert.Equal(1.0f, candidate.TrustScore);
            Assert.Equal(1.0f, candidate.ExpectedQuality);
            Assert.True(candidate.IsPreferred);
        }

        [Fact]
        public async Task FindCandidates_ContentNotAdvertisable_ReturnsEmpty()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var contentId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(contentId);
            mockRepo.Setup(r => r.FindContentItem(contentId))
                .Returns(("Music", "work:123", "file.flac", false, "blocked", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            var backend = await CreateBackendAsync(mockRepo, itemId, contentId);

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId, CancellationToken.None);

            // Assert
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task FindCandidates_ContentNotFound_ReturnsEmpty()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var contentId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(contentId);
            mockRepo.Setup(r => r.FindContentItem(contentId))
                .Returns((ValueTuple<string, string, string, bool, string, long>?)null);

            var backend = await CreateBackendAsync(mockRepo, itemId, contentId);

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId, CancellationToken.None);

            // Assert
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task ValidateCandidate_ValidLocal_ReturnsValid()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var contentId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(contentId);
            mockRepo.Setup(r => r.FindContentItem(contentId))
                .Returns(("Music", "work:123", "file.flac", true, string.Empty, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            var backend = await CreateBackendAsync(mockRepo, itemId, contentId);
            var candidate = new SourceCandidate
            {
                Id = "local:123",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = contentId,
                ExpectedQuality = 1.0f,
                TrustScore = 1.0f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            };

            // Act
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(1.0f, result.TrustScore);
            Assert.Equal(1.0f, result.QualityScore);
        }

        [Fact]
        public async Task ValidateCandidate_NotLocalBackend_ReturnsInvalid()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var itemId = ContentItemId.NewId();
            var backend = await CreateBackendAsync(mockRepo, itemId, itemId.ToString());
            var candidate = new SourceCandidate
            {
                Id = "slsk:123",
                ItemId = itemId,
                Backend = ContentBackendType.Soulseek, // Wrong backend
                BackendRef = "slsk:user:file",
                ExpectedQuality = 80,
                TrustScore = 0.7f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            };

            // Act
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("not a LocalLibrary candidate", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_NoLongerExists_ReturnsInvalid()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var contentId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(contentId);
            mockRepo.Setup(r => r.FindContentItem(contentId))
                .Returns((ValueTuple<string, string, string, bool, string, long>?)null);

            var backend = await CreateBackendAsync(mockRepo, itemId, contentId);
            var candidate = new SourceCandidate
            {
                Id = "local:123",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = contentId,
                ExpectedQuality = 1.0f,
                TrustScore = 1.0f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            };

            // Act
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("no longer exists", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_NoLongerAdvertisable_ReturnsInvalid()
        {
            // Arrange
            var mockRepo = new Mock<IShareRepository>();
            var contentId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(contentId);
            mockRepo.Setup(r => r.FindContentItem(contentId))
                .Returns(("Music", "work:123", "file.flac", false, "quarantined", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            var backend = await CreateBackendAsync(mockRepo, itemId, contentId);
            var candidate = new SourceCandidate
            {
                Id = "local:123",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = contentId,
                ExpectedQuality = 1.0f,
                TrustScore = 1.0f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            };

            // Act
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("no longer advertisable", result.InvalidityReason);
        }
    }
}
