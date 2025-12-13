// <copyright file="MediaCoreIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.MediaCore;

using slskd.MediaCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using slskd.Mesh.Dht;

/// <summary>
/// Integration tests for MediaCore components working together.
/// </summary>
public class MediaCoreIntegrationTests
{
    private readonly ContentIdRegistry _registry;
    private readonly IpldMapper _ipldMapper;
    private readonly PerceptualHasher _perceptualHasher;
    private readonly FuzzyMatcher _fuzzyMatcher;
    private readonly MetadataPortability _metadataPortability;
    private readonly Mock<IMeshDhtClient> _dhtMock;
    private readonly Mock<ILogger<DescriptorRetriever>> _descriptorRetrieverLogger;
    private readonly IDescriptorRetriever _descriptorRetriever;

    public MediaCoreIntegrationTests()
    {
        _registry = new ContentIdRegistry();

        var ipldLogger = new Mock<ILogger<IpldMapper>>();
        _ipldMapper = new IpldMapper(_registry, ipldLogger.Object);

        _perceptualHasher = new PerceptualHasher();

        var fuzzyLogger = new Mock<ILogger<FuzzyMatcher>>();
        _fuzzyMatcher = new FuzzyMatcher(_perceptualHasher, fuzzyLogger.Object);

        var portabilityLogger = new Mock<ILogger<MetadataPortability>>();
        _metadataPortability = new MetadataPortability(_registry, _ipldMapper, portabilityLogger.Object);

        // Mock DHT client for descriptor retrieval tests
        _dhtMock = new Mock<IMeshDhtClient>();
        _descriptorRetrieverLogger = new Mock<ILogger<DescriptorRetriever>>();
        var descriptorValidatorMock = new Mock<IDescriptorValidator>();
        var mediaCoreOptions = Options.Create(new MediaCoreOptions { MaxTtlMinutes = 60 });

        _descriptorRetriever = new DescriptorRetriever(
            _descriptorRetrieverLogger.Object,
            _dhtMock.Object,
            descriptorValidatorMock.Object,
            mediaCoreOptions);
    }

    [Fact]
    public async Task ContentRegistrationAndRetrievalPipeline_CompleteWorkflow_Succeeds()
    {
        // Arrange - Test data representing a music track
        var externalId = "mb:recording:12345-67890-abcde";
        var contentId = "content:audio:track:mb-12345";

        // Act - Register the content
        await _registry.RegisterAsync(externalId, contentId);

        // Assert - Verify registration worked
        var resolved = await _registry.ResolveAsync(externalId);
        Assert.Equal(contentId, resolved);

        var isRegistered = await _registry.IsRegisteredAsync(externalId);
        Assert.True(isRegistered);

        // Verify it's in the correct domain
        var audioContent = await _registry.FindByDomainAsync("audio");
        Assert.Contains(contentId, audioContent);
    }

    [Fact]
    public async Task PerceptualHashingPipeline_AudioSimilarityDetection_Works()
    {
        // Arrange - Generate test audio samples (sine waves of different frequencies)
        var sampleRate = 44100;
        var duration = 1.0f; // 1 second

        // Same frequency should produce identical hashes
        var samples1 = GenerateSineWave(sampleRate, duration, 440); // A4 note
        var samples2 = GenerateSineWave(sampleRate, duration, 440); // Same A4 note

        // Act - Compute perceptual hashes
        var hash1 = _perceptualHasher.ComputeAudioHash(samples1, sampleRate, PerceptualHashAlgorithm.Chromaprint);
        var hash2 = _perceptualHasher.ComputeAudioHash(samples2, sampleRate, PerceptualHashAlgorithm.Chromaprint);

        // Assert - Identical content should have identical hashes
        Assert.Equal(hash1.Hex, hash2.Hex);
        Assert.Equal(hash1.NumericHash, hash2.NumericHash);

        // Different frequency should have different hash
        var samples3 = GenerateSineWave(sampleRate, duration, 880); // A5 note (octave higher)
        var hash3 = _perceptualHasher.ComputeAudioHash(samples3, sampleRate, PerceptualHashAlgorithm.Chromaprint);
        Assert.NotEqual(hash1.Hex, hash3.Hex);
        Assert.NotEqual(hash1.NumericHash, hash3.NumericHash);
    }

