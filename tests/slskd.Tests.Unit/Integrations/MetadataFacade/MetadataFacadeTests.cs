// <copyright file="MetadataFacadeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MetadataFacade;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Integrations.AcoustId;
using slskd.Integrations.Chromaprint;
using slskd.Integrations.MetadataFacade;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;
using slskd.Tests.Unit;
using Xunit;

public sealed class MetadataFacadeTests
{
    [Fact]
    public async Task SearchAsync_YieldsTitleArtistHitsWithoutRecordingId()
    {
        var mb = new Mock<IMusicBrainzClient>();
        mb.Setup(client => client.SearchRecordingsAsync("artist title", 10, default))
            .ReturnsAsync(new[]
            {
                new RecordingSearchHit(string.Empty, "Title", "Artist", null),
            });

        var facade = CreateFacade(mb.Object);

        var results = await ToListAsync(facade.SearchAsync(" artist title ", 10));

        var result = Assert.Single(results);
        Assert.Equal("Artist", result.Artist);
        Assert.Equal("Title", result.Title);
        Assert.Null(result.MusicBrainzRecordingId);
    }

    [Fact]
    public async Task GetByFileAsync_FallsBackToFilenameMetadataWhenTagParsingFails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "slskdn-metadata-facade-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "Test Artist - Test Title.mp3");
        await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());

        try
        {
            var facade = CreateFacade();

            var result = await facade.GetByFileAsync(filePath);

            Assert.NotNull(result);
            Assert.Equal("Test Artist", result!.Artist);
            Assert.Equal("Test Title", result.Title);
            Assert.Equal(MetadataResult.SourceFileTags, result.Source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static MetadataFacade CreateFacade(IMusicBrainzClient? musicBrainzClient = null)
    {
        var options = new slskd.Options
        {
            Integration = new slskd.Options.IntegrationOptions
            {
                AcoustId = new slskd.Options.IntegrationOptions.AcoustIdOptions
                {
                    Enabled = false,
                },
                Chromaprint = new slskd.Options.IntegrationOptions.ChromaprintOptions
                {
                    Enabled = false,
                },
            },
        };

        return new MetadataFacade(
            musicBrainzClient ?? Mock.Of<IMusicBrainzClient>(),
            Mock.Of<IAcoustIdClient>(),
            Mock.Of<IFingerprintExtractionService>(),
            new TestOptionsMonitor<slskd.Options>(options),
            Mock.Of<ILogger<MetadataFacade>>(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static async Task<List<MetadataResult>> ToListAsync(IAsyncEnumerable<MetadataResult> source)
    {
        var results = new List<MetadataResult>();
        await foreach (var item in source.ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }
}
