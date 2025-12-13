// <copyright file="MediaCorePerformanceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.MediaCore;

using slskd.MediaCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Performance and scalability tests for MediaCore components.
/// </summary>
public class MediaCorePerformanceTests
{
    private readonly ContentIdRegistry _registry;
    private readonly IpldMapper _ipldMapper;
    private readonly PerceptualHasher _perceptualHasher;
    private readonly FuzzyMatcher _fuzzyMatcher;

    public MediaCorePerformanceTests()
    {
        _registry = new ContentIdRegistry();

        var ipldLogger = new Mock<ILogger<IpldMapper>>();
        _ipldMapper = new IpldMapper(_registry, ipldLogger.Object);

        _perceptualHasher = new PerceptualHasher();

        var fuzzyLogger = new Mock<ILogger<FuzzyMatcher>>();
        _fuzzyMatcher = new FuzzyMatcher(_perceptualHasher, fuzzyLogger.Object);
    }

    [Fact]
    public async Task ContentIdRegistry_BulkRegistration_PerformanceWithinLimits()
    {
        // Arrange - Prepare bulk registration data
        const int bulkSize = 1000;
        var contentIds = new List<string>();
        var externalIds = new List<string>();

        for (int i = 0; i < bulkSize; i++)
        {
            var contentId = $"content:audio:track:mb-bulk-{i:D6}";
            var externalId = $"mb:recording:bulk-{i:D6}";
            contentIds.Add(contentId);
            externalIds.Add(externalId);
        }

        // Act - Measure bulk registration performance
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < bulkSize; i++)
        {
            await _registry.RegisterAsync(externalIds[i], contentIds[i]);
        }
        stopwatch.Stop();

