namespace slskd.Tests.Unit.VirtualSoulfind.Core;

using System;
using Xunit;
using slskd.VirtualSoulfind.Core;

public class ContentDomainTests
{
    [Fact]
    public void ContentDomain_Music_HasCorrectValue()
    {
        // Arrange & Act
        var domain = ContentDomain.Music;

        // Assert
        Assert.Equal(0, (int)domain);
    }

    [Fact]
    public void ContentDomain_GenericFile_HasCorrectValue()
    {
        // Arrange & Act
        var domain = ContentDomain.GenericFile;

        // Assert
        Assert.Equal(1, (int)domain);
    }

    [Theory]
    [InlineData(0, ContentDomain.Music)]
    [InlineData(1, ContentDomain.GenericFile)]
    public void ContentDomain_CanBeCastFromInt(int value, ContentDomain expected)
    {
        // Arrange & Act
        var domain = (ContentDomain)value;

        // Assert
        Assert.Equal(expected, domain);
    }

    [Fact]
    public void ContentDomain_IsDefined()
    {
        // Arrange & Act & Assert
        Assert.True(Enum.IsDefined(typeof(ContentDomain), ContentDomain.Music));
        Assert.True(Enum.IsDefined(typeof(ContentDomain), ContentDomain.GenericFile));
    }
}

public class ContentWorkIdTests
{
    [Fact]
    public void ContentWorkId_NewId_CreatesUniqueIds()
    {
        // Arrange & Act
        var id1 = ContentWorkId.NewId();
        var id2 = ContentWorkId.NewId();

        // Assert
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(Guid.Empty, id1.Value);
        Assert.NotEqual(Guid.Empty, id2.Value);
    }

    [Fact]
    public void ContentWorkId_Parse_ValidGuid_ReturnsCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var id = ContentWorkId.Parse(guidString);

        // Assert
        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void ContentWorkId_Parse_InvalidString_ThrowsFormatException()
    {
        // Arrange
        var invalidString = "not-a-guid";

        // Act & Assert
        Assert.Throws<FormatException>(() => ContentWorkId.Parse(invalidString));
    }

    [Fact]
    public void ContentWorkId_TryParse_ValidGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = ContentWorkId.TryParse(guidString, out var id);

        // Assert
        Assert.True(result);
        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void ContentWorkId_TryParse_InvalidString_ReturnsFalse()
    {
        // Arrange
        var invalidString = "not-a-guid";

        // Act
        var result = ContentWorkId.TryParse(invalidString, out var id);

        // Assert
        Assert.False(result);
        Assert.Equal(default, id);
    }

    [Fact]
    public void ContentWorkId_ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new ContentWorkId(guid);

        // Act
        var result = id.ToString();

        // Assert
        Assert.Equal(guid.ToString(), result);
    }

    [Fact]
    public void ContentWorkId_Equality_SameValue_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new ContentWorkId(guid);
        var id2 = new ContentWorkId(guid);

        // Act & Assert
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void ContentWorkId_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var id1 = ContentWorkId.NewId();
        var id2 = ContentWorkId.NewId();

        // Act & Assert
        Assert.NotEqual(id1, id2);
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }
}

public class ContentItemIdTests
{
    [Fact]
    public void ContentItemId_NewId_CreatesUniqueIds()
    {
        // Arrange & Act
        var id1 = ContentItemId.NewId();
        var id2 = ContentItemId.NewId();

        // Assert
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(Guid.Empty, id1.Value);
        Assert.NotEqual(Guid.Empty, id2.Value);
    }

    [Fact]
    public void ContentItemId_Parse_ValidGuid_ReturnsCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var id = ContentItemId.Parse(guidString);

        // Assert
        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void ContentItemId_Parse_InvalidString_ThrowsFormatException()
    {
        // Arrange
        var invalidString = "not-a-guid";

        // Act & Assert
        Assert.Throws<FormatException>(() => ContentItemId.Parse(invalidString));
    }

    [Fact]
    public void ContentItemId_TryParse_ValidGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = ContentItemId.TryParse(guidString, out var id);

        // Assert
        Assert.True(result);
        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void ContentItemId_TryParse_InvalidString_ReturnsFalse()
    {
        // Arrange
        var invalidString = "not-a-guid";

        // Act
        var result = ContentItemId.TryParse(invalidString, out var id);

        // Assert
        Assert.False(result);
        Assert.Equal(default, id);
    }

    [Fact]
    public void ContentItemId_ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new ContentItemId(guid);

        // Act
        var result = id.ToString();

