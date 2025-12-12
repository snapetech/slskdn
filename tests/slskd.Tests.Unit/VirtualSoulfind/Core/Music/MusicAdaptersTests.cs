namespace slskd.Tests.Unit.VirtualSoulfind.Core.Music;

using System;
using Xunit;
using slskd.HashDb.Models;
using slskd.VirtualSoulfind.Core;
using slskd.VirtualSoulfind.Core.Music;

public class MusicWorkTests
{
    [Fact]
    public void MusicWork_ImplementsIContentWork()
    {
        // Arrange
        var albumEntry = CreateTestAlbumEntry();
        var workId = ContentWorkId.NewId();

        // Act
        IContentWork work = new MusicWork(workId, albumEntry);

        // Assert
        Assert.NotNull(work);
        Assert.Equal(ContentDomain.Music, work.Domain);
    }

    [Fact]
    public void MusicWork_MapsAlbumEntryPropertiesCorrectly()
    {
        // Arrange
        var albumEntry = new AlbumTargetEntry
        {
            ReleaseId = Guid.NewGuid().ToString(),
            Title = "Abbey Road",
            Artist = "The Beatles",
            ReleaseDate = "1969-09-26",
            Label = "Apple Records",
            Country = "GB",
            Status = "Official"
        };
        var workId = ContentWorkId.NewId();

        // Act
        var work = new MusicWork(workId, albumEntry);

        // Assert
        Assert.Equal(workId, work.Id);
        Assert.Equal(ContentDomain.Music, work.Domain);
        Assert.Equal("Abbey Road", work.Title);
        Assert.Equal("The Beatles", work.Creator);
        Assert.Equal(1969, work.Year);
        Assert.Equal("Apple Records", work.Label);
        Assert.Equal("GB", work.Country);
        Assert.Equal("Official", work.Status);
    }

    [Fact]
    public void MusicWork_ParsesYearFromReleaseDate()
    {
        // Arrange
        var albumEntry = CreateTestAlbumEntry();
        albumEntry.ReleaseDate = "2025-12-11";
        var workId = ContentWorkId.NewId();

        // Act
        var work = new MusicWork(workId, albumEntry);

        // Assert
        Assert.Equal(2025, work.Year);
    }

    [Fact]
    public void MusicWork_HandlesNullReleaseDate()
    {
        // Arrange
        var albumEntry = CreateTestAlbumEntry();
        albumEntry.ReleaseDate = null;
        var workId = ContentWorkId.NewId();

        // Act
        var work = new MusicWork(workId, albumEntry);

        // Assert
        Assert.Null(work.Year);
    }

    [Fact]
    public void MusicWork_HandlesInvalidReleaseDate()
    {
        // Arrange
        var albumEntry = CreateTestAlbumEntry();
        albumEntry.ReleaseDate = "invalid-date";
        var workId = ContentWorkId.NewId();

        // Act
        var work = new MusicWork(workId, albumEntry);

        // Assert
        Assert.Null(work.Year);
    }

    [Fact]
    public void MusicWork_FromAlbumEntry_GeneratesDeterministicId()
    {
        // Arrange
        var albumEntry = CreateTestAlbumEntry();

        // Act
        var work1 = MusicWork.FromAlbumEntry(albumEntry);
        var work2 = MusicWork.FromAlbumEntry(albumEntry);

        // Assert
        Assert.Equal(work1.Id, work2.Id); // Same MBID should produce same ContentWorkId
        Assert.Equal(albumEntry.ReleaseId, work1.ReleaseId);
    }

    [Fact]
    public void MusicWork_ThrowsOnNullAlbumEntry()
    {
        // Arrange
        var workId = ContentWorkId.NewId();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MusicWork(workId, null));
    }

    private static AlbumTargetEntry CreateTestAlbumEntry()
    {
        return new AlbumTargetEntry
        {
            ReleaseId = Guid.NewGuid().ToString(),
            Title = "Test Album",
            Artist = "Test Artist",
            ReleaseDate = "2025-01-01"
        };
    }
}

