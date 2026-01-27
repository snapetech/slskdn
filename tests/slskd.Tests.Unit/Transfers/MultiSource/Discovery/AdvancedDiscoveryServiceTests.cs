// <copyright file="AdvancedDiscoveryServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.Discovery;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Transfers.MultiSource;
using slskd.Transfers.MultiSource.Discovery;
using slskd.Transfers.MultiSource.Metrics;
using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Unit tests for AdvancedDiscoveryService.
/// </summary>
public class AdvancedDiscoveryServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<IContentVerificationService> _contentVerificationMock;
    private readonly Mock<IPeerMetricsService> _peerMetricsMock;
    private readonly Mock<ILogger<AdvancedDiscoveryService>> _loggerMock;
    private readonly AdvancedDiscoveryService _service;

    public AdvancedDiscoveryServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _contentVerificationMock = new Mock<IContentVerificationService>();
        _peerMetricsMock = new Mock<IPeerMetricsService>();
        _loggerMock = new Mock<ILogger<AdvancedDiscoveryService>>();
        _service = new AdvancedDiscoveryService(
            _contentVerificationMock.Object,
            _peerMetricsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DiscoverPeersForContentAsync_Should_Return_Empty_List_When_No_Sources()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
            MinSimilarity = 0.7,
        };

        _contentVerificationMock
            .Setup(x => x.VerifySourcesAsync(It.IsAny<ContentVerificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentVerificationResult
            {
                SourcesByHash = new Dictionary<string, List<VerifiedSource>>(),
            });

        // Act
        var result = await _service.DiscoverPeersForContentAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverPeersForContentAsync_Should_Filter_By_MinSimilarity()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
            MinSimilarity = 0.8,
        };

        var sources = new List<VerifiedSource>
        {
            new VerifiedSource
            {
                Username = "peer1",
                FullPath = "test.mp3", // Exact match - high similarity
            },
            new VerifiedSource
            {
                Username = "peer2",
                FullPath = "different.mp3", // Low similarity
            },
        };

        _contentVerificationMock
            .Setup(x => x.VerifySourcesAsync(It.IsAny<ContentVerificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentVerificationResult
            {
                SourcesByHash = new Dictionary<string, List<VerifiedSource>> { { "hash1", sources } },
            });

        // Act
        var result = await _service.DiscoverPeersForContentAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Should filter out low-similarity peers
        Assert.All(result, p => Assert.True(p.SimilarityScore >= request.MinSimilarity));
    }

    [Fact]
    public async Task DiscoverPeersForContentAsync_Should_Classify_Match_Types()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
            RecordingId = "mbid-123",
            MinSimilarity = 0.5,
        };

        var sources = new List<VerifiedSource>
        {
            new VerifiedSource
            {
                Username = "peer1",
                FullPath = "test.mp3", // Exact match
                MusicBrainzRecordingId = "mbid-123",
            },
            new VerifiedSource
            {
                Username = "peer2",
                FullPath = "test_variant.mp3", // Variant match
                MusicBrainzRecordingId = "mbid-123",
            },
        };

        _contentVerificationMock
            .Setup(x => x.VerifySourcesAsync(It.IsAny<ContentVerificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentVerificationResult
            {
                SourcesByHash = new Dictionary<string, List<VerifiedSource>> { { "hash1", sources } },
            });

        // Act
        var result = await _service.DiscoverPeersForContentAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Should have different match types
        Assert.Contains(result, p => p.MatchType == MatchType.Exact || p.MatchType == MatchType.Metadata || p.MatchType == MatchType.Variant);
    }

    [Fact]
    public async Task DiscoverPeersForContentAsync_Should_Calculate_Metadata_Confidence()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
            RecordingId = "mbid-123",
            Fingerprint = "fingerprint-123",
            MinSimilarity = 0.5,
        };

        var sources = new List<VerifiedSource>
        {
            new VerifiedSource
            {
                Username = "peer1",
                FullPath = "test.mp3",
                MusicBrainzRecordingId = "mbid-123", // Matches recording ID
                AudioFingerprint = "fingerprint-123", // Matches fingerprint
            },
        };

        _contentVerificationMock
            .Setup(x => x.VerifySourcesAsync(It.IsAny<ContentVerificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentVerificationResult
            {
                SourcesByHash = new Dictionary<string, List<VerifiedSource>> { { "hash1", sources } },
            });

        // Act
        var result = await _service.DiscoverPeersForContentAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Should have high metadata confidence when recording ID and fingerprint match
        Assert.All(result, p => Assert.True(p.MetadataConfidence >= 0.5));
    }

    [Fact]
    public async Task RankPeersAsync_Should_Sort_By_Ranking_Score()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
        };

        var peers = new List<DiscoveredPeer>
        {
            new DiscoveredPeer
            {
                PeerId = "peer1",
                SimilarityScore = 0.9,
                MatchType = MatchType.Exact,
                MetadataConfidence = 0.8,
            },
            new DiscoveredPeer
            {
                PeerId = "peer2",
                SimilarityScore = 0.7,
                MatchType = MatchType.Variant,
                MetadataConfidence = 0.6,
            },
        };

        _peerMetricsMock
            .Setup(x => x.GetMetricsAsync("peer1", It.IsAny<PeerSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerPerformanceMetrics
            {
                PeerId = "peer1",
                ReputationScore = 0.9,
                RttAvgMs = 50,
                ThroughputAvgBytesPerSec = 2000000,
                ChunksCompleted = 100,
                ChunksFailed = 2,
                ChunksTimedOut = 1,
                ChunksCorrupted = 0,
            });

        _peerMetricsMock
            .Setup(x => x.GetMetricsAsync("peer2", It.IsAny<PeerSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerPerformanceMetrics
            {
                PeerId = "peer2",
                ReputationScore = 0.6,
                RttAvgMs = 100,
                ThroughputAvgBytesPerSec = 1000000,
                ChunksCompleted = 50,
                ChunksFailed = 10,
                ChunksTimedOut = 5,
                ChunksCorrupted = 2,
            });

        // Act
        var result = await _service.RankPeersAsync(peers, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(peers.Count, result.Count);
        // Should be sorted by ranking score (descending)
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].RankingScore >= result[i + 1].RankingScore);
        }
        // Ranks should be assigned correctly
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(i + 1, result[i].Rank);
        }
    }

    [Fact]
    public async Task RankPeersAsync_Should_Handle_Null_Metrics()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
        };

        var peers = new List<DiscoveredPeer>
        {
            new DiscoveredPeer
            {
                PeerId = "peer1",
                SimilarityScore = 0.8,
            },
        };

        _peerMetricsMock
            .Setup(x => x.GetMetricsAsync("peer1", It.IsAny<PeerSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PeerPerformanceMetrics?)null);

        // Act
        var result = await _service.RankPeersAsync(peers, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        // Should still rank peer even with null metrics (uses default scores)
        Assert.True(result[0].RankingScore >= 0.0);
    }

    [Fact]
    public async Task RankPeersAsync_Should_Calculate_Performance_And_Availability_Scores()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
        };

        var peers = new List<DiscoveredPeer>
        {
            new DiscoveredPeer
            {
                PeerId = "peer1",
                SimilarityScore = 0.9,
            },
        };

        _peerMetricsMock
            .Setup(x => x.GetMetricsAsync("peer1", It.IsAny<PeerSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerPerformanceMetrics
            {
                PeerId = "peer1",
                ReputationScore = 0.9,
                RttAvgMs = 50,
                ThroughputAvgBytesPerSec = 2000000,
                ChunksCompleted = 100,
                ChunksFailed = 5,
                ChunksTimedOut = 2,
                ChunksCorrupted = 1,
                LastUpdated = DateTimeOffset.UtcNow,
            });

        // Act
        var result = await _service.RankPeersAsync(peers, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var rankedPeer = result[0];
        Assert.True(rankedPeer.PerformanceScore >= 0.0 && rankedPeer.PerformanceScore <= 1.0);
        Assert.True(rankedPeer.AvailabilityScore >= 0.0 && rankedPeer.AvailabilityScore <= 1.0);
    }

    [Fact]
    public async Task FindSimilarVariantsAsync_Should_Return_Empty_List_For_Placeholder()
    {
        // Arrange
        var filename = "test.mp3";
        var fileSize = 1024 * 1024L;
        var recordingId = "mbid-123";

        // Act
        var result = await _service.FindSimilarVariantsAsync(filename, fileSize, recordingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Current implementation returns empty list (placeholder)
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverPeersForContentAsync_Should_Handle_Exceptions()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
        };

        _contentVerificationMock
            .Setup(x => x.VerifySourcesAsync(It.IsAny<ContentVerificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.DiscoverPeersForContentAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        // Should log error but not throw
    }

    [Fact]
    public async Task RankPeersAsync_Should_Handle_Exceptions()
    {
        // Arrange
        var request = new ContentDiscoveryRequest
        {
            Filename = "test.mp3",
            FileSize = 1024 * 1024,
            Domain = "music",
        };

        var peers = new List<DiscoveredPeer>
        {
            new DiscoveredPeer
            {
                PeerId = "peer1",
                SimilarityScore = 0.8,
            },
        };

        _peerMetricsMock
            .Setup(x => x.GetMetricsAsync(It.IsAny<string>(), It.IsAny<PeerSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.RankPeersAsync(peers, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Should return peers with default scores on error
        Assert.Equal(peers.Count, result.Count);
    }
}
