// <copyright file="ContentBackendTests.cs" company="slskd Team">
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
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     Tests for V2-P1-01: ContentBackend Interface & Types.
    /// </summary>
    public class ContentBackendTests
    {
        [Fact]
        public void ContentBackendType_HasExpectedValues()
        {
            // Assert - All expected backend types exist
            Assert.Equal(0, (int)ContentBackendType.LocalLibrary);
            Assert.Equal(1, (int)ContentBackendType.Soulseek);
            Assert.Equal(2, (int)ContentBackendType.MeshDht);
            Assert.Equal(3, (int)ContentBackendType.Torrent);
            Assert.Equal(4, (int)ContentBackendType.Http);
            Assert.Equal(5, (int)ContentBackendType.Lan);
        }

        [Fact]
        public void SourceCandidateValidationResult_Valid_CreatesValidResult()
        {
            // Act
            var result = SourceCandidateValidationResult.Valid(0.8f, 0.9f);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(0.8f, result.TrustScore);
            Assert.Equal(0.9f, result.QualityScore);
            Assert.Null(result.InvalidityReason);
        }

        [Fact]
        public void SourceCandidateValidationResult_Invalid_CreatesInvalidResult()
        {
            // Act
            var result = SourceCandidateValidationResult.Invalid("source_unavailable");

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(0.0f, result.TrustScore);
            Assert.Equal(0.0f, result.QualityScore);
            Assert.Equal("source_unavailable", result.InvalidityReason);
        }

        [Fact]
        public async Task NoopContentBackend_FindCandidatesAsync_ReturnsEmpty()
        {
            // Arrange
            var backend = new NoopContentBackend(ContentBackendType.LocalLibrary);
            var itemId = ContentItemId.NewId();

            // Act
            var candidates = await backend.FindCandidatesAsync(itemId, CancellationToken.None);

            // Assert
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task NoopContentBackend_ValidateCandidateAsync_ReturnsInvalid()
        {
            // Arrange
            var backend = new NoopContentBackend(ContentBackendType.MeshDht);
            var candidate = new SourceCandidate
            {
                Id = "test-candidate",
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh://test",
                ExpectedQuality = 0.8f,
                TrustScore = 0.9f,
            };

            // Act
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("noop_backend", result.InvalidityReason);
        }

        [Fact]
        public void NoopContentBackend_Properties_ReflectConstructorArgs()
        {
            // Arrange & Act
            var backend = new NoopContentBackend(
                ContentBackendType.Soulseek,
                ContentDomain.Music);

            // Assert
            Assert.Equal(ContentBackendType.Soulseek, backend.Type);
            Assert.Equal(ContentDomain.Music, backend.SupportedDomain);
        }

        [Fact]
        public void NoopContentBackend_SupportedDomain_CanBeNull()
        {
            // Arrange & Act
            var backend = new NoopContentBackend(ContentBackendType.LocalLibrary, null);

            // Assert
            Assert.Equal(ContentBackendType.LocalLibrary, backend.Type);
            Assert.Null(backend.SupportedDomain);
        }
    }
}