        // Assert
        Assert.Equal(guid.ToString(), result);
    }

    [Fact]
    public void ContentItemId_Equality_SameValue_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new ContentItemId(guid);
        var id2 = new ContentItemId(guid);

        // Act & Assert
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void ContentItemId_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var id1 = ContentItemId.NewId();
        var id2 = ContentItemId.NewId();

        // Act & Assert
        Assert.NotEqual(id1, id2);
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }
}

public class ContentInterfacesTests
{
    // Test implementation for IContentWork
    private class TestContentWork : IContentWork
    {
        public ContentWorkId Id { get; init; }
        public ContentDomain Domain { get; init; }
        public string Title { get; init; }
        public string? Creator { get; init; }
        public int? Year { get; init; }
    }

    // Test implementation for IContentItem
    private class TestContentItem : IContentItem
    {
        public ContentItemId Id { get; init; }
        public ContentDomain Domain { get; init; }
        public ContentWorkId? WorkId { get; init; }
        public string Title { get; init; }
        public int? Position { get; init; }
        public TimeSpan? Duration { get; init; }
        public bool IsAdvertisable { get; init; } // T-MCP03
    }

    [Fact]
    public void IContentWork_CanBeImplemented()
    {
        // Arrange
        var workId = ContentWorkId.NewId();
        var work = new TestContentWork
        {
            Id = workId,
            Domain = ContentDomain.Music,
            Title = "Test Album",
            Creator = "Test Artist",
            Year = 2025
        };

        // Act & Assert
        Assert.Equal(workId, work.Id);
        Assert.Equal(ContentDomain.Music, work.Domain);
        Assert.Equal("Test Album", work.Title);
        Assert.Equal("Test Artist", work.Creator);
        Assert.Equal(2025, work.Year);
    }

    [Fact]
    public void IContentItem_CanBeImplemented()
    {
        // Arrange
        var itemId = ContentItemId.NewId();
        var workId = ContentWorkId.NewId();
        var item = new TestContentItem
        {
            Id = itemId,
            Domain = ContentDomain.Music,
            WorkId = workId,
            Title = "Test Track",
            Position = 1,
            Duration = TimeSpan.FromMinutes(3)
        };

        // Act & Assert
        Assert.Equal(itemId, item.Id);
        Assert.Equal(ContentDomain.Music, item.Domain);
        Assert.Equal(workId, item.WorkId);
        Assert.Equal("Test Track", item.Title);
        Assert.Equal(1, item.Position);
        Assert.Equal(TimeSpan.FromMinutes(3), item.Duration);
    }

    [Fact]
    public void IContentWork_OptionalFields_CanBeNull()
    {
        // Arrange & Act
        var work = new TestContentWork
        {
            Id = ContentWorkId.NewId(),
            Domain = ContentDomain.GenericFile,
            Title = "Generic File",
            Creator = null,
            Year = null
        };

        // Assert
        Assert.Null(work.Creator);
        Assert.Null(work.Year);
    }

    [Fact]
    public void IContentItem_OptionalFields_CanBeNull()
    {
        // Arrange & Act
        var item = new TestContentItem
        {
            Id = ContentItemId.NewId(),
            Domain = ContentDomain.GenericFile,
            WorkId = null,
            Title = "standalone-file.txt",
            Position = null,
            Duration = null
        };

        // Assert
        Assert.Null(item.WorkId);
        Assert.Null(item.Position);
        Assert.Null(item.Duration);
    }

    [Fact]
    public void ContentDomain_MusicWork_HasExpectedProperties()
    {
        // Arrange & Act
        var work = new TestContentWork
        {
            Id = ContentWorkId.NewId(),
            Domain = ContentDomain.Music,
            Title = "Abbey Road",
            Creator = "The Beatles",
            Year = 1969
        };

        // Assert
        Assert.Equal(ContentDomain.Music, work.Domain);
        Assert.NotNull(work.Creator);
        Assert.NotNull(work.Year);
    }

    [Fact]
    public void ContentDomain_GenericFileItem_HasMinimalProperties()
    {
        // Arrange & Act
        var item = new TestContentItem
        {
            Id = ContentItemId.NewId(),
            Domain = ContentDomain.GenericFile,
            WorkId = null,
            Title = "document.pdf",
            Position = null,
            Duration = null
        };

        // Assert
        Assert.Equal(ContentDomain.GenericFile, item.Domain);
        Assert.Null(item.WorkId);
        Assert.Null(item.Position);
        Assert.Null(item.Duration);
    }
}