public class MusicItemTests
{
    [Fact]
    public void MusicItem_ImplementsIContentItem()
    {
        // Arrange
        var trackEntry = CreateTestTrackEntry();
        var itemId = ContentItemId.NewId();
        var workId = ContentWorkId.NewId();

        // Act
        IContentItem item = new MusicItem(itemId, workId, trackEntry);

        // Assert
        Assert.NotNull(item);
        Assert.Equal(ContentDomain.Music, item.Domain);
    }

    [Fact]
    public void MusicItem_MapsTrackEntryPropertiesCorrectly()
    {
        // Arrange
        var trackEntry = new AlbumTargetTrackEntry
        {
            ReleaseId = Guid.NewGuid().ToString(),
            RecordingId = Guid.NewGuid().ToString(),
            Position = 3,
            Title = "Come Together",
            Artist = "The Beatles",
            DurationMs = 259000, // 4:19
            Isrc = "GBAYE0601729"
        };
        var itemId = ContentItemId.NewId();
        var workId = ContentWorkId.NewId();

        // Act
        var item = new MusicItem(itemId, workId, trackEntry);

        // Assert
        Assert.Equal(itemId, item.Id);
        Assert.Equal(workId, item.WorkId);
        Assert.Equal(ContentDomain.Music, item.Domain);
        Assert.Equal("Come Together", item.Title);
        Assert.Equal(3, item.Position);
        Assert.Equal(TimeSpan.FromMilliseconds(259000), item.Duration);
        Assert.Equal("The Beatles", item.Artist);
        Assert.Equal("GBAYE0601729", item.Isrc);
    }

    [Fact]
    public void MusicItem_ConvertsDurationMsToTimeSpan()
    {
        // Arrange
        var trackEntry = CreateTestTrackEntry();
        trackEntry.DurationMs = 180000; // 3 minutes
        var itemId = ContentItemId.NewId();
        var workId = ContentWorkId.NewId();

        // Act
        var item = new MusicItem(itemId, workId, trackEntry);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(3), item.Duration);
        Assert.Equal(180000, item.DurationMs);
    }

    [Fact]
    public void MusicItem_HandlesNullDuration()
    {
        // Arrange
        var trackEntry = CreateTestTrackEntry();
        trackEntry.DurationMs = null;
        var itemId = ContentItemId.NewId();
        var workId = ContentWorkId.NewId();

        // Act
        var item = new MusicItem(itemId, workId, trackEntry);

        // Assert
        Assert.Null(item.Duration);
        Assert.Null(item.DurationMs);
    }

    [Fact]
    public void MusicItem_FromTrackEntry_GeneratesDeterministicId()
    {
        // Arrange
        var trackEntry = CreateTestTrackEntry();

        // Act
        var item1 = MusicItem.FromTrackEntry(trackEntry);
        var item2 = MusicItem.FromTrackEntry(trackEntry);

        // Assert
        Assert.Equal(item1.Id, item2.Id); // Same Recording ID should produce same ContentItemId
        Assert.Equal(item1.WorkId, item2.WorkId); // Same Release ID should produce same ContentWorkId
        Assert.Equal(trackEntry.RecordingId, item1.RecordingId);
        Assert.Equal(trackEntry.ReleaseId, item1.ReleaseId);
    }

    [Fact]
    public void MusicItem_ThrowsOnNullTrackEntry()
    {
        // Arrange
        var itemId = ContentItemId.NewId();
        var workId = ContentWorkId.NewId();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MusicItem(itemId, workId, null));
    }

    private static AlbumTargetTrackEntry CreateTestTrackEntry()
    {
        return new AlbumTargetTrackEntry
        {
            ReleaseId = Guid.NewGuid().ToString(),
            RecordingId = Guid.NewGuid().ToString(),
            Position = 1,
            Title = "Test Track",
            Artist = "Test Artist",
            DurationMs = 180000
        };
    }
}

