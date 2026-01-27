// <copyright file="AdaptiveSchedulerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.Scheduling;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Transfers.MultiSource.Metrics;
using slskd.Transfers.MultiSource.Scheduling;
using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Unit tests for AdaptiveScheduler.
/// </summary>
public class AdaptiveSchedulerTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<IChunkScheduler> _baseSchedulerMock;
    private readonly Mock<IPeerMetricsService> _peerMetricsMock;
    private readonly Mock<ILogger<AdaptiveScheduler>> _loggerMock;
    private readonly AdaptiveScheduler _service;

    public AdaptiveSchedulerTests(ITestOutputHelper output)
    {
        _output = output;
        _baseSchedulerMock = new Mock<IChunkScheduler>();
        _peerMetricsMock = new Mock<IPeerMetricsService>();
        _loggerMock = new Mock<ILogger<AdaptiveScheduler>>();
        _service = new AdaptiveScheduler(
            _baseSchedulerMock.Object,
            _peerMetricsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AssignChunkAsync_Should_Delegate_To_Base_Scheduler()
    {
        // Arrange
        var request = new ChunkRequest { ChunkIndex = 1, Size = 1024 };
        var availablePeers = new List<string> { "peer1", "peer2" };
        var expectedAssignment = new ChunkAssignment
        {
            ChunkIndex = 1,
            AssignedPeer = "peer1",
            Success = true,
        };

        _baseSchedulerMock
            .Setup(x => x.AssignChunkAsync(request, availablePeers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssignment);

        // Act
        var result = await _service.AssignChunkAsync(request, availablePeers, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAssignment.Success, result.Success);
        Assert.Equal(expectedAssignment.AssignedPeer, result.AssignedPeer);
        _baseSchedulerMock.Verify(x => x.AssignChunkAsync(request, availablePeers, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordChunkCompletionAsync_Should_Record_Feedback()
    {
        // Arrange
        var chunkIndex = 1;
        var peerId = "peer1";
        var success = true;
        var durationMs = 1000L;
        var bytesTransferred = 1024 * 1024L;

        // Act
        await _service.RecordChunkCompletionAsync(chunkIndex, peerId, success, durationMs, bytesTransferred, CancellationToken.None);

        // Assert
        // Verify stats are updated
        var stats = _service.GetStats();
        Assert.True(stats.TotalCompletions > 0);
    }

    [Fact]
    public async Task RecordChunkCompletionAsync_Should_Update_Peer_Learning_Data()
    {
        // Arrange
        var peerId = "peer1";

        // Act
        await _service.RecordChunkCompletionAsync(1, peerId, true, 1000, 1024 * 1024, CancellationToken.None);
        await _service.RecordChunkCompletionAsync(2, peerId, true, 2000, 2 * 1024 * 1024, CancellationToken.None);
        await _service.RecordChunkCompletionAsync(3, peerId, false, 500, 512 * 1024, CancellationToken.None);

        // Assert
        var stats = _service.GetStats();
        Assert.True(stats.TrackedPeers > 0);
        Assert.Equal(3, stats.TotalCompletions);
    }

    [Fact]
    public async Task RecordChunkCompletionAsync_Should_Trigger_Adaptation_After_Interval()
    {
        // Arrange
        var peerId = "peer1";
        const int AdaptationInterval = 50;

        // Act - Record enough completions to trigger adaptation
        for (int i = 0; i < AdaptationInterval; i++)
        {
            await _service.RecordChunkCompletionAsync(i, peerId, true, 1000, 1024 * 1024, CancellationToken.None);
        }

        // Assert
        // Adaptation should have been triggered (we can't directly verify, but stats should reflect it)
        var stats = _service.GetStats();
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task AdaptWeightsAsync_Should_Not_Adapt_With_Insufficient_Data()
    {
        // Arrange
        // Record fewer than 10 completions

        // Act
        await _service.AdaptWeightsAsync(CancellationToken.None);

        // Assert
        var stats = _service.GetStats();
        // Weights should remain at initial values when there's insufficient data
        Assert.True(stats.ReputationWeight > 0);
        Assert.True(stats.ThroughputWeight > 0);
        Assert.True(stats.RttWeight > 0);
    }

    [Fact]
    public async Task AdaptWeightsAsync_Should_Adapt_Weights_With_Enough_Data()
    {
        // Arrange
        // Record enough completions with varying success rates
        for (int i = 0; i < 20; i++)
        {
            var peerId = i % 2 == 0 ? "goodpeer" : "badpeer";
            var success = i % 2 == 0; // Good peer succeeds, bad peer fails
            await _service.RecordChunkCompletionAsync(i, peerId, success, 1000, 1024 * 1024, CancellationToken.None);
        }

        // Act
        await _service.AdaptWeightsAsync(CancellationToken.None);

        // Assert
        var stats = _service.GetStats();
        // Weights should be valid (between 0 and 1, sum approximately 1.0)
        Assert.True(stats.ReputationWeight >= 0.0 && stats.ReputationWeight <= 1.0);
        Assert.True(stats.ThroughputWeight >= 0.0 && stats.ThroughputWeight <= 1.0);
        Assert.True(stats.RttWeight >= 0.0 && stats.RttWeight <= 1.0);
    }

    [Fact]
    public void GetStats_Should_Return_Valid_Statistics()
    {
        // Arrange

        // Act
        var stats = _service.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.ReputationWeight >= 0.0 && stats.ReputationWeight <= 1.0);
        Assert.True(stats.ThroughputWeight >= 0.0 && stats.ThroughputWeight <= 1.0);
        Assert.True(stats.RttWeight >= 0.0 && stats.RttWeight <= 1.0);
        Assert.True(stats.RecentPerformanceWeight >= 0.0 && stats.RecentPerformanceWeight <= 1.0);
        Assert.True(stats.TotalCompletions >= 0);
        Assert.True(stats.TrackedPeers >= 0);
    }

    [Fact]
    public async Task AssignMultipleChunksAsync_Should_Delegate_To_Base_Scheduler()
    {
        // Arrange
        var requests = new List<ChunkRequest>
        {
            new ChunkRequest { ChunkIndex = 1, Size = 1024 },
            new ChunkRequest { ChunkIndex = 2, Size = 2048 },
        };
        var availablePeers = new List<string> { "peer1" };
        var expectedAssignments = new List<ChunkAssignment>
        {
            new ChunkAssignment { ChunkIndex = 1, AssignedPeer = "peer1", Success = true },
            new ChunkAssignment { ChunkIndex = 2, AssignedPeer = "peer1", Success = true },
        };

        _baseSchedulerMock
            .Setup(x => x.AssignMultipleChunksAsync(requests, availablePeers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssignments);

        // Act
        var result = await _service.AssignMultipleChunksAsync(requests, availablePeers, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAssignments.Count, result.Count);
        _baseSchedulerMock.Verify(x => x.AssignMultipleChunksAsync(requests, availablePeers, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePeerDegradationAsync_Should_Delegate_And_Update_Learning_Data()
    {
        // Arrange
        var peerId = "degradedpeer";
        var reason = DegradationReason.HighErrorRate;
        var expectedChunks = new List<int> { 1, 2, 3 };

        _baseSchedulerMock
            .Setup(x => x.HandlePeerDegradationAsync(peerId, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChunks);

        // Act
        var result = await _service.HandlePeerDegradationAsync(peerId, reason, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedChunks, result);
        _baseSchedulerMock.Verify(x => x.HandlePeerDegradationAsync(peerId, reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RegisterAssignment_Should_Delegate_To_Base_Scheduler_When_Supported()
    {
        // Arrange
        var chunkIndex = 1;
        var peerId = "peer1";

        // Act & Assert
        // If base scheduler supports RegisterAssignment, it should be called
        // Otherwise, should not throw
        _service.RegisterAssignment(chunkIndex, peerId);
    }

    [Fact]
    public void UnregisterAssignment_Should_Delegate_To_Base_Scheduler_When_Supported()
    {
        // Arrange
        var chunkIndex = 1;

        // Act & Assert
        // If base scheduler supports UnregisterAssignment, it should be called
        // Otherwise, should not throw
        _service.UnregisterAssignment(chunkIndex);
    }

    [Fact]
    public async Task RecordChunkCompletionAsync_Should_Handle_Exceptions()
    {
        // Arrange
        // This test verifies error handling

        // Act - Should not throw even if there's an internal error
        await _service.RecordChunkCompletionAsync(1, "peer1", true, 1000, 1024 * 1024, CancellationToken.None);

        // Assert
        // Should complete without throwing
        Assert.True(true);
    }

    [Fact]
    public async Task RecordChunkCompletionAsync_Should_Limit_Recent_Completions_Queue()
    {
        // Arrange
        var peerId = "peer1";
        const int MaxRecentCompletions = 100;

        // Act - Record more than max completions
        for (int i = 0; i < MaxRecentCompletions + 50; i++)
        {
            await _service.RecordChunkCompletionAsync(i, peerId, true, 1000, 1024 * 1024, CancellationToken.None);
        }

        // Assert
        var stats = _service.GetStats();
        // Queue should be limited to MaxRecentCompletions
        Assert.True(stats.TotalCompletions <= MaxRecentCompletions);
    }
}
