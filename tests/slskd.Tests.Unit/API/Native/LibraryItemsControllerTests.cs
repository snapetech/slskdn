// <copyright file="LibraryItemsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Native;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.API.Native;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Shares;
using Soulseek;
using Xunit;

/// <summary>
/// Unit tests for LibraryItemsController (library search API for E2E and Collections UI).
/// </summary>
public class LibraryItemsControllerTests
{
    private readonly Mock<IShareService> shareServiceMock;
    private readonly Mock<IHashDbService> hashDbServiceMock;
    private readonly Mock<ILogger<LibraryItemsController>> loggerMock;
    private readonly LibraryItemsController controller;

    public LibraryItemsControllerTests()
    {
        shareServiceMock = new Mock<IShareService>();
        hashDbServiceMock = new Mock<IHashDbService>();
        loggerMock = new Mock<ILogger<LibraryItemsController>>();

        controller = new LibraryItemsController(
            shareServiceMock.Object,
            hashDbServiceMock.Object,
            loggerMock.Object);

        // Set up controller context with authenticated user (required for [Authorize])
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }, "test"))
            }
        };
    }

    [Fact]
    public async Task SearchItems_NoQuery_ReturnsAllFiles()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Music/song1.mp3", 1024, ".mp3"),
                new Soulseek.File(2, "Music/song2.flac", 2048, ".flac")
            }),
            new Soulseek.Directory("Movies", new List<Soulseek.File>
            {
                new Soulseek.File(3, "Movies/movie.mp4", 4096, ".mp4")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act
        var result = await controller.SearchItems(query: null, kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        Assert.NotNull(itemsProp);

        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task SearchItems_WithQuery_FiltersByFilename()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Music/sintel.mp3", 1024, ".mp3"),
                new Soulseek.File(2, "Music/other.mp3", 2048, ".mp3")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act
        var result = await controller.SearchItems(query: "sintel", kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Single(items);

        var itemType = items[0].GetType();
        var fileNameProp = itemType.GetProperty("FileName");
        var fileName = fileNameProp?.GetValue(items[0]) as string;
        Assert.Contains("sintel", fileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchItems_WithKinds_FiltersByMediaKind()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Media", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Media/song.mp3", 1024, ".mp3"),
                new Soulseek.File(2, "Media/movie.mp4", 2048, ".mp4"),
                new Soulseek.File(3, "Media/book.txt", 512, ".txt")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act
        var result = await controller.SearchItems(query: null, kinds: "Audio", limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Single(items);

        var itemType = items[0].GetType();
        var mediaKindProp = itemType.GetProperty("MediaKind");
        var mediaKind = mediaKindProp?.GetValue(items[0]) as string;
        Assert.Equal("Audio", mediaKind);
    }

    [Fact]
    public async Task SearchItems_WithMultipleKinds_ReturnsMatchingFiles()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Media", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Media/song.mp3", 1024, ".mp3"),
                new Soulseek.File(2, "Media/movie.mp4", 2048, ".mp4"),
                new Soulseek.File(3, "Media/book.txt", 512, ".txt")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act
        var result = await controller.SearchItems(query: null, kinds: "Audio,Video", limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task SearchItems_WithLimit_RespectsLimit()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", Enumerable.Range(1, 10)
                .Select(i => new Soulseek.File(i, $"Music/song{i}.mp3", 1024, ".mp3"))
                .ToList())
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act
        var result = await controller.SearchItems(query: null, kinds: null, limit: 5, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public async Task SearchItems_EmptyShares_ReturnsEmptyList()
    {
        // Arrange
        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(new List<Soulseek.Directory>());

        // Act
        var result = await controller.SearchItems(query: null, kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task SearchItems_WithSha256FromHashDb_UsesSha256ContentId()
    {
        // Arrange
        var testSha256 = "abc123def456";
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Music/song.mp3", 1024, ".mp3")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.mp3");
        try
        {
            System.IO.File.WriteAllBytes(testFilePath, new byte[] { 1, 2, 3 });

            shareServiceMock
                .Setup(x => x.ResolveFileAsync("Music/song.mp3"))
                .ReturnsAsync(("local", testFilePath, 1024L));

            var flacKey = HashDbEntry.GenerateFlacKey(testFilePath, 1024);
            hashDbServiceMock
                .Setup(x => x.LookupHashAsync(flacKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashDbEntry
                {
                    FileSha256 = testSha256
                });

            // Act
            var result = await controller.SearchItems(query: null, kinds: null, limit: 100, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseType = okResult.Value.GetType();
            var itemsProp = responseType.GetProperty("items");
            var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
            Assert.NotNull(items);
            Assert.Single(items);

            var itemType = items[0].GetType();
            var contentIdProp = itemType.GetProperty("ContentId");
            var contentId = contentIdProp?.GetValue(items[0]) as string;
            Assert.Equal($"sha256:{testSha256}", contentId);

            var sha256Prop = itemType.GetProperty("Sha256");
            var sha256 = sha256Prop?.GetValue(items[0]) as string;
            Assert.Equal(testSha256, sha256);
        }
        finally
        {
            try { System.IO.File.Delete(testFilePath); } catch { }
        }
    }

    [Fact]
    public async Task SearchItems_OnError_Returns500()
    {
        // Arrange
        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ThrowsAsync(new Exception("Test error"));

        // Act
        var result = await controller.SearchItems(query: null, kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetItem_ValidContentId_ReturnsItem()
    {
        // Arrange
        var testSha256 = "abc123def456";
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Music/song.mp3", 1024, ".mp3")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.mp3");
        try
        {
            System.IO.File.WriteAllBytes(testFilePath, new byte[] { 1, 2, 3 });

            shareServiceMock
                .Setup(x => x.ResolveFileAsync("Music/song.mp3"))
                .ReturnsAsync(("local", testFilePath, 1024L));

            var flacKey = HashDbEntry.GenerateFlacKey(testFilePath, 1024);
            hashDbServiceMock
                .Setup(x => x.LookupHashAsync(flacKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashDbEntry
                {
                    FileSha256 = testSha256
                });

            var contentId = $"sha256:{testSha256}";

            // Act
            var result = await controller.GetItem(contentId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var itemType = okResult.Value.GetType();
            var contentIdProp = itemType.GetProperty("ContentId");
            var returnedContentId = contentIdProp?.GetValue(okResult.Value) as string;
            Assert.Equal(contentId, returnedContentId);
        }
        finally
        {
            try { System.IO.File.Delete(testFilePath); } catch { }
        }
    }

    [Fact]
    public async Task GetItem_InvalidContentId_ReturnsNotFound()
    {
        // Arrange
        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(new List<Soulseek.Directory>());

        // Act
        var result = await controller.GetItem("sha256:nonexistent", CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var responseType = notFoundResult.Value.GetType();
        var errorProp = responseType.GetProperty("error");
        var error = errorProp?.GetValue(notFoundResult.Value) as string;
        Assert.Equal("Item not found", error);
    }

    [Fact]
    public async Task GetItem_OnError_Returns500()
    {
        // Arrange
        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ThrowsAsync(new Exception("Test error"));

        // Act
        var result = await controller.GetItem("sha256:test", CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task SearchItems_QueryCaseInsensitive_MatchesCorrectly()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Music/SINTEL.mp3", 1024, ".mp3"),
                new Soulseek.File(2, "Music/other.mp3", 2048, ".mp3")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act - lowercase query should match uppercase filename
        var result = await controller.SearchItems(query: "sintel", kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Single(items);
    }

    [Fact]
    public async Task SearchItems_MediaKindMapping_CorrectlyIdentifiesKinds()
    {
        // Arrange
        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Media", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Media/song.mp3", 1024, ".mp3"),
                new Soulseek.File(2, "Media/song.flac", 2048, ".flac"),
                new Soulseek.File(3, "Media/song.ogg", 512, ".ogg"),
                new Soulseek.File(4, "Media/movie.mp4", 4096, ".mp4"),
                new Soulseek.File(5, "Media/movie.mkv", 8192, ".mkv"),
                new Soulseek.File(6, "Media/book.txt", 256, ".txt"),
                new Soulseek.File(7, "Media/book.pdf", 512, ".pdf"),
                new Soulseek.File(8, "Media/unknown.xyz", 128, ".xyz")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        shareServiceMock
            .Setup(x => x.ResolveFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string filename) => ("local", filename, 1024L));

        // Act
        var result = await controller.SearchItems(query: null, kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Equal(8, items.Count);

        var itemType = items[0].GetType();
        var mediaKindProp = itemType.GetProperty("MediaKind");

        // Check Audio files
        var audioFiles = items.Where(i => 
        {
            var fileName = (itemType.GetProperty("FileName")?.GetValue(i) as string) ?? string.Empty;
            return fileName.Contains("song");
        }).ToList();
        foreach (var item in audioFiles)
        {
            var mediaKind = mediaKindProp?.GetValue(item) as string;
            Assert.Equal("Audio", mediaKind);
        }

        // Check Video files
        var videoFiles = items.Where(i =>
        {
            var fileName = (itemType.GetProperty("FileName")?.GetValue(i) as string) ?? string.Empty;
            return fileName.Contains("movie");
        }).ToList();
        foreach (var item in videoFiles)
        {
            var mediaKind = mediaKindProp?.GetValue(item) as string;
            Assert.Equal("Video", mediaKind);
        }

        // Check Book files
        var bookFiles = items.Where(i =>
        {
            var fileName = (itemType.GetProperty("FileName")?.GetValue(i) as string) ?? string.Empty;
            return fileName.Contains("book");
        }).ToList();
        foreach (var item in bookFiles)
        {
            var mediaKind = mediaKindProp?.GetValue(item) as string;
            Assert.Equal("Book", mediaKind);
        }

        // Check unknown file
        var unknownFile = items.FirstOrDefault(i =>
        {
            var fileName = (itemType.GetProperty("FileName")?.GetValue(i) as string) ?? string.Empty;
            return fileName.Contains("unknown");
        });
        Assert.NotNull(unknownFile);
        var unknownMediaKind = mediaKindProp?.GetValue(unknownFile) as string;
        Assert.Equal("File", unknownMediaKind);
    }

    [Fact]
    public async Task SearchItems_WithoutHashDb_GeneratesPathBasedContentId()
    {
        // Arrange - controller without HashDb service
        var controllerWithoutHashDb = new LibraryItemsController(
            shareServiceMock.Object,
            hashDbService: null, // No HashDb
            loggerMock.Object);

        controllerWithoutHashDb.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }, "test"))
            }
        };

        var directories = new List<Soulseek.Directory>
        {
            new Soulseek.Directory("Music", new List<Soulseek.File>
            {
                new Soulseek.File(1, "Music/song.mp3", 1024, ".mp3")
            })
        };

        shareServiceMock
            .Setup(x => x.BrowseAsync(It.IsAny<slskd.Shares.Share>()))
            .ReturnsAsync(directories);

        // Use a path that doesn't exist to test path-based fallback (when file doesn't exist, SHA256 can't be computed)
        var testFilePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.mp3");

        shareServiceMock
            .Setup(x => x.ResolveFileAsync("Music/song.mp3"))
            .ReturnsAsync(("local", testFilePath, 1024L));

        // Act
        var result = await controllerWithoutHashDb.SearchItems(query: null, kinds: null, limit: 100, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var itemsProp = responseType.GetProperty("items");
        var items = (itemsProp.GetValue(okResult.Value) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        Assert.NotNull(items);
        Assert.Single(items);

        var itemType = items[0].GetType();
        var contentIdProp = itemType.GetProperty("ContentId");
        var contentId = contentIdProp?.GetValue(items[0]) as string;
        Assert.StartsWith("path:", contentId); // Should use path-based fallback when file doesn't exist
    }
}