        // Assert - Performance within acceptable limits (should be fast)
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, // Less than 5 seconds for 1000 registrations
            $"Bulk registration took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");

        // Verify all registrations worked
        for (int i = 0; i < bulkSize; i++)
        {
            var resolved = await _registry.ResolveAsync(externalIds[i]);
            Assert.Equal(contentIds[i], resolved);
        }
    }

    [Fact]
    public async Task ContentIdRegistry_BulkQueries_PerformanceWithinLimits()
    {
        // Arrange - Set up test data
        const int dataSize = 500;
        var audioContentIds = new List<string>();
        var videoContentIds = new List<string>();

        for (int i = 0; i < dataSize; i++)
        {
            var audioId = $"content:audio:track:mb-audio-{i:D6}";
            var videoId = $"content:video:movie:imdb-video-{i:D6}";

            await _registry.RegisterAsync($"mb:recording:audio-{i:D6}", audioId);
            await _registry.RegisterAsync($"imdb:video-{i:D6}", videoId);

            audioContentIds.Add(audioId);
            videoContentIds.Add(videoId);
        }

        // Act - Measure query performance
        var audioQueryStopwatch = Stopwatch.StartNew();
        var audioResults = await _registry.FindByDomainAsync("audio");
        audioQueryStopwatch.Stop();

        var videoQueryStopwatch = Stopwatch.StartNew();
        var videoResults = await _registry.FindByDomainAsync("video");
        videoQueryStopwatch.Stop();

        // Assert - Query performance within limits
        Assert.True(audioQueryStopwatch.ElapsedMilliseconds < 1000,
            $"Audio domain query took {audioQueryStopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
        Assert.True(videoQueryStopwatch.ElapsedMilliseconds < 1000,
            $"Video domain query took {videoQueryStopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

        Assert.Equal(dataSize, audioResults.Count);
        Assert.Equal(dataSize, videoResults.Count);
    }

    [Fact]
    public async Task PerceptualHasher_BulkHashing_PerformanceWithinLimits()
    {
        // Arrange - Prepare test audio data
        const int batchSize = 50;
        var sampleRate = 44100;
        var duration = 1.0f;

        var audioSamples = new List<float[]>();
        for (int i = 0; i < batchSize; i++)
        {
            var frequency = 220.0f + (i * 10.0f); // Different frequencies
            var samples = GenerateSineWave(sampleRate, duration, frequency);
            audioSamples.Add(samples);
        }

        // Act - Measure bulk hashing performance
        var stopwatch = Stopwatch.StartNew();
        var hashes = new List<PerceptualHash>();
        foreach (var samples in audioSamples)
        {
            var hash = _perceptualHasher.ComputeAudioHash(samples, sampleRate, PerceptualHashAlgorithm.Chromaprint);
            hashes.Add(hash);
        }
        stopwatch.Stop();

        // Assert - Performance within limits (hashing should be reasonably fast)
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, // Less than 10 seconds for 50 hashes
            $"Bulk hashing took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");

        Assert.Equal(batchSize, hashes.Count);
        Assert.All(hashes, hash =>
        {
            Assert.NotNull(hash);
            Assert.Equal("ChromaPrint", hash.Algorithm);
            Assert.NotNull(hash.Hex);
            Assert.True(hash.NumericHash.HasValue);
        });
    }

    [Fact]
    public async Task IpldMapper_ComplexGraphOperations_PerformanceWithinLimits()
    {
        // Arrange - Create a complex graph structure
        const int nodesPerLevel = 10;
        const int levels = 3;

        // Create root node
        var rootId = "content:audio:artist:mb-complex-artist";
        await _registry.RegisterAsync("mb:artist:complex-artist", rootId);

        // Create albums under artist
        var albumIds = new List<string>();
        for (int album = 0; album < nodesPerLevel; album++)
        {
            var albumId = $"content:audio:album:mb-album-{album}";
            await _registry.RegisterAsync($"mb:release:album-{album}", albumId);
            albumIds.Add(albumId);

            // Link album to artist
            await _ipldMapper.AddLinksAsync(albumId, new[] { new IpldLink(IpldLinkNames.Artist, rootId) });
        }

        // Create tracks under albums
        foreach (var albumId in albumIds)
        {
            var trackIds = new List<string>();
            for (int track = 0; track < nodesPerLevel; track++)
            {
                var trackId = $"content:audio:track:mb-{albumId.Split('-').Last()}-track-{track}";
                await _registry.RegisterAsync($"mb:recording:{albumId.Split('-').Last()}-track-{track}", trackId);
                trackIds.Add(trackId);

                // Link track to album
                await _ipldMapper.AddLinksAsync(trackId, new[] { new IpldLink(IpldLinkNames.Album, albumId) });
            }

            // Link album to tracks
            var trackLinks = trackIds.Select(trackId => new IpldLink(IpldLinkNames.Tracks, trackId)).ToArray();
            await _ipldMapper.AddLinksAsync(albumId, trackLinks);
        }

        // Link artist to albums
        var albumLinks = albumIds.Select(albumId => new IpldLink(IpldLinkNames.Album, albumId)).ToArray();
        await _ipldMapper.AddLinksAsync(rootId, albumLinks);

        // Act - Measure graph traversal performance
        var stopwatch = Stopwatch.StartNew();
        var graph = await _ipldMapper.GetGraphAsync(rootId, maxDepth: levels);
        stopwatch.Stop();

        // Assert - Complex graph traversal within performance limits
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, // Less than 5 seconds for complex traversal
            $"Graph traversal took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");

        Assert.NotNull(graph);
        Assert.Equal(rootId, graph.RootContentId);

        // Should have traversed all levels (artist -> albums -> tracks)
        var totalExpectedNodes = 1 + nodesPerLevel + (nodesPerLevel * nodesPerLevel); // 1 + 10 + 100 = 111
        Assert.True(graph.Nodes.Count >= totalExpectedNodes,
            $"Expected at least {totalExpectedNodes} nodes, got {graph.Nodes.Count}");
    }

    [Fact]
    public async Task FuzzyMatcher_BulkSimilarityScoring_PerformanceWithinLimits()
    {
        // Arrange - Create test content set
        const int contentCount = 25;
        var contentIds = new List<string>();

        for (int i = 0; i < contentCount; i++)
        {
            var contentId = $"content:audio:track:mb-bulk-track-{i:D3}";
            await _registry.RegisterAsync($"mb:recording:bulk-track-{i:D3}", contentId);
            contentIds.Add(contentId);
        }

        // Act - Measure bulk similarity scoring
        var stopwatch = Stopwatch.StartNew();
        var similarityTasks = new List<Task<double>>();

        // Compare each pair (only upper triangle to avoid duplicates)
        for (int i = 0; i < contentCount; i++)
        {
            for (int j = i + 1; j < contentCount; j++)
            {
                var task = _fuzzyMatcher.ScorePerceptualAsync(contentIds[i], contentIds[j], _registry);
                similarityTasks.Add(task);
            }
        }

        var similarities = await Task.WhenAll(similarityTasks);
        stopwatch.Stop();

        var comparisonsMade = similarities.Length;

        // Assert - Performance within limits for bulk similarity scoring
        Assert.True(stopwatch.ElapsedMilliseconds < 15000, // Less than 15 seconds for ~300 comparisons
            $"Bulk similarity scoring took {stopwatch.ElapsedMilliseconds}ms for {comparisonsMade} comparisons, expected < 15000ms");

        Assert.Equal(comparisonsMade, similarities.Length);
        Assert.All(similarities, similarity => Assert.InRange(similarity, 0.0, 1.0));
    }

    [Fact]
    public async Task MetadataPortability_LargeDataset_ExportImportPerformance()
    {
        // Arrange - Create large dataset for portability testing
        const int datasetSize = 200;
        var contentIds = new List<string>();

        for (int i = 0; i < datasetSize; i++)
        {
            var contentId = $"content:audio:track:mb-large-dataset-{i:D6}";
            await _registry.RegisterAsync($"mb:recording:large-dataset-{i:D6}", contentId);
            contentIds.Add(contentId);
        }

        // Create some relationships
        for (int i = 0; i < datasetSize - 1; i += 2)
        {
            await _ipldMapper.AddLinksAsync(contentIds[i], new[]
            {
                new IpldLink("related", contentIds[i + 1])
            });
        }

        var portabilityLogger = new Mock<ILogger<MetadataPortability>>();
        var metadataPortability = new MetadataPortability(_registry, _ipldMapper, portabilityLogger.Object);

        // Act - Measure export performance
        var exportStopwatch = Stopwatch.StartNew();
        var exportedPackage = await metadataPortability.ExportAsync(contentIds, includeLinks: true);
        exportStopwatch.Stop();

        // Create fresh instances for import test
        var freshRegistry = new ContentIdRegistry();
        var freshIpldLogger = new Mock<ILogger<IpldMapper>>();
        var freshIpldMapper = new IpldMapper(freshRegistry, freshIpldLogger.Object);
        var freshPortabilityLogger = new Mock<ILogger<MetadataPortability>>();
        var freshMetadataPortability = new MetadataPortability(freshRegistry, freshIpldMapper, freshPortabilityLogger.Object);

        // Act - Measure import performance
        var importStopwatch = Stopwatch.StartNew();
        var importResult = await freshMetadataPortability.ImportAsync(exportedPackage);
        importStopwatch.Stop();

        // Assert - Large dataset operations within performance limits
        Assert.True(exportStopwatch.ElapsedMilliseconds < 10000, // Less than 10 seconds for export
            $"Export took {exportStopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
        Assert.True(importStopwatch.ElapsedMilliseconds < 15000, // Less than 15 seconds for import
            $"Import took {importStopwatch.ElapsedMilliseconds}ms, expected < 15000ms");

        Assert.True(importResult.Success);
        Assert.Equal(datasetSize, importResult.EntriesProcessed);
    }

    [Fact]
    public async Task ConcurrentOperations_ThreadSafetyAndPerformance()
    {
        // Arrange - Prepare concurrent operation data
        const int concurrentOperations = 50;
        var tasks = new List<Task>();

        // Act - Execute multiple operations concurrently
        var stopwatch = Stopwatch.StartNew();

        // Concurrent registrations
        for (int i = 0; i < concurrentOperations; i++)
        {
            var task = Task.Run(async () =>
            {
                var contentId = $"content:audio:track:mb-concurrent-{Guid.NewGuid()}";
                var externalId = $"mb:recording:concurrent-{Guid.NewGuid()}";
                await _registry.RegisterAsync(externalId, contentId);
            });
            tasks.Add(task);
        }

        // Concurrent queries
        for (int i = 0; i < concurrentOperations; i++)
        {
            var task = Task.Run(async () =>
            {
                await _registry.FindByDomainAsync("audio");
            });
            tasks.Add(task);
        }

        // Wait for all operations to complete
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - Concurrent operations completed within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, // Less than 10 seconds for concurrent operations
            $"Concurrent operations took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");

        // Verify final state
        var audioContent = await _registry.FindByDomainAsync("audio");
        Assert.True(audioContent.Count >= concurrentOperations,
            $"Expected at least {concurrentOperations} audio content items, got {audioContent.Count}");
    }

    // Helper method for test data generation
    private static float[] GenerateSineWave(int sampleRate, float durationSeconds, float frequency)
    {
        var numSamples = (int)(sampleRate * durationSeconds);
        var samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            var t = (float)i / sampleRate;
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * t);
        }

        return samples;
    }
}
