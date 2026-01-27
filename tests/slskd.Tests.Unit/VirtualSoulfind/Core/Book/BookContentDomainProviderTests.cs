// <copyright file="BookContentDomainProviderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Core.Book;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core.Book;
using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Unit tests for BookContentDomainProvider.
/// </summary>
public class BookContentDomainProviderTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<BookContentDomainProvider>> _loggerMock;
    private readonly BookContentDomainProvider _provider;

    public BookContentDomainProviderTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<BookContentDomainProvider>>();
        _provider = new BookContentDomainProvider(_loggerMock.Object);
    }

    [Fact]
    public async Task TryGetWorkByIsbnAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var isbn = "9780132350884";

        // Act
        var result = await _provider.TryGetWorkByIsbnAsync(isbn, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetWorkByTitleAuthorAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var title = "Test Book";
        var author = "Test Author";

        // Act
        var result = await _provider.TryGetWorkByTitleAuthorAsync(title, author, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetItemByHashAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var hash = "abc123def456";
        var filename = "test.pdf";
        var sizeBytes = 5 * 1024 * 1024L;

        // Act
        var result = await _provider.TryGetItemByHashAsync(hash, filename, sizeBytes, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetItemByLocalMetadataAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var metadata = new LocalFileMetadata
        {
            Id = "test-id",
            SizeBytes = 5 * 1024 * 1024L,
            PrimaryHash = "abc123",
            MediaInfo = "Book: PDF",
        };

        // Act
        var result = await _provider.TryGetItemByLocalMetadataAsync(metadata, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("test.pdf", BookFormat.Pdf)]
    [InlineData("test.epub", BookFormat.Epub)]
    [InlineData("test.mobi", BookFormat.Mobi)]
    [InlineData("test.azw", BookFormat.Azw)]
    [InlineData("test.azw3", BookFormat.Azw)]
    [InlineData("test.fb2", BookFormat.Fb2)]
    [InlineData("test.txt", BookFormat.Txt)]
    [InlineData("test.doc", BookFormat.Doc)]
    [InlineData("test.docx", BookFormat.Docx)]
    [InlineData("test.unknown", BookFormat.Unknown)]
    public void DetectFormat_Should_Map_Known_Extensions(string filename, BookFormat expected)
    {
        // Arrange & Act
        var result = _provider.DetectFormat(filename);

        // Assert
        Assert.Equal(expected, result);
    }
}
