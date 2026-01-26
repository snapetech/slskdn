// <copyright file="ContentLocatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Streaming;

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Shares;
using slskd.Streaming;
using Xunit;

public class ContentLocatorTests
{
    private readonly Mock<IShareService> _shareServiceMock = new();
    private readonly Mock<IShareRepository> _repoMock = new();
    private readonly Mock<ILogger<ContentLocator>> _logMock = new();

    private ContentLocator CreateLocator()
    {
        _shareServiceMock.Setup(x => x.GetLocalRepository()).Returns(_repoMock.Object);
        return new ContentLocator(_shareServiceMock.Object, _logMock.Object);
    }

    [Fact]
    public void Resolve_EmptyContentId_ReturnsNull()
    {
        var locator = CreateLocator();
        Assert.Null(locator.Resolve(""));
        Assert.Null(locator.Resolve("   "));
        Assert.Null(locator.Resolve(null!));
    }

    [Fact]
    public void Resolve_ContentItemNotFound_ReturnsNull()
    {
        var locator = CreateLocator();
        _repoMock.Setup(x => x.FindContentItem("c1")).Returns((ValueTuple<string, string, string, bool, string, long>?)null);

        var r = locator.Resolve("c1");

        Assert.Null(r);
    }

    [Fact]
    public void Resolve_ContentItemNotAdvertisable_ReturnsNull()
    {
        var locator = CreateLocator();
        _repoMock.Setup(x => x.FindContentItem("c1")).Returns(("Music", "w1", "masked.flac", false, "blocked", 0L));

        var r = locator.Resolve("c1");

        Assert.Null(r);
    }

    [Fact]
    public void Resolve_FileInfoNotFound_ReturnsNull()
    {
        var locator = CreateLocator();
        _repoMock.Setup(x => x.FindContentItem("c1")).Returns(("Music", "w1", "masked.flac", true, "", 0L));
        _repoMock.Setup(x => x.FindFileInfo("masked.flac")).Returns((Filename: "", Size: 0));

        var r = locator.Resolve("c1");

        Assert.Null(r);
    }

    [Fact]
    public void Resolve_FileNotOnDisk_ReturnsNull()
    {
        var locator = CreateLocator();
        _repoMock.Setup(x => x.FindContentItem("c1")).Returns(("Music", "w1", "masked.flac", true, "", 0L));
        _repoMock.Setup(x => x.FindFileInfo("masked.flac")).Returns((Filename: "/nonexistent.flac", Size: 1000));

        var r = locator.Resolve("c1");

        Assert.Null(r);
    }

    [Fact]
    public void Resolve_Success_ReturnsResolvedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), "ContentLoc_" + Guid.NewGuid().ToString("N")[..8] + ".mp3");
        try
        {
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            var locator = CreateLocator();
            _repoMock.Setup(x => x.FindContentItem("c1")).Returns(("Music", "w1", "masked.flac", true, "", 0L));
            _repoMock.Setup(x => x.FindFileInfo("masked.flac")).Returns((Filename: path, Size: 3));

            var r = locator.Resolve("c1");

            Assert.NotNull(r);
            Assert.Equal(path, r.AbsolutePath);
            Assert.Equal(3, r.Length);
            Assert.Equal("audio/mpeg", r.ContentType);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void Resolve_DetectsMimeType()
    {
        var path = Path.Combine(Path.GetTempPath(), "ContentLoc_" + Guid.NewGuid().ToString("N")[..8] + ".flac");
        try
        {
            File.WriteAllBytes(path, new byte[] { 1 });
            var locator = CreateLocator();
            _repoMock.Setup(x => x.FindContentItem("c1")).Returns(("Music", "w1", "masked.flac", true, "", 0L));
            _repoMock.Setup(x => x.FindFileInfo("masked.flac")).Returns((Filename: path, Size: 1));

            var r = locator.Resolve("c1");

            Assert.NotNull(r);
            Assert.Equal("audio/flac", r.ContentType);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
