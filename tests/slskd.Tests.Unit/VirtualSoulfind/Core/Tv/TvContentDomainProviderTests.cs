// <copyright file="TvContentDomainProviderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Core.Tv;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core.Tv;
using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Unit tests for TvContentDomainProvider.
/// </summary>
public class TvContentDomainProviderTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<TvContentDomainProvider>> _loggerMock;
    private readonly TvContentDomainProvider _provider;

    public TvContentDomainProviderTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<TvContentDomainProvider>>();
        _provider = new TvContentDomainProvider(_loggerMock.Object);
    }

    [Fact]
    public async Task TryGetWorkByTvdbIdAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var tvdbId = "123456";

        // Act
        var result = await _provider.TryGetWorkByTvdbIdAsync(tvdbId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetWorkByTitleAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var title = "Test Series";

        // Act
        var result = await _provider.TryGetWorkByTitleAsync(title, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetItemByEpisodeAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var seriesId = "series-123";
        var season = 1;
        var episode = 1;

        // Act
        var result = await _provider.TryGetItemByEpisodeAsync(seriesId, season, episode, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetItemByHashAsync_Should_Return_Null_For_Placeholder()
    {
        // Arrange
        var hash = "abc123def456";
        var filename = "test.mkv";
        var sizeBytes = 1024 * 1024 * 1024L;

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
            SizeBytes = 1024 * 1024 * 1024L,
            PrimaryHash = "abc123",
        };

        // Act
        var result = await _provider.TryGetItemByLocalMetadataAsync(metadata, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