public class MusicDomainMappingTests
{
    [Fact]
    public void ReleaseIdToContentWorkId_IsDeterministic()
    {
        // Arrange
        var releaseId = Guid.NewGuid().ToString();

        // Act
        var workId1 = MusicDomainMapping.ReleaseIdToContentWorkId(releaseId);
        var workId2 = MusicDomainMapping.ReleaseIdToContentWorkId(releaseId);

        // Assert
        Assert.Equal(workId1, workId2);
    }

    [Fact]
    public void ReleaseIdToContentWorkId_DifferentIdsProduceDifferentResults()
    {
        // Arrange
        var releaseId1 = Guid.NewGuid().ToString();
        var releaseId2 = Guid.NewGuid().ToString();

        // Act
        var workId1 = MusicDomainMapping.ReleaseIdToContentWorkId(releaseId1);
        var workId2 = MusicDomainMapping.ReleaseIdToContentWorkId(releaseId2);

        // Assert
        Assert.NotEqual(workId1, workId2);
    }

    [Fact]
    public void RecordingIdToContentItemId_IsDeterministic()
    {
        // Arrange
        var recordingId = Guid.NewGuid().ToString();

        // Act
        var itemId1 = MusicDomainMapping.RecordingIdToContentItemId(recordingId);
        var itemId2 = MusicDomainMapping.RecordingIdToContentItemId(recordingId);

        // Assert
        Assert.Equal(itemId1, itemId2);
    }

    [Fact]
    public void RecordingIdToContentItemId_DifferentIdsProduceDifferentResults()
    {
        // Arrange
        var recordingId1 = Guid.NewGuid().ToString();
        var recordingId2 = Guid.NewGuid().ToString();

        // Act
        var itemId1 = MusicDomainMapping.RecordingIdToContentItemId(recordingId1);
        var itemId2 = MusicDomainMapping.RecordingIdToContentItemId(recordingId2);

        // Assert
        Assert.NotEqual(itemId1, itemId2);
    }

    [Fact]
    public void ReleaseIdToContentWorkId_ThrowsOnNullOrEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.ReleaseIdToContentWorkId(null));
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.ReleaseIdToContentWorkId(""));
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.ReleaseIdToContentWorkId("   "));
    }

    [Fact]
    public void ReleaseIdToContentWorkId_ThrowsOnInvalidGuid()
    {
        // Arrange
        var invalidGuid = "not-a-guid";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.ReleaseIdToContentWorkId(invalidGuid));
    }

    [Fact]
    public void RecordingIdToContentItemId_ThrowsOnNullOrEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.RecordingIdToContentItemId(null));
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.RecordingIdToContentItemId(""));
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.RecordingIdToContentItemId("   "));
    }

    [Fact]
    public void RecordingIdToContentItemId_ThrowsOnInvalidGuid()
    {
        // Arrange
        var invalidGuid = "not-a-guid";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            MusicDomainMapping.RecordingIdToContentItemId(invalidGuid));
    }

    [Fact]
    public void ReleaseIdToContentWorkId_ProducesValidGuid()
    {
        // Arrange
        var releaseId = Guid.NewGuid().ToString();

        // Act
        var workId = MusicDomainMapping.ReleaseIdToContentWorkId(releaseId);

        // Assert
        Assert.NotEqual(Guid.Empty, workId.Value);
    }

    [Fact]
    public void RecordingIdToContentItemId_ProducesValidGuid()
    {
        // Arrange
        var recordingId = Guid.NewGuid().ToString();

        // Act
        var itemId = MusicDomainMapping.RecordingIdToContentItemId(recordingId);

        // Assert
        Assert.NotEqual(Guid.Empty, itemId.Value);
    }

    [Fact]
    public void MusicDomainMapping_WorkAndItemIdsAreDifferent()
    {
        // Arrange
        var releaseId = Guid.NewGuid().ToString();
        var recordingId = Guid.NewGuid().ToString();

        // Act
        var workId = MusicDomainMapping.ReleaseIdToContentWorkId(releaseId);
        var itemId = MusicDomainMapping.RecordingIdToContentItemId(recordingId);

        // Assert
        // Different namespaces ensure Release IDs and Recording IDs never collide
        Assert.NotEqual(workId.Value, itemId.Value);
    }
}
