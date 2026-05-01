// <copyright file="LibraryBloomDiffServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Moq;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Integrations.MusicBrainz.Bloom;
using slskd.PodCore;
using slskd.Wishlist;

public sealed class LibraryBloomDiffServiceTests
{
    [Fact]
    public void BloomFilter_FromBase64_RestoresMembership()
    {
        var filter = new BloomFilter(16, 0.01);
        filter.Add("salt\u001fmusicbrainz:recording\u001frec-1");

        var restored = BloomFilter.FromBase64(16, 0.01, filter.ToBase64(), filter.ItemCount);

        Assert.True(restored.Contains("salt\u001fmusicbrainz:recording\u001frec-1"));
        Assert.False(restored.Contains("salt\u001fmusicbrainz:recording\u001frec-2"));
    }

    [Fact]
    public async Task CreateSnapshotAsync_ContainsOnlyBloomMetadata()
    {
        var hashDb = CreateHashDb(
            heldRecordings: new[] { "rec-local" },
            targets: new[]
            {
                new AlbumTargetEntry { ReleaseId = "release-local", Title = "Local Release", Artist = "Artist" },
            },
            tracksByRelease: new Dictionary<string, IEnumerable<AlbumTargetTrackEntry>>
            {
                ["release-local"] = new[]
                {
                    new AlbumTargetTrackEntry
                    {
                        ReleaseId = "release-local",
                        RecordingId = "rec-local",
                        Title = "Local Track",
                        Artist = "Artist",
                    },
                },
            });
        var service = CreateService(hashDb);

        var snapshot = await service.CreateSnapshotAsync(new LibraryBloomSnapshotRequest
        {
            SaltId = "salt-1",
            ExpectedItems = 32,
        });

        Assert.Equal(1, snapshot.Version);
        Assert.Equal("salt-1", snapshot.SaltId);
        Assert.True(snapshot.ItemCount >= 1);
        Assert.True(snapshot.NamespaceItemCounts["musicbrainz:recording"] >= 1);
        Assert.True(snapshot.NamespaceItemCounts["musicbrainz:release"] >= 1);
        Assert.DoesNotContain("rec-local", snapshot.BitsBase64, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(snapshot.PrivacyNotes, note => note.Contains("does not include filenames", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CompareAsync_ReturnsLikelyMissingSuggestionFromInboundRecordingBloom()
    {
        var remoteSnapshot = await CreateRemoteSnapshotAsync("rec-missing");
        var localHashDb = CreateHashDb(
            heldRecordings: Array.Empty<string>(),
            targets: new[]
            {
                new AlbumTargetEntry { ReleaseId = "release-1", Title = "Remote Release", Artist = "Remote Artist" },
            },
            tracksByRelease: new Dictionary<string, IEnumerable<AlbumTargetTrackEntry>>
            {
                ["release-1"] = new[]
                {
                    new AlbumTargetTrackEntry
                    {
                        ReleaseId = "release-1",
                        RecordingId = "rec-missing",
                        Title = "Missing Track",
                        Artist = "Remote Artist",
                    },
                },
            });
        var service = CreateService(localHashDb);

        var diff = await service.CompareAsync(new LibraryBloomDiffRequest
        {
            Snapshot = remoteSnapshot,
            Limit = 10,
        });

        Assert.True(diff.IsCompatible);
        var suggestion = Assert.Single(diff.Suggestions);
        Assert.Equal("rec-missing", suggestion.Mbid);
        Assert.Equal("Remote Artist Missing Track", suggestion.SearchText);
        Assert.True(suggestion.Confidence < 1);
        Assert.Contains("false positives", suggestion.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompareAsync_RejectsUnsupportedSnapshotVersion()
    {
        var service = CreateService(CreateHashDb());

        var diff = await service.CompareAsync(new LibraryBloomDiffRequest
        {
            Snapshot = new LibraryBloomSnapshot
            {
                Version = 99,
                SnapshotId = "future",
                SaltId = "salt",
                BitsBase64 = Convert.ToBase64String(new byte[] { 1 }),
                ExpectedItems = 16,
                FalsePositiveRate = 0.01,
            },
        });

        Assert.False(diff.IsCompatible);
        Assert.Empty(diff.Suggestions);
        Assert.Contains("Unsupported library Bloom snapshot version.", diff.Warnings);
    }

    [Fact]
    public async Task PromoteSuggestionsToWishlistAsync_CreatesReviewOnlyWishlistSeeds()
    {
        var remoteSnapshot = await CreateRemoteSnapshotAsync("rec-missing");
        var localHashDb = CreateHashDb(
            heldRecordings: Array.Empty<string>(),
            targets: new[]
            {
                new AlbumTargetEntry { ReleaseId = "release-1", Title = "Remote Release", Artist = "Remote Artist" },
            },
            tracksByRelease: new Dictionary<string, IEnumerable<AlbumTargetTrackEntry>>
            {
                ["release-1"] = new[]
                {
                    new AlbumTargetTrackEntry
                    {
                        ReleaseId = "release-1",
                        RecordingId = "rec-missing",
                        Title = "Missing Track",
                        Artist = "Remote Artist",
                    },
                },
            });
        var wishlist = new Mock<IWishlistService>();
        wishlist.Setup(service => service.ListAsync()).ReturnsAsync(new List<WishlistItem>());
        wishlist.Setup(service => service.CreateAsync(It.IsAny<WishlistItem>()))
            .ReturnsAsync((WishlistItem item) =>
            {
                item.Id = Guid.NewGuid();
                return item;
            });
        var service = CreateService(localHashDb, wishlist);

        var result = await service.PromoteSuggestionsToWishlistAsync(new LibraryBloomWishlistPromotionRequest
        {
            DiffRequest = new LibraryBloomDiffRequest { Snapshot = remoteSnapshot },
            Filter = "flac",
            MaxResults = 25,
        });

        Assert.Equal(1, result.CreatedCount);
        Assert.Empty(result.CreatedItemIds.Where(id => id == Guid.Empty));
        wishlist.Verify(service => service.CreateAsync(It.Is<WishlistItem>(item =>
            item.SearchText == "Remote Artist Missing Track" &&
            item.Filter == "flac" &&
            item.AutoDownload == false &&
            item.MaxResults == 25)), Times.Once);
    }

    private static async Task<LibraryBloomSnapshot> CreateRemoteSnapshotAsync(string recordingId)
    {
        var remoteHashDb = CreateHashDb(heldRecordings: new[] { recordingId });
        return await CreateService(remoteHashDb).CreateSnapshotAsync(new LibraryBloomSnapshotRequest
        {
            SaltId = "remote-salt",
            ExpectedItems = 32,
        });
    }

    private static LibraryBloomDiffService CreateService(
        Mock<IHashDbService> hashDb,
        Mock<IWishlistService>? wishlist = null)
    {
        wishlist ??= new Mock<IWishlistService>();
        wishlist.Setup(service => service.ListAsync()).ReturnsAsync(new List<WishlistItem>());
        return new LibraryBloomDiffService(hashDb.Object, wishlist.Object);
    }

    private static Mock<IHashDbService> CreateHashDb(
        IEnumerable<string>? heldRecordings = null,
        IEnumerable<AlbumTargetEntry>? targets = null,
        IReadOnlyDictionary<string, IEnumerable<AlbumTargetTrackEntry>>? tracksByRelease = null)
    {
        var hashDb = new Mock<IHashDbService>();
        hashDb.Setup(service => service.GetRecordingIdsWithVariantsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((heldRecordings ?? Array.Empty<string>()).ToList());
        hashDb.Setup(service => service.GetAlbumTargetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(targets ?? Array.Empty<AlbumTargetEntry>());
        hashDb.Setup(service => service.GetAlbumTracksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string releaseId, CancellationToken _) =>
                tracksByRelease != null && tracksByRelease.TryGetValue(releaseId, out var tracks)
                    ? tracks
                    : Array.Empty<AlbumTargetTrackEntry>());
        return hashDb;
    }
}
