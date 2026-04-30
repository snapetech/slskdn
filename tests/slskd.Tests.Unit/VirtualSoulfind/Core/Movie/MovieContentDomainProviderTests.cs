// <copyright file="MovieContentDomainProviderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.VirtualSoulfind.Core.Movie;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core.Movie;
using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Unit tests for MovieContentDomainProvider.
/// </summary>
public class MovieContentDomainProviderTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<MovieContentDomainProvider>> _loggerMock;
    private readonly MovieContentDomainProvider _provider;

    public MovieContentDomainProviderTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<MovieContentDomainProvider>>();
        _provider = new MovieContentDomainProvider(_loggerMock.Object);
    }

    [Fact]
    public async Task TryGetWorkByImdbIdAsync_Should_Return_Work()
    {
        // Arrange
        var imdbId = "tt1234567";

        // Act
        var result = await _provider.TryGetWorkByImdbIdAsync(imdbId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(imdbId, result.ImdbId);
    }

    [Fact]
    public async Task TryGetWorkByTitleYearAsync_Should_Return_Work()
    {
        // Arrange
        var title = "Test Movie";
        var year = 2020;

        // Act
        var result = await _provider.TryGetWorkByTitleYearAsync(title, year, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
        Assert.Equal(year, result.Year);
    }

    [Fact]
    public async Task TryGetItemByHashAsync_Should_Return_Item()
    {
        // Arrange
        var hash = "abc123def456";
        var filename = "Test.Movie.2020.mkv";
        var sizeBytes = 1024 * 1024 * 1024L; // 1 GB

        // Act
        var result = await _provider.TryGetItemByHashAsync(hash, filename, sizeBytes, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(filename, result.Filename);
        Assert.Equal(hash, result.Hash);
    }

    [Fact]
    public async Task TryGetItemByLocalMetadataAsync_Should_Return_Item()
    {
        // Arrange
        var metadata = new LocalFileMetadata
        {
            Id = "Test.Movie.2020.mkv",
            SizeBytes = 1024 * 1024 * 1024L,
            PrimaryHash = "abc123",
        };

        // Act
        var result = await _provider.TryGetItemByLocalMetadataAsync(metadata, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test.Movie.2020.mkv", result.Filename);
    }

    [Fact]
    public async Task TryGetItemByLocalMetadataAsync_Should_Handle_Null_Metadata()
    {
        // Arrange
        LocalFileMetadata? metadata = null;

        // Act
        var result = await _provider.TryGetItemByLocalMetadataAsync(metadata!, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
