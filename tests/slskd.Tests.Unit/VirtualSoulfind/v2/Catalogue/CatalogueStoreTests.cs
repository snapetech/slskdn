// <copyright file="CatalogueStoreTests.cs" company="slskd Team">
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
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using Xunit;

    /// <summary>
    ///     Tests for T-V2-P1-03: Virtual Catalogue Store.
    /// </summary>
    public class CatalogueStoreTests
    {
        [Fact]
        public async Task Artist_UpsertAndFind_Works()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            var artist = new Artist
            {
                ArtistId = "artist:1",
                MusicBrainzId = "mbid-artist-1",
                Name = "Test Artist",
                SortName = "Artist, Test",
                Tags = "rock,alternative",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            await store.UpsertArtistAsync(artist);
            var foundById = await store.FindArtistByIdAsync("artist:1");
            var foundByMbid = await store.FindArtistByMBIDAsync("mbid-artist-1");

            // Assert
            Assert.NotNull(foundById);
            Assert.Equal("Test Artist", foundById.Name);
            Assert.NotNull(foundByMbid);
            Assert.Equal("artist:1", foundByMbid.ArtistId);
        }

        [Fact]
        public async Task Artist_SearchByName_Works()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            await store.UpsertArtistAsync(new Artist
            {
                ArtistId = "artist:1",
                Name = "Pink Floyd",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await store.UpsertArtistAsync(new Artist
            {
                ArtistId = "artist:2",
                Name = "The Beatles",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // Act
            var results = await store.SearchArtistsAsync("floyd");

            // Assert
            Assert.Single(results);
            Assert.Equal("Pink Floyd", results[0].Name);
        }

        [Fact]
        public async Task ReleaseGroup_UpsertAndFind_Works()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            var rg = new ReleaseGroup
            {
                ReleaseGroupId = "rg:1",
                MusicBrainzId = "mbid-rg-1",
                ArtistId = "artist:1",
                Title = "Dark Side of the Moon",
                PrimaryType = ReleaseGroupPrimaryType.Album,
                Year = 1973,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            await store.UpsertReleaseGroupAsync(rg);
            var foundById = await store.FindReleaseGroupByIdAsync("rg:1");
            var foundByMbid = await store.FindReleaseGroupByMBIDAsync("mbid-rg-1");

            // Assert
            Assert.NotNull(foundById);
            Assert.Equal("Dark Side of the Moon", foundById.Title);
            Assert.NotNull(foundByMbid);
            Assert.Equal(ReleaseGroupPrimaryType.Album, foundByMbid.PrimaryType);
        }

        [Fact]
        public async Task ReleaseGroup_ListForArtist_Works()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            await store.UpsertReleaseGroupAsync(new ReleaseGroup
            {
                ReleaseGroupId = "rg:1",
                ArtistId = "artist:1",
                Title = "Album 1",
                PrimaryType = ReleaseGroupPrimaryType.Album,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await store.UpsertReleaseGroupAsync(new ReleaseGroup
            {
                ReleaseGroupId = "rg:2",
                ArtistId = "artist:1",
                Title = "Album 2",
                PrimaryType = ReleaseGroupPrimaryType.Album,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // Act
            var results = await store.ListReleaseGroupsForArtistAsync("artist:1");

            // Assert
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task Release_UpsertAndFind_Works()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            var release = new Release
            {
                ReleaseId = "release:1",
                MusicBrainzId = "mbid-release-1",
                ReleaseGroupId = "rg:1",
                Title = "Dark Side of the Moon",
                Year = 1973,
                Country = "US",
                Label = "Harvest Records",
                CatalogNumber = "SMAS-11163",
                MediaCount = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            await store.UpsertReleaseAsync(release);
            var foundById = await store.FindReleaseByIdAsync("release:1");
            var foundByMbid = await store.FindReleaseByMBIDAsync("mbid-release-1");

            // Assert
            Assert.NotNull(foundById);
            Assert.Equal("Harvest Records", foundById.Label);
            Assert.NotNull(foundByMbid);
            Assert.Equal(1973, foundByMbid.Year);
        }

        [Fact]
        public async Task Track_UpsertAndFind_Works()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            var track = new Track
            {
                TrackId = "track:1",
                MusicBrainzRecordingId = "mbid-recording-1",
                ReleaseId = "release:1",
                DiscNumber = 1,
                TrackNumber = 5,
                Title = "Time",
                DurationSeconds = 414,
                Isrc = "GBAYE0601542",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            await store.UpsertTrackAsync(track);
            var foundById = await store.FindTrackByIdAsync("track:1");
            var foundByMbid = await store.FindTrackByMBIDAsync("mbid-recording-1");

            // Assert
            Assert.NotNull(foundById);
            Assert.Equal("Time", foundById.Title);
            Assert.Equal(414, foundById.DurationSeconds);
            Assert.NotNull(foundByMbid);
            Assert.Equal("track:1", foundByMbid.TrackId);
        }

        [Fact]
        public async Task Track_ListForRelease_OrdersCorrectly()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            await store.UpsertTrackAsync(new Track
            {
                TrackId = "track:3",
                ReleaseId = "release:1",
                DiscNumber = 1,
                TrackNumber = 3,
                Title = "Track 3",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await store.UpsertTrackAsync(new Track
            {
                TrackId = "track:1",
                ReleaseId = "release:1",
                DiscNumber = 1,
                TrackNumber = 1,
                Title = "Track 1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await store.UpsertTrackAsync(new Track
            {
                TrackId = "track:2",
                ReleaseId = "release:1",
                DiscNumber = 1,
                TrackNumber = 2,
                Title = "Track 2",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // Act
            var results = await store.ListTracksForReleaseAsync("release:1");

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("Track 1", results[0].Title);
            Assert.Equal("Track 2", results[1].Title);
            Assert.Equal("Track 3", results[2].Title);
        }

        [Fact]
        public async Task Count_Methods_Work()
        {
            // Arrange
            using var store = new InMemoryCatalogueStore();
            await store.UpsertArtistAsync(new Artist
            {
                ArtistId = "artist:1",
                Name = "Artist 1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await store.UpsertReleaseAsync(new Release
            {
                ReleaseId = "release:1",
                ReleaseGroupId = "rg:1",
                Title = "Release 1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await store.UpsertTrackAsync(new Track
            {
                TrackId = "track:1",
                ReleaseId = "release:1",
                TrackNumber = 1,
                Title = "Track 1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // Act
            var artistCount = await store.CountArtistsAsync();
            var releaseCount = await store.CountReleasesAsync();
            var trackCount = await store.CountTracksAsync();

            // Assert
            Assert.Equal(1, artistCount);
            Assert.Equal(1, releaseCount);
            Assert.Equal(1, trackCount);
        }
    }
}
