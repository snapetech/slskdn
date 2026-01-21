// <copyright file="SourceRegistryTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Sources
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     Tests for T-V2-P1-02: Source Registry.
    /// </summary>
    public class SourceRegistryTests : IDisposable
    {
        private readonly SqliteSourceRegistry _registry;
        private readonly string _dbPath;

        public SourceRegistryTests()
        {
            // Use temporary file-based SQLite for tests
            _dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_source_registry_{Guid.NewGuid()}.db");
            _registry = new SqliteSourceRegistry($"Data Source={_dbPath}");
        }

        public void Dispose()
        {
            // Cleanup temp database
            try
            {
                if (System.IO.File.Exists(_dbPath))
                {
                    System.IO.File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public async Task UpsertCandidateAsync_InsertsNewCandidate()
        {
            // Arrange
            var itemId = ContentItemId.NewId();
            var candidate = new SourceCandidate
            {
                Id = "test-candidate-1",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = "local://file1.mp3",
                ExpectedQuality = 0.9f,
                TrustScore = 1.0f,
                IsPreferred = true,
            };

            // Act
            await _registry.UpsertCandidateAsync(candidate);
            var results = await _registry.FindCandidatesForItemAsync(itemId);

            // Assert
            Assert.Single(results);
            Assert.Equal("test-candidate-1", results[0].Id);
            Assert.Equal(ContentBackendType.LocalLibrary, results[0].Backend);
            Assert.Equal(0.9f, results[0].ExpectedQuality);
        }

        [Fact]
        public async Task UpsertCandidateAsync_UpdatesExistingCandidate()
        {
            // Arrange
            var itemId = ContentItemId.NewId();
            var candidate = new SourceCandidate
            {
                Id = "test-candidate-2",
                ItemId = itemId,
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh://test",
                ExpectedQuality = 0.5f,
                TrustScore = 0.5f,
                IsPreferred = false,
            };

            await _registry.UpsertCandidateAsync(candidate);

            // Act - Update with higher scores
            var updated = new SourceCandidate
            {
                Id = "test-candidate-2", // Same ID
                ItemId = itemId,
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh://test",
                ExpectedQuality = 0.8f, // Updated
                TrustScore = 0.9f, // Updated
                IsPreferred = true, // Updated
            };

            await _registry.UpsertCandidateAsync(updated);
            var results = await _registry.FindCandidatesForItemAsync(itemId);

            // Assert
            Assert.Single(results);
            Assert.Equal(0.8f, results[0].ExpectedQuality);
            Assert.Equal(0.9f, results[0].TrustScore);
            Assert.True(results[0].IsPreferred);
        }

        [Fact]
        public async Task FindCandidatesForItemAsync_ReturnsEmptyForUnknownItem()
        {
            // Arrange
            var unknownItemId = ContentItemId.NewId();

            // Act
            var results = await _registry.FindCandidatesForItemAsync(unknownItemId);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task FindCandidatesForItemAsync_OrdersByTrustAndQuality()
        {
            // Arrange
            var itemId = ContentItemId.NewId();

            var lowTrust = new SourceCandidate
            {
                Id = "low-trust",
                ItemId = itemId,
                Backend = ContentBackendType.Http,
                BackendRef = "http://test1",
                ExpectedQuality = 0.5f,
                TrustScore = 0.3f, // Low trust
                IsPreferred = false,
            };

            var highTrust = new SourceCandidate
            {
                Id = "high-trust",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = "local://test",
                ExpectedQuality = 0.7f,
                TrustScore = 1.0f, // High trust
                IsPreferred = true,
            };

            await _registry.UpsertCandidateAsync(lowTrust);
            await _registry.UpsertCandidateAsync(highTrust);

            // Act
            var results = await _registry.FindCandidatesForItemAsync(itemId);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("high-trust", results[0].Id); // High trust first
            Assert.Equal("low-trust", results[1].Id);
        }

        [Fact]
        public async Task FindCandidatesForItemAsync_FiltersByBackend()
        {
            // Arrange
            var itemId = ContentItemId.NewId();

            var soulseekCandidate = new SourceCandidate
            {
                Id = "soulseek-1",
                ItemId = itemId,
                Backend = ContentBackendType.Soulseek,
                BackendRef = "soulseek://peer1/file.mp3",
                ExpectedQuality = 0.8f,
                TrustScore = 0.7f,
                IsPreferred = false,
            };

            var meshCandidate = new SourceCandidate
            {
                Id = "mesh-1",
                ItemId = itemId,
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh://content",
                ExpectedQuality = 0.6f,
                TrustScore = 0.8f,
                IsPreferred = false,
            };

            await _registry.UpsertCandidateAsync(soulseekCandidate);
            await _registry.UpsertCandidateAsync(meshCandidate);

            // Act
            var soulseekOnly = await _registry.FindCandidatesForItemAsync(
                itemId, 
                ContentBackendType.Soulseek);

            // Assert
            Assert.Single(soulseekOnly);
            Assert.Equal("soulseek-1", soulseekOnly[0].Id);
        }

        [Fact]
        public async Task RemoveCandidateAsync_RemovesCandidate()
        {
            // Arrange
            var itemId = ContentItemId.NewId();
            var candidate = new SourceCandidate
            {
                Id = "to-remove",
                ItemId = itemId,
                Backend = ContentBackendType.Torrent,
                BackendRef = "torrent://infohash",
                ExpectedQuality = 0.7f,
                TrustScore = 0.8f,
                IsPreferred = false,
            };

            await _registry.UpsertCandidateAsync(candidate);

            // Act
            await _registry.RemoveCandidateAsync("to-remove");
            var results = await _registry.FindCandidatesForItemAsync(itemId);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task RemoveStaleCandidatesAsync_RemovesOldCandidates()
        {
            // Arrange
            var itemId = ContentItemId.NewId();

            var stale = new SourceCandidate
            {
                Id = "stale",
                ItemId = itemId,
                Backend = ContentBackendType.Http,
                BackendRef = "http://old",
                ExpectedQuality = 0.5f,
                TrustScore = 0.5f,
                LastSeenAt = DateTimeOffset.UtcNow.AddDays(-30), // 30 days ago
                IsPreferred = false,
            };

            var fresh = new SourceCandidate
            {
                Id = "fresh",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = "local://new",
                ExpectedQuality = 0.9f,
                TrustScore = 0.9f,
                LastSeenAt = DateTimeOffset.UtcNow, // Recent
                IsPreferred = true,
            };

            await _registry.UpsertCandidateAsync(stale);
            await _registry.UpsertCandidateAsync(fresh);

            // Act
            var removed = await _registry.RemoveStaleCandidatesAsync(
                DateTimeOffset.UtcNow.AddDays(-7)); // Remove older than 7 days

            // Assert
            Assert.Equal(1, removed);
            var remaining = await _registry.FindCandidatesForItemAsync(itemId);
            Assert.Single(remaining);
            Assert.Equal("fresh", remaining[0].Id);
        }

        [Fact]
        public async Task CountCandidatesAsync_ReturnsCorrectCount()
        {
            // Arrange
            var itemId = ContentItemId.NewId();

            for (int i = 0; i < 5; i++)
            {
                await _registry.UpsertCandidateAsync(new SourceCandidate
                {
                    Id = $"candidate-{i}",
                    ItemId = itemId,
                    Backend = ContentBackendType.MeshDht,
                    BackendRef = $"mesh://test{i}",
                    ExpectedQuality = 0.5f,
                    TrustScore = 0.5f,
                    IsPreferred = false,
                });
            }

            // Act
            var count = await _registry.CountCandidatesAsync();

            // Assert
            Assert.Equal(5, count);
        }
    }
}