    [Fact]
    public async Task ContentLinkingPipeline_IPLDGraphCreation_Works()
    {
        // Arrange - Create a content graph: Album -> Track -> Artist
        var albumContentId = "content:audio:album:mb-67890";
        var trackContentId = "content:audio:track:mb-12345";
        var artistContentId = "content:audio:artist:mb-abc123";

        // Register all content
        await _registry.RegisterAsync("mb:release:67890", albumContentId);
        await _registry.RegisterAsync("mb:recording:12345", trackContentId);
        await _registry.RegisterAsync("mb:artist:abc123", artistContentId);

        // Act - Create links between content
        var albumToTrackLink = new IpldLink(IpldLinkNames.Tracks, trackContentId);
        var trackToAlbumLink = new IpldLink(IpldLinkNames.Album, albumContentId);
        var trackToArtistLink = new IpldLink(IpldLinkNames.Artist, artistContentId);

        await _ipldMapper.AddLinksAsync(trackContentId, new[] { trackToAlbumLink, trackToArtistLink });
        await _ipldMapper.AddLinksAsync(albumContentId, new[] { albumToTrackLink });

        // Assert - Verify links were created
        var albumLinks = await _ipldMapper.GetGraphAsync(albumContentId, maxDepth: 1);
        Assert.NotNull(albumLinks);
        Assert.Equal(albumContentId, albumLinks.RootContentId);

        var trackLinks = await _ipldMapper.GetGraphAsync(trackContentId, maxDepth: 1);
        Assert.NotNull(trackLinks);
        Assert.Equal(trackContentId, trackLinks.RootContentId);
    }

    [Fact]
    public async Task FuzzyMatchingPipeline_ContentSimilarityScoring_Works()
    {
        // Arrange - Register similar content
        var contentId1 = "content:audio:track:mb-12345";
        var contentId2 = "content:audio:track:mb-67890";
        var contentId3 = "content:video:movie:imdb-tt0111161"; // Different domain

        await _registry.RegisterAsync("mb:recording:12345", contentId1);
        await _registry.RegisterAsync("mb:recording:67890", contentId2);
        await _registry.RegisterAsync("imdb:tt0111161", contentId3);

        // Act - Test fuzzy matching between same domain content
        var score = await _fuzzyMatcher.ScorePerceptualAsync(contentId1, contentId2, _registry);

        // Assert - Should return a similarity score (could be low due to mock data)
        Assert.InRange(score, 0.0, 1.0);

        // Different domain should return 0
        var differentDomainScore = await _fuzzyMatcher.ScorePerceptualAsync(contentId1, contentId3, _registry);
        Assert.Equal(0.0, differentDomainScore);
    }

    [Fact]
    public async Task MetadataPortabilityPipeline_ExportImportWorkflow_Works()
    {
        // Arrange - Create test content with metadata
        var contentIds = new[]
        {
            "content:audio:track:mb-12345",
            "content:audio:album:mb-67890"
        };

        // Register content
        await _registry.RegisterAsync("mb:recording:12345", contentIds[0]);
        await _registry.RegisterAsync("mb:release:67890", contentIds[1]);

        // Create links between content
        var trackToAlbumLink = new IpldLink(IpldLinkNames.Album, contentIds[1]);
        await _ipldMapper.AddLinksAsync(contentIds[0], new[] { trackToAlbumLink });

        // Act - Export metadata package
        var exportedPackage = await _metadataPortability.ExportAsync(contentIds, includeLinks: true);

        // Assert - Verify export worked
        Assert.NotNull(exportedPackage);
        Assert.Equal("1.0", exportedPackage.Version);
        Assert.NotEmpty(exportedPackage.Source);
        Assert.True(exportedPackage.Metadata.TotalEntries >= 0);

        // Act - Import the package
        var importResult = await _metadataPortability.ImportAsync(exportedPackage);

        // Assert - Verify import succeeded
        Assert.NotNull(importResult);
        Assert.True(importResult.Success);
    }

    [Fact]
    public async Task DescriptorRetrievalPipeline_CachingAndVerification_Works()
    {
        // Arrange - Mock DHT responses
        var contentId = "content:audio:track:mb-12345";
        var testDescriptor = new ContentDescriptor
        {
            ContentId = contentId,
            SizeBytes = 1024 * 1024, // 1MB
            Codec = "mp3",
            Confidence = 0.95
        };

        _dhtMock.Setup(d => d.GetAsync<ContentDescriptor>(It.IsAny<string>(), default))
            .ReturnsAsync(testDescriptor);

        // Act - First retrieval (should hit DHT)
        var result1 = await _descriptorRetriever.RetrieveAsync(contentId, bypassCache: false);

        // Assert - Should find the descriptor
        Assert.True(result1.Found);
        Assert.NotNull(result1.Descriptor);
        Assert.Equal(contentId, result1.Descriptor.ContentId);
        Assert.False(result1.FromCache);

        // Act - Second retrieval (should hit cache)
        var result2 = await _descriptorRetriever.RetrieveAsync(contentId, bypassCache: false);

        // Assert - Should return cached result
        Assert.True(result2.Found);
        Assert.NotNull(result2.Descriptor);
        Assert.Equal(contentId, result2.Descriptor.ContentId);
        Assert.True(result2.FromCache);
    }

