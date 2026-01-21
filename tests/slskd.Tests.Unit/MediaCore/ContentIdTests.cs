// <copyright file="ContentIdTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using System;
using slskd.MediaCore;
using Xunit;

public class ContentIdTests
{
    [Theory]
    [InlineData("content:audio:track:mb-12345", "audio", "track", "mb-12345")]
    [InlineData("content:video:movie:imdb-tt0111161", "video", "movie", "imdb-tt0111161")]
    [InlineData("content:image:photo:flickr-67890", "image", "photo", "flickr-67890")]
    [InlineData("content:text:book:goodreads-12345", "text", "book", "goodreads-12345")]
    [InlineData("content:application:software:github-repo", "application", "software", "github-repo")]
    public void Parse_ValidContentId_ReturnsCorrectComponents(string contentId, string expectedDomain, string expectedType, string expectedId)
    {
        var result = ContentIdParser.Parse(contentId);

        Assert.NotNull(result);
        Assert.Equal(expectedDomain, result.Domain);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(contentId, result.FullId);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", true)]
    [InlineData("content:video:movie:imdb-tt0111161", true)]
    [InlineData("content:image:photo:flickr-67890", true)]
    [InlineData("content:text:book:goodreads-12345", true)]
    [InlineData("content:application:software:github-repo", true)]
    [InlineData("invalid", false)]
    [InlineData("content:audio", false)]
    [InlineData("content:audio:track", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ContentId_ReturnsCorrectValidity(string contentId, bool expectedValid)
    {
        var result = ContentIdParser.IsValid(contentId);
        Assert.Equal(expectedValid, result);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", "audio")]
    [InlineData("content:video:movie:imdb-tt0111161", "video")]
    [InlineData("content:image:photo:flickr-67890", "image")]
    [InlineData("invalid", null)]
    [InlineData("", null)]
    public void GetDomain_ContentId_ReturnsCorrectDomain(string contentId, string expectedDomain)
    {
        var result = ContentIdParser.GetDomain(contentId);
        Assert.Equal(expectedDomain, result);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", "track")]
    [InlineData("content:video:movie:imdb-tt0111161", "movie")]
    [InlineData("content:image:photo:flickr-67890", "photo")]
    [InlineData("invalid", null)]
    public void GetType_ContentId_ReturnsCorrectType(string contentId, string expectedType)
    {
        var result = ContentIdParser.GetType(contentId);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", "mb-12345")]
    [InlineData("content:video:movie:imdb-tt0111161", "imdb-tt0111161")]
    [InlineData("invalid", null)]
    public void GetId_ContentId_ReturnsCorrectId(string contentId, string expectedId)
    {
        var result = ContentIdParser.GetId(contentId);
        Assert.Equal(expectedId, result);
    }

    [Theory]
    [InlineData("audio", "track", "mb-12345", "content:audio:track:mb-12345")]
    [InlineData("video", "movie", "imdb-tt0111161", "content:video:movie:imdb-tt0111161")]
    [InlineData("image", "photo", "flickr-67890", "content:image:photo:flickr-67890")]
    public void Create_ValidComponents_ReturnsCorrectContentId(string domain, string type, string id, string expectedContentId)
    {
        var result = ContentIdParser.Create(domain, type, id);
        Assert.Equal(expectedContentId, result);
    }

    [Fact]
    public void Create_EmptyDomain_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ContentIdParser.Create("", "track", "id"));
    }

    [Fact]
    public void Create_EmptyType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ContentIdParser.Create("audio", "", "id"));
    }

    [Fact]
    public void Create_EmptyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ContentIdParser.Create("audio", "track", ""));
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", true)]
    [InlineData("content:video:movie:imdb-tt0111161", false)]
    [InlineData("content:image:photo:flickr-67890", false)]
    [InlineData("content:text:book:goodreads-12345", false)]
    [InlineData("content:application:software:github-repo", false)]
    public void IsAudio_ContentId_ReturnsCorrectResult(string contentId, bool expectedAudio)
    {
        var parsed = ContentIdParser.Parse(contentId);
        Assert.NotNull(parsed);
        Assert.Equal(expectedAudio, parsed.IsAudio);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", false)]
    [InlineData("content:video:movie:imdb-tt0111161", true)]
    [InlineData("content:image:photo:flickr-67890", false)]
    [InlineData("content:text:book:goodreads-12345", false)]
    [InlineData("content:application:software:github-repo", false)]
    public void IsVideo_ContentId_ReturnsCorrectResult(string contentId, bool expectedVideo)
    {
        var parsed = ContentIdParser.Parse(contentId);
        Assert.NotNull(parsed);
        Assert.Equal(expectedVideo, parsed.IsVideo);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", false)]
    [InlineData("content:video:movie:imdb-tt0111161", false)]
    [InlineData("content:image:photo:flickr-67890", true)]
    [InlineData("content:text:book:goodreads-12345", false)]
    [InlineData("content:application:software:github-repo", false)]
    public void IsImage_ContentId_ReturnsCorrectResult(string contentId, bool expectedImage)
    {
        var parsed = ContentIdParser.Parse(contentId);
        Assert.NotNull(parsed);
        Assert.Equal(expectedImage, parsed.IsImage);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", false)]
    [InlineData("content:video:movie:imdb-tt0111161", false)]
    [InlineData("content:image:photo:flickr-67890", false)]
    [InlineData("content:text:book:goodreads-12345", true)]
    [InlineData("content:application:software:github-repo", false)]
    public void IsText_ContentId_ReturnsCorrectResult(string contentId, bool expectedText)
    {
        var parsed = ContentIdParser.Parse(contentId);
        Assert.NotNull(parsed);
        Assert.Equal(expectedText, parsed.IsText);
    }

    [Theory]
    [InlineData("content:audio:track:mb-12345", false)]
    [InlineData("content:video:movie:imdb-tt0111161", false)]
    [InlineData("content:image:photo:flickr-67890", false)]
    [InlineData("content:text:book:goodreads-12345", false)]
    [InlineData("content:application:software:github-repo", true)]
    public void IsApplication_ContentId_ReturnsCorrectResult(string contentId, bool expectedApplication)
    {
        var parsed = ContentIdParser.Parse(contentId);
        Assert.NotNull(parsed);
        Assert.Equal(expectedApplication, parsed.IsApplication);
    }

    [Fact]
    public void Parse_NullOrEmptyContentId_ReturnsNull()
    {
        Assert.Null(ContentIdParser.Parse(null));
        Assert.Null(ContentIdParser.Parse(""));
        Assert.Null(ContentIdParser.Parse("   "));
    }

    [Fact]
    public void Parse_InvalidFormat_ReturnsNull()
    {
        Assert.Null(ContentIdParser.Parse("invalid"));
        Assert.Null(ContentIdParser.Parse("content:audio"));
        Assert.Null(ContentIdParser.Parse("content:audio:track"));
        Assert.Null(ContentIdParser.Parse("content::track:id"));
        Assert.Null(ContentIdParser.Parse("content:audio::id"));
        Assert.Null(ContentIdParser.Parse("content:audio:track:"));
    }
}
