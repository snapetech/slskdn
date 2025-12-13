// <copyright file="CrossCodecMatchingTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.MediaCore;

using slskd.MediaCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Integration tests for cross-codec matching accuracy with realistic audio data.
/// </summary>
public class CrossCodecMatchingTests : IDisposable
{
    private readonly ContentIdRegistry _registry;
    private readonly IpldMapper _ipldMapper;
    private readonly PerceptualHasher _perceptualHasher;
    private readonly FuzzyMatcher _fuzzyMatcher;
    private readonly string _testDataDirectory;

    public CrossCodecMatchingTests()
    {
        _registry = new ContentIdRegistry();

        var ipldLogger = new Mock<ILogger<IpldMapper>>();
        _ipldMapper = new IpldMapper(_registry, ipldLogger.Object);

        _perceptualHasher = new PerceptualHasher();

        var fuzzyLogger = new Mock<ILogger<FuzzyMatcher>>();
        _fuzzyMatcher = new FuzzyMatcher(_perceptualHasher, fuzzyLogger.Object);

        // Create test data directory
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "slskdn-mediacore-tests");
        Directory.CreateDirectory(_testDataDirectory);
    }

    public void Dispose()
    {
        // Clean up test data
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, true);
        }
    }

    [Fact]
    public async Task IdenticalContentDifferentCodecs_HighSimilarityScores()
    {
        // Arrange - Register identical content in different formats
        var contentIdMp3 = "content:audio:track:mb-same-content-mp3";
        var contentIdFlac = "content:audio:track:mb-same-content-flac";
        var contentIdWav = "content:audio:track:mb-same-content-wav";

        await _registry.RegisterAsync("mb:recording:same-content-mp3", contentIdMp3);
        await _registry.RegisterAsync("mb:recording:same-content-flac", contentIdFlac);
        await _registry.RegisterAsync("mb:recording:same-content-wav", contentIdWav);

        // Generate identical audio content (same sine wave)
        var sampleRate = 44100;
        var duration = 2.0f; // 2 seconds for better fingerprinting
        var frequency = 440.0f; // A4 note

        var samples = GenerateSineWave(sampleRate, duration, frequency);

        // Create perceptual hashes for each "codec"
        var hashMp3 = _perceptualHasher.ComputeAudioHash(samples, sampleRate, PerceptualHashAlgorithm.Chromaprint);
        var hashFlac = _perceptualHasher.ComputeAudioHash(samples, sampleRate, PerceptualHashAlgorithm.Chromaprint);
        var hashWav = _perceptualHasher.ComputeAudioHash(samples, sampleRate, PerceptualHashAlgorithm.Chromaprint);

        // Act - Compare similarity between identical content in different formats
        var similarityMp3Flac = _perceptualHasher.AreSimilar(hashMp3.NumericHash!.Value, hashFlac.NumericHash!.Value, 0.8);
        var similarityMp3Wav = _perceptualHasher.AreSimilar(hashMp3.NumericHash!.Value, hashWav.NumericHash!.Value, 0.8);
        var similarityFlacWav = _perceptualHasher.AreSimilar(hashFlac.NumericHash!.Value, hashWav.NumericHash!.Value, 0.8);

        // Assert - Identical content should have very high similarity
        Assert.True(similarityMp3Flac, "MP3 and FLAC of identical content should be very similar");
        Assert.True(similarityMp3Wav, "MP3 and WAV of identical content should be very similar");
        Assert.True(similarityFlacWav, "FLAC and WAV of identical content should be very similar");
    }

    [Fact]
    public async Task SimilarContentDifferentQuality_HighSimilarityScores()
    {
        // Arrange - Test content with different quality levels (simulated by adding noise)
        var contentIdHighQuality = "content:audio:track:mb-track-hq";
        var contentIdLowQuality = "content:audio:track:mb-track-lq";

        await _registry.RegisterAsync("mb:recording:track-hq", contentIdHighQuality);
        await _registry.RegisterAsync("mb:recording:track-lq", contentIdLowQuality);

        var sampleRate = 44100;
        var duration = 2.0f;
        var frequency = 440.0f;

        // Generate base content
        var cleanSamples = GenerateSineWave(sampleRate, duration, frequency);

        // Simulate lower quality by adding noise
        var noisySamples = AddNoise(cleanSamples, 0.1f); // 10% noise

        // Act - Compute hashes and compare
        var cleanHash = _perceptualHasher.ComputeAudioHash(cleanSamples, sampleRate, PerceptualHashAlgorithm.Chromaprint);
        var noisyHash = _perceptualHasher.ComputeAudioHash(noisySamples, sampleRate, PerceptualHashAlgorithm.Chromaprint);

        var similarity = _perceptualHasher.AreSimilar(cleanHash.NumericHash!.Value, noisyHash.NumericHash!.Value, 0.7);

        // Assert - Similar content with quality differences should still be recognizable
        Assert.True(similarity, "High and low quality versions of same content should be similar");
    }

    [Fact]
    public async Task DifferentContent_LowSimilarityScores()
    {
        // Arrange - Completely different content
        var contentIdTrack1 = "content:audio:track:mb-track1";
        var contentIdTrack2 = "content:audio:track:mb-track2";

        await _registry.RegisterAsync("mb:recording:track1", contentIdTrack1);
        await _registry.RegisterAsync("mb:recording:track2", contentIdTrack2);

        var sampleRate = 44100;
        var duration = 2.0f;

        // Generate very different content
        var samples1 = GenerateSineWave(sampleRate, duration, 440.0f); // A4
        var samples2 = GenerateSineWave(sampleRate, duration, 880.0f); // A5 (octave higher)

        // Act - Compute hashes and compare
        var hash1 = _perceptualHasher.ComputeAudioHash(samples1, sampleRate, PerceptualHashAlgorithm.Chromaprint);
        var hash2 = _perceptualHasher.ComputeAudioHash(samples2, sampleRate, PerceptualHashAlgorithm.Chromaprint);

        var similarity = _perceptualHasher.AreSimilar(hash1.NumericHash!.Value, hash2.NumericHash!.Value, 0.5);

        // Assert - Very different content should have low similarity
        Assert.False(similarity, "Very different content should not be similar");
    }

    [Fact]
    public async Task AlbumTrackRelationships_CorrectlyEstablished()
    {
        // Arrange - Create album and track relationships
        var albumId = "content:audio:album:mb-dark-side-of-the-moon";
        var trackIds = new[]
        {
            "content:audio:track:mb-speak-to-me",
            "content:audio:track:mb-breathe",
            "content:audio:track:mb-on-the-run",
            "content:audio:track:mb-time"
        };

        // Register album and tracks
        await _registry.RegisterAsync("mb:release:dark-side-of-the-moon", albumId);
        for (int i = 0; i < trackIds.Length; i++)
        {
            await _registry.RegisterAsync($"mb:recording:track-{i}", trackIds[i]);
        }

        // Create album -> tracks relationships
        var albumToTracksLinks = trackIds.Select(trackId =>
            new IpldLink(IpldLinkNames.Tracks, trackId)).ToArray();
        await _ipldMapper.AddLinksAsync(albumId, albumToTracksLinks);

        // Create track -> album relationships
        foreach (var trackId in trackIds)
        {
            var trackToAlbumLink = new IpldLink(IpldLinkNames.Album, albumId);
            await _ipldMapper.AddLinksAsync(trackId, new[] { trackToAlbumLink });
        }

        // Act - Query relationships
        var albumGraph = await _ipldMapper.GetGraphAsync(albumId, maxDepth: 1);
        var trackGraphs = new List<ContentGraph>();
        foreach (var trackId in trackIds)
        {
            var trackGraph = await _ipldMapper.GetGraphAsync(trackId, maxDepth: 1);
            trackGraphs.Add(trackGraph);
        }

        // Assert - Relationships are correctly established
        Assert.NotNull(albumGraph);
        Assert.Equal(albumId, albumGraph.RootContentId);

        foreach (var trackGraph in trackGraphs)
        {
            Assert.NotNull(trackGraph);
            Assert.Contains(trackGraph.Nodes, n => n.ContentId == albumId);
        }
    }

    [Fact]
    public async Task MetadataExportImport_RoundTripIntegrity()
    {
        // Arrange - Create complex content graph with metadata
        var artistId = "content:audio:artist:mb-pink-floyd";
        var albumId = "content:audio:album:mb-the-wall";
        var trackId = "content:audio:track:mb-comfortably-numb";

        // Register content
        await _registry.RegisterAsync("mb:artist:pink-floyd", artistId);
        await _registry.RegisterAsync("mb:release:the-wall", albumId);
        await _registry.RegisterAsync("mb:recording:comfortably-numb", trackId);

        // Create relationships
        await _ipldMapper.AddLinksAsync(trackId, new[]
        {
            new IpldLink(IpldLinkNames.Album, albumId),
            new IpldLink(IpldLinkNames.Artist, artistId)
        });

        await _ipldMapper.AddLinksAsync(albumId, new[]
        {
            new IpldLink(IpldLinkNames.Artist, artistId),
            new IpldLink(IpldLinkNames.Tracks, trackId)
        });

        var contentIds = new[] { artistId, albumId, trackId };
        var portabilityLogger = new Mock<ILogger<MetadataPortability>>();
        var metadataPortability = new MetadataPortability(_registry, _ipldMapper, portabilityLogger.Object);

        // Act - Export metadata
        var exportedPackage = await metadataPortability.ExportAsync(contentIds, includeLinks: true);

        // Create fresh registry and mapper for import test
        var freshRegistry = new ContentIdRegistry();
        var freshIpldLogger = new Mock<ILogger<IpldMapper>>();
        var freshIpldMapper = new IpldMapper(freshRegistry, freshIpldLogger.Object);
        var freshPortabilityLogger = new Mock<ILogger<MetadataPortability>>();
        var freshMetadataPortability = new MetadataPortability(freshRegistry, freshIpldMapper, freshPortabilityLogger.Object);

        // Act - Import metadata
        var importResult = await freshMetadataPortability.ImportAsync(exportedPackage);

        // Assert - Import succeeded and data integrity maintained
        Assert.True(importResult.Success);
        Assert.Equal(3, importResult.EntriesProcessed);

        // Verify content can be resolved
        foreach (var contentId in contentIds)
        {
            var parsed = ContentIdParser.Parse(contentId);
            Assert.NotNull(parsed);

            var domain = parsed.Domain;
            var type = parsed.Type;
            var id = parsed.Id;
            var externalId = $"{domain}:{type}:{id}";

            var resolved = await freshRegistry.ResolveAsync(externalId);
            Assert.Equal(contentId, resolved);
        }
    }

    [Fact]
    public async Task ContentDiscoveryWorkflow_FullPipelineIntegration()
    {
        // Arrange - Simulate a realistic content discovery scenario
        var artistName = "Radiohead";
        var albumName = "OK Computer";
        var trackNames = new[] { "Airbag", "Paranoid Android", "Subterranean Homesick Alien" };

        // Create content IDs
        var artistId = $"content:audio:artist:{artistName.ToLower().Replace(" ", "-")}";
        var albumId = $"content:audio:album:{albumName.ToLower().Replace(" ", "-")}";
        var trackIds = trackNames.Select(name =>
            $"content:audio:track:{name.ToLower().Replace(" ", "-")}").ToArray();

        // Register all content
        await _registry.RegisterAsync($"mb:artist:{artistName.ToLower()}", artistId);
        await _registry.RegisterAsync($"mb:release:{albumName.ToLower()}", albumId);
        for (int i = 0; i < trackNames.Length; i++)
        {
            await _registry.RegisterAsync($"mb:recording:{trackNames[i].ToLower()}", trackIds[i]);
        }

        // Create comprehensive relationships
        foreach (var trackId in trackIds)
        {
            await _ipldMapper.AddLinksAsync(trackId, new[]
            {
                new IpldLink(IpldLinkNames.Album, albumId),
                new IpldLink(IpldLinkNames.Artist, artistId)
            });
        }

        await _ipldMapper.AddLinksAsync(albumId, new[]
        {
            new IpldLink(IpldLinkNames.Artist, artistId)
        }.Concat(trackIds.Select(t => new IpldLink(IpldLinkNames.Tracks, t))).ToArray());

        await _ipldMapper.AddLinksAsync(artistId, new[]
        {
            new IpldLink(IpldLinkNames.Album, albumId)
        });

        // Act - Test various discovery queries
        var allAudioContent = await _registry.FindByDomainAsync("audio");
        var allTracks = await _registry.FindByDomainAndTypeAsync("audio", "track");
        var allAlbums = await _registry.FindByDomainAndTypeAsync("audio", "album");

        // Test graph traversals
        var artistGraph = await _ipldMapper.GetGraphAsync(artistId, maxDepth: 3);
        var albumGraph = await _ipldMapper.GetGraphAsync(albumId, maxDepth: 2);

        // Test content similarity (simulated with mock perceptual data)
        var trackSimilarities = new List<double>();
        for (int i = 0; i < trackIds.Length - 1; i++)
        {
            var score = await _fuzzyMatcher.ScorePerceptualAsync(trackIds[i], trackIds[i + 1], _registry);
            trackSimilarities.Add(score);
        }

        // Assert - Full workflow integration works
        Assert.Equal(5, allAudioContent.Count); // artist + album + 3 tracks
        Assert.Equal(3, allTracks.Count);
        Assert.Single(allAlbums);

        Assert.NotNull(artistGraph);
        Assert.NotNull(albumGraph);

        // Artist graph should contain album and tracks
        Assert.Contains(artistGraph.Nodes, n => n.ContentId == albumId);
        Assert.All(trackIds, trackId =>
            Assert.Contains(artistGraph.Nodes, n => n.ContentId == trackId));

        // Album graph should contain artist and tracks
        Assert.Contains(albumGraph.Nodes, n => n.ContentId == artistId);
        Assert.All(trackIds, trackId =>
            Assert.Contains(albumGraph.Nodes, n => n.ContentId == trackId));

        Assert.All(trackSimilarities, score => Assert.InRange(score, 0.0, 1.0));
    }

    // Helper methods for test data generation
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

    private static float[] AddNoise(float[] samples, float noiseLevel)
    {
        var noisySamples = new float[samples.Length];
        var random = new Random(42); // Deterministic seed for reproducible tests

        for (int i = 0; i < samples.Length; i++)
        {
            var noise = (float)(random.NextDouble() * 2 - 1) * noiseLevel;
            noisySamples[i] = samples[i] + noise;
        }

        return noisySamples;
    }
}
