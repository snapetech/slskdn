// <copyright file="HashDbServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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

namespace slskd.Tests.Unit.HashDb;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Integrations.MusicBrainz.Models;
using Xunit;

public class HashDbServiceTests : IDisposable
{
    private readonly string testDir;
    private readonly HashDbService service;

    public HashDbServiceTests()
    {
        testDir = Path.Combine(Path.GetTempPath(), $"hashdb-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        service = new HashDbService(testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public void Constructor_InitializesDatabase()
    {
        var dbPath = Path.Combine(testDir, "hashdb.db");
        Assert.True(File.Exists(dbPath));
    }

    [Fact]
    public void Constructor_InitializesSeqIdToZero()
    {
        Assert.Equal(0, service.CurrentSeqId);
    }

    [Fact]
    public void GetStats_ReturnsZeroCountsForEmptyDb()
    {
        // Act
        var stats = service.GetStats();

        // Assert
        Assert.Equal(0, stats.TotalPeers);
        Assert.Equal(0, stats.SlskdnPeers);
        Assert.Equal(0, stats.TotalFlacEntries);
        Assert.Equal(0, stats.HashedFlacEntries);
        Assert.Equal(0, stats.TotalHashEntries);
        Assert.True(stats.DatabaseSizeBytes > 0); // SQLite creates some base tables
    }

    // ========== Peer Management Tests ==========

    [Fact]
    public async Task GetOrCreatePeerAsync_CreatesNewPeer()
    {
        // Act
        var peer = await service.GetOrCreatePeerAsync("testuser");

        // Assert
        Assert.NotNull(peer);
        Assert.Equal("testuser", peer.PeerId);
        Assert.True(peer.LastSeen > 0);
    }

    [Fact]
    public async Task GetOrCreatePeerAsync_ReturnsExistingPeer()
    {
        // Arrange
        await service.GetOrCreatePeerAsync("testuser");

        // Act
        var peer = await service.GetOrCreatePeerAsync("testuser");

        // Assert
        Assert.NotNull(peer);
        Assert.Equal("testuser", peer.PeerId);
    }

    [Fact]
    public async Task TouchPeerAsync_UpdatesLastSeen()
    {
        // Arrange
        var peer1 = await service.GetOrCreatePeerAsync("testuser");
        var originalLastSeen = peer1.LastSeen;
        await Task.Delay(10); // Ensure time passes

        // Act
        await service.TouchPeerAsync("testuser");
        var peer2 = await service.GetOrCreatePeerAsync("testuser");

        // Assert
        Assert.True(peer2.LastSeen >= originalLastSeen);
    }

    [Fact]
    public async Task UpdatePeerCapabilitiesAsync_UpdatesCaps()
    {
        // Arrange
        await service.GetOrCreatePeerAsync("testuser");

        // Act
        await service.UpdatePeerCapabilitiesAsync("testuser", slskd.Capabilities.PeerCapabilityFlags.SupportsMeshSync, "slskdn/1.0");

        // Assert
        var peers = await service.GetSlskdnPeersAsync();
        var peer = Assert.Single(peers);
        Assert.Equal("testuser", peer.PeerId);
        Assert.Equal("slskdn/1.0", peer.ClientVersion);
    }

    // ========== FLAC Inventory Tests ==========

    [Fact]
    public async Task UpsertFlacEntryAsync_InsertsNewEntry()
    {
        var entry = new FlacInventoryEntry
        {
            PeerId = "testuser",
            Path = "/music/test.flac",
            Size = 50000000,
            HashStatusStr = "none",
        };

        await service.UpsertFlacEntryAsync(entry);
        var stats = service.GetStats();
        Assert.Equal(1, stats.TotalFlacEntries);
    }

    [Fact]
    public async Task UpsertFlacEntryAsync_GeneratesFileId()
    {
        // Arrange
        var entry = new FlacInventoryEntry
        {
            PeerId = "testuser",
            Path = "/music/test.flac",
            Size = 50000000,
        };

        // Act
        await service.UpsertFlacEntryAsync(entry);

        // Assert
        Assert.NotNull(entry.FileId);
        Assert.NotEmpty(entry.FileId);
    }

    [Fact]
    public async Task UpsertFlacEntryAsync_UpdatesExistingEntry()
    {
        // Arrange
        var entry = new FlacInventoryEntry
        {
            PeerId = "testuser",
            Path = "/music/test.flac",
            Size = 50000000,
            HashStatusStr = "none",
        };
        await service.UpsertFlacEntryAsync(entry);

        // Act - Update with hash
        entry.HashStatusStr = "known";
        entry.HashValue = "abc123";
        await service.UpsertFlacEntryAsync(entry);

        // Assert
        var retrieved = await service.GetFlacEntryAsync(entry.FileId);
        Assert.NotNull(retrieved);
        Assert.Equal("known", retrieved.HashStatusStr);
    }

    [Fact]
    public async Task GetAlbumTargetsAsync_ReturnsStoredTarget()
    {
        var target = new AlbumTarget
        {
            MusicBrainzReleaseId = "mb:abc",
            Title = "Test Album",
            Artist = "Test Artist",
            Metadata = new ReleaseMetadata { ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow) },
            Tracks = new[]
            {
                new TrackTarget
                {
                    MusicBrainzRecordingId = "rec:1",
                    Position = 1,
                    Title = "Track One",
                    Artist = "Test Artist",
                    Duration = TimeSpan.FromSeconds(180),
                }
            }
        };

        await service.UpsertAlbumTargetAsync(target);
        var stored = (await service.GetAlbumTargetsAsync()).Single();

        Assert.Equal("Test Album", stored.Title);
        Assert.Equal("Test Artist", stored.Artist);
        Assert.Equal(target.Metadata.ReleaseDate?.ToString("yyyy-MM-dd"), stored.ReleaseDate);
    }

    [Fact]
    public async Task LookupHashesByRecordingIdAsync_ReturnsMatches()
    {
        var entry = new HashDbEntry
        {
            FlacKey = HashDbEntry.GenerateFlacKey("test.flac", 123456),
            ByteHash = "abcdef",
            Size = 123456,
            FirstSeenAt = 1,
            LastUpdatedAt = 1,
            SeqId = 1,
            UseCount = 1,
        };

        await service.StoreHashAsync(entry);
        await service.UpdateHashRecordingIdAsync(entry.FlacKey, "mb:rec1");

        var matches = await service.LookupHashesByRecordingIdAsync("mb:rec1");
        var match = Assert.Single(matches);
        Assert.Equal(entry.FlacKey, match.FlacKey);
    }

    [Fact]
    public async Task GetFlacEntriesBySizeAsync_FindsMatchingEntries()
    {
        // Arrange
        var size = 50000000L;
        await service.UpsertFlacEntryAsync(new FlacInventoryEntry
        {
            PeerId = "user1",
            Path = "/music/test1.flac",
            Size = size,
        });
        await service.UpsertFlacEntryAsync(new FlacInventoryEntry
        {
            PeerId = "user2",
            Path = "/music/test2.flac",
            Size = size,
        });
        await service.UpsertFlacEntryAsync(new FlacInventoryEntry
        {
            PeerId = "user3",
            Path = "/music/different.flac",
            Size = 60000000,
        });

        // Act
        var entries = await service.GetFlacEntriesBySizeAsync(size);

        // Assert
        Assert.Equal(2, ((List<FlacInventoryEntry>)entries).Count);
    }

    [Fact]
    public async Task GetUnhashedFlacFilesAsync_ReturnsOnlyUnhashed()
    {
        // Arrange
        await service.UpsertFlacEntryAsync(new FlacInventoryEntry
        {
            PeerId = "user1",
            Path = "/music/unhashed.flac",
            Size = 50000000,
            HashStatusStr = "none",
        });
        await service.UpsertFlacEntryAsync(new FlacInventoryEntry
        {
            PeerId = "user2",
            Path = "/music/hashed.flac",
            Size = 50000000,
            HashStatusStr = "known",
            HashValue = "abc123",
        });

        // Act
        var unhashed = await service.GetUnhashedFlacFilesAsync();

        // Assert
        var list = (List<FlacInventoryEntry>)unhashed;
        Assert.Single(list);
        Assert.Equal("user1", list[0].PeerId);
    }

    [Fact]
    public async Task UpdateFlacHashAsync_SetsHashAndStatus()
    {
        // Arrange
        var entry = new FlacInventoryEntry
        {
            PeerId = "testuser",
            Path = "/music/test.flac",
            Size = 50000000,
            HashStatusStr = "none",
        };
        await service.UpsertFlacEntryAsync(entry);

        // Act
        await service.UpdateFlacHashAsync(entry.FileId, "newhash123", HashSource.LocalScan);

        // Assert
        var updated = await service.GetFlacEntryAsync(entry.FileId);
        Assert.Equal("known", updated.HashStatusStr);
        Assert.Equal("newhash123", updated.HashValue);
    }

    [Fact]
    public async Task MarkFlacHashFailedAsync_SetsFailedStatus()
    {
        // Arrange
        var entry = new FlacInventoryEntry
        {
            PeerId = "testuser",
            Path = "/music/test.flac",
            Size = 50000000,
            HashStatusStr = "none",
        };
        await service.UpsertFlacEntryAsync(entry);

        // Act
        await service.MarkFlacHashFailedAsync(entry.FileId);

        // Assert
        var updated = await service.GetFlacEntryAsync(entry.FileId);
        Assert.Equal("failed", updated.HashStatusStr);
    }

    // ========== Hash Database Tests ==========

    [Fact]
    public async Task StoreHashAsync_StoresAndIncrementsSeqId()
    {
        // Arrange
        var entry = new HashDbEntry
        {
            FlacKey = "testkey",
            ByteHash = "testhash",
            Size = 50000000,
        };

        // Act
        await service.StoreHashAsync(entry);

        // Assert
        Assert.Equal(1, service.CurrentSeqId);
        var stats = service.GetStats();
        Assert.Equal(1, stats.TotalHashEntries);
    }

    [Fact]
    public async Task LookupHashAsync_FindsStoredHash()
    {
        // Arrange
        var entry = new HashDbEntry
        {
            FlacKey = "testkey",
            ByteHash = "testhash",
            Size = 50000000,
        };
        await service.StoreHashAsync(entry);

        // Act
        var found = await service.LookupHashAsync("testkey");

        // Assert
        Assert.NotNull(found);
        Assert.Equal("testhash", found.ByteHash);
        Assert.Equal(50000000, found.Size);
    }

    [Fact]
    public async Task LookupHashAsync_ReturnsNullForMissingKey()
    {
        // Act
        var found = await service.LookupHashAsync("nonexistent");

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public async Task LookupHashesBySizeAsync_FindsMatchingHashes()
    {
        // Arrange
        var size = 50000000L;
        await service.StoreHashAsync(new HashDbEntry
        {
            FlacKey = "key1",
            ByteHash = "hash1",
            Size = size,
        });
        await service.StoreHashAsync(new HashDbEntry
        {
            FlacKey = "key2",
            ByteHash = "hash2",
            Size = size,
        });

        // Act
        var hashes = await service.LookupHashesBySizeAsync(size);

        // Assert
        Assert.Equal(2, ((List<HashDbEntry>)hashes).Count);
    }

    [Fact]
    public async Task StoreHashFromVerificationAsync_CreatesCorrectKey()
    {
        // Arrange - hash needs to be at least 16 chars for logging substring
        var hash = "0123456789abcdef0123456789abcdef";

        // Act
        await service.StoreHashFromVerificationAsync("/music/test.flac", 50000000, hash);

        // Assert
        var expectedKey = HashDbEntry.GenerateFlacKey("/music/test.flac", 50000000);
        var found = await service.LookupHashAsync(expectedKey);
        Assert.NotNull(found);
        Assert.Equal(hash, found.ByteHash);
    }

    [Fact]
    public async Task IncrementHashUseCountAsync_IncrementsCount()
    {
        // Arrange
        var entry = new HashDbEntry
        {
            FlacKey = "testkey",
            ByteHash = "testhash",
            Size = 50000000,
        };
        await service.StoreHashAsync(entry);

        // Act
        await service.IncrementHashUseCountAsync("testkey");
        await service.IncrementHashUseCountAsync("testkey");

        // Assert
        var found = await service.LookupHashAsync("testkey");
        Assert.Equal(3, found.UseCount); // Initial 1 + 2 increments
    }

    // ========== Mesh Sync Tests ==========

    [Fact]
    public async Task GetEntriesSinceSeqAsync_ReturnsEntriesAfterSeq()
    {
        // Arrange
        await service.StoreHashAsync(new HashDbEntry { FlacKey = "key1", ByteHash = "hash1", Size = 100 });
        await service.StoreHashAsync(new HashDbEntry { FlacKey = "key2", ByteHash = "hash2", Size = 200 });
        await service.StoreHashAsync(new HashDbEntry { FlacKey = "key3", ByteHash = "hash3", Size = 300 });

        // Act
        var entries = await service.GetEntriesSinceSeqAsync(1);

        // Assert
        var list = (List<HashDbEntry>)entries;
        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.FlacKey == "key2");
        Assert.Contains(list, e => e.FlacKey == "key3");
    }

    [Fact]
    public async Task MergeEntriesFromMeshAsync_MergesNewEntries()
    {
        // Arrange
        var entries = new List<HashDbEntry>
        {
            new HashDbEntry { FlacKey = "mesh1", ByteHash = "meshhash1", Size = 100 },
            new HashDbEntry { FlacKey = "mesh2", ByteHash = "meshhash2", Size = 200 },
        };

        // Act
        var merged = await service.MergeEntriesFromMeshAsync(entries);

        // Assert
        Assert.Equal(2, merged);
        Assert.NotNull(await service.LookupHashAsync("mesh1"));
        Assert.NotNull(await service.LookupHashAsync("mesh2"));
    }

    [Fact]
    public async Task MergeEntriesFromMeshAsync_SkipsExistingEntries()
    {
        // Arrange
        await service.StoreHashAsync(new HashDbEntry { FlacKey = "existing", ByteHash = "localhash", Size = 100 });

        var entries = new List<HashDbEntry>
        {
            new HashDbEntry { FlacKey = "existing", ByteHash = "localhash", Size = 100 }, // Same hash
            new HashDbEntry { FlacKey = "new", ByteHash = "newhash", Size = 200 },
        };

        // Act
        var merged = await service.MergeEntriesFromMeshAsync(entries);

        // Assert
        Assert.Equal(1, merged); // Only the new one should be merged
    }

    [Fact]
    public async Task UpdatePeerLastSeqSeenAsync_TracksSeqPerPeer()
    {
        // Act
        await service.UpdatePeerLastSeqSeenAsync("peer1", 100);
        await service.UpdatePeerLastSeqSeenAsync("peer2", 200);

        // Assert
        Assert.Equal(100, await service.GetPeerLastSeqSeenAsync("peer1"));
        Assert.Equal(200, await service.GetPeerLastSeqSeenAsync("peer2"));
    }

    // ========== Backfill Tests ==========

    [Fact]
    public async Task GetBackfillCandidatesAsync_ReturnsUnhashedFiles()
    {
        // Arrange
        await service.GetOrCreatePeerAsync("testuser");
        await service.UpsertFlacEntryAsync(new FlacInventoryEntry
        {
            PeerId = "testuser",
            Path = "/music/test.flac",
            Size = 50000000,
            HashStatusStr = "none",
        });

        // Act
        var candidates = await service.GetBackfillCandidatesAsync();

        // Assert
        Assert.Single(candidates);
    }

    [Fact]
    public async Task IncrementPeerBackfillCountAsync_IncrementsCount()
    {
        // Arrange
        await service.GetOrCreatePeerAsync("testuser");

        // Act
        await service.IncrementPeerBackfillCountAsync("testuser");
        await service.IncrementPeerBackfillCountAsync("testuser");

        // Assert
        var count = await service.GetPeerBackfillCountTodayAsync("testuser");
        Assert.Equal(2, count);
    }

    // ========== FlacInventoryEntry Tests ==========

    [Fact]
    public void FlacInventoryEntry_GenerateFileId_IsConsistent()
    {
        // Act
        var id1 = FlacInventoryEntry.GenerateFileId("user", "/path/file.flac", 12345);
        var id2 = FlacInventoryEntry.GenerateFileId("user", "/path/file.flac", 12345);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void FlacInventoryEntry_GenerateFileId_DifferentInputsProduceDifferentIds()
    {
        // Act
        var id1 = FlacInventoryEntry.GenerateFileId("user1", "/path/file.flac", 12345);
        var id2 = FlacInventoryEntry.GenerateFileId("user2", "/path/file.flac", 12345);
        var id3 = FlacInventoryEntry.GenerateFileId("user1", "/path/other.flac", 12345);
        var id4 = FlacInventoryEntry.GenerateFileId("user1", "/path/file.flac", 99999);

        // Assert
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.NotEqual(id1, id4);
    }

    // ========== HashDbEntry Tests ==========

    [Fact]
    public void HashDbEntry_GenerateFlacKey_IsConsistent()
    {
        // Act
        var key1 = HashDbEntry.GenerateFlacKey("/path/file.flac", 12345);
        var key2 = HashDbEntry.GenerateFlacKey("/path/file.flac", 12345);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void HashDbEntry_PackUnpackMetaFlags_RoundTrips()
    {
        // Arrange
        var sampleRate = 44100;
        var channels = 2;
        var bitDepth = 16;

        // Act
        var packed = HashDbEntry.PackMetaFlags(sampleRate, channels, bitDepth);
        var (unpackedRate, unpackedChannels, unpackedDepth) = HashDbEntry.UnpackMetaFlags(packed);

        // Assert
        Assert.Equal(sampleRate, unpackedRate);
        Assert.Equal(channels, unpackedChannels);
        Assert.Equal(bitDepth, unpackedDepth);
    }
}