    [Fact]
    public async Task CrossCodecMatchingPipeline_TextAndPerceptualSimilarity_Works()
    {
        // Arrange - Create test content with similar metadata
        var contentId1 = "content:audio:track:mb-12345";
        var contentId2 = "content:audio:track:mb-67890";

        await _registry.RegisterAsync("mb:recording:12345", contentId1);
        await _registry.RegisterAsync("mb:recording:67890", contentId2);

        // Act - Test combined similarity scoring
        var perceptualScore = await _fuzzyMatcher.ScorePerceptualAsync(contentId1, contentId2, _registry);
        var textScore = _fuzzyMatcher.Score(contentId1, "", contentId2, "");

        // Assert - Both scoring methods should work
        Assert.InRange(perceptualScore, 0.0, 1.0);
        Assert.InRange(textScore, 0.0, 1.0);

        // Test fuzzy matching with candidates
        var candidates = new[] { contentId2 };
        var fuzzyResults = await _fuzzyMatcher.FindSimilarContentAsync(
            contentId1, candidates, _registry, minConfidence: 0.0);

        Assert.NotNull(fuzzyResults);
    }

    [Fact]
    public async Task ContentDomainQueries_DomainBasedFiltering_Works()
    {
        // Arrange - Register content across multiple domains
        var audioContent = new[]
        {
            "content:audio:track:mb-12345",
            "content:audio:album:mb-67890",
            "content:audio:artist:mb-abc123"
        };

        var videoContent = new[]
        {
            "content:video:movie:imdb-tt0111161",
            "content:video:series:imdb-tt0944947"
        };

        var imageContent = new[]
        {
            "content:image:photo:flickr-99999"
        };

        // Register all content
        foreach (var content in audioContent.Concat(videoContent).Concat(imageContent))
        {
            var domain = ContentIdParser.GetDomain(content);
            var type = ContentIdParser.GetType(content);
            var id = ContentIdParser.GetId(content);
            await _registry.RegisterAsync($"{domain}:{type}:{id}", content);
        }

        // Act & Assert - Query by domain
        var audioResults = await _registry.FindByDomainAsync("audio");
        Assert.Equal(3, audioResults.Count);
        foreach (var result in audioResults)
        {
            Assert.StartsWith("content:audio:", result);
        }

        var videoResults = await _registry.FindByDomainAsync("video");
        Assert.Equal(2, videoResults.Count);
        foreach (var result in videoResults)
        {
            Assert.StartsWith("content:video:", result);
        }

        var imageResults = await _registry.FindByDomainAsync("image");
        Assert.Single(imageResults);
        Assert.StartsWith("content:image:", imageResults.First());

        // Act & Assert - Query by domain and type
        var trackResults = await _registry.FindByDomainAndTypeAsync("audio", "track");
        Assert.Single(trackResults);
        Assert.Equal("content:audio:track:mb-12345", trackResults.First());

        var movieResults = await _registry.FindByDomainAndTypeAsync("video", "movie");
        Assert.Single(movieResults);
        Assert.Equal("content:video:movie:imdb-tt0111161", movieResults.First());
    }

    [Fact]
    public async Task EndToEndWorkflow_ContentDiscoveryAndMatching_Works()
    {
        // Arrange - Create a comprehensive content graph
        var artistId = "content:audio:artist:mb-radiohead";
        var albumId = "content:audio:album:mb-kid-a";
        var trackIds = new[]
        {
            "content:audio:track:mb-everything-in-its-right-place",
            "content:audio:track:mb-kid-a",
            "content:audio:track:mb-the-national-anthem"
        };

        // Register all content
        await _registry.RegisterAsync("mb:artist:radiohead", artistId);
        await _registry.RegisterAsync("mb:release:kid-a", albumId);
        for (int i = 0; i < trackIds.Length; i++)
        {
            await _registry.RegisterAsync($"mb:recording:track-{i}", trackIds[i]);
        }

        // Create relationships
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

        // Act - Test the complete discovery workflow
        var albumGraph = await _ipldMapper.GetGraphAsync(albumId, maxDepth: 2);
        Assert.NotNull(albumGraph);

        var artistTracks = await _registry.FindByDomainAsync("audio");
        Assert.True(artistTracks.Count >= 5); // artist + album + 3 tracks

        // Test fuzzy matching between tracks
        var trackSimilarities = new List<double>();
        for (int i = 0; i < trackIds.Length - 1; i++)
        {
            var score = await _fuzzyMatcher.ScorePerceptualAsync(trackIds[i], trackIds[i + 1], _registry);
            trackSimilarities.Add(score);
        }

        // Assert - All operations completed successfully
        Assert.NotNull(albumGraph);
        Assert.True(artistTracks.Count >= 5);
        Assert.All(trackSimilarities, score => Assert.InRange(score, 0.0, 1.0));
    }

    // Helper method for generating test audio data
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
