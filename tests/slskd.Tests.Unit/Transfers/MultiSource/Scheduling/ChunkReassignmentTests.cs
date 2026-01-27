// <copyright file="ChunkReassignmentTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.Scheduling;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using slskd.Transfers.MultiSource.Metrics;
using slskd.Transfers.MultiSource.Scheduling;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Unit tests for chunk reassignment functionality (T-1405).
/// </summary>
public class ChunkReassignmentTests
{
    private readonly ITestOutputHelper output;
    private readonly MockPeerMetricsService metricsService;
    private readonly ChunkScheduler scheduler;

    public ChunkReassignmentTests(ITestOutputHelper output)
    {
        this.output = output;
        metricsService = new MockPeerMetricsService();
        scheduler = new ChunkScheduler(metricsService, enableCostBasedScheduling: true);
    }

    [Fact]
    public void RegisterAssignment_Should_Track_Chunk_To_Peer()
    {
        // Arrange
        var chunkIndex = 5;
        var peerId = "testpeer";

        // Act
        scheduler.RegisterAssignment(chunkIndex, peerId);

        // Assert - Verify by checking degradation returns this chunk
        var degradedChunks = scheduler.HandlePeerDegradationAsync(peerId, DegradationReason.HighErrorRate, CancellationToken.None).Result;
        Assert.Contains(chunkIndex, degradedChunks);
    }

    [Fact]
    public void UnregisterAssignment_Should_Remove_Chunk_Tracking()
    {
        // Arrange
        var chunkIndex = 10;
        var peerId = "testpeer";
        scheduler.RegisterAssignment(chunkIndex, peerId);

        // Act
        scheduler.UnregisterAssignment(chunkIndex);

        // Assert
        var degradedChunks = scheduler.HandlePeerDegradationAsync(peerId, DegradationReason.HighErrorRate, CancellationToken.None).Result;
        Assert.DoesNotContain(chunkIndex, degradedChunks);
    }

    [Fact]
    public async Task HandlePeerDegradationAsync_Should_Return_All_Chunks_For_Peer()
    {
        // Arrange
        var peerId = "degradedpeer";
        var chunks = new[] { 1, 2, 3, 4, 5 };
        foreach (var chunk in chunks)
        {
            scheduler.RegisterAssignment(chunk, peerId);
        }

        // Also register some chunks to other peers
        scheduler.RegisterAssignment(10, "otherpeer1");
        scheduler.RegisterAssignment(11, "otherpeer2");

        // Act
        var result = await scheduler.HandlePeerDegradationAsync(peerId, DegradationReason.HighErrorRate, CancellationToken.None);

        // Assert
        Assert.Equal(chunks.Length, result.Count);
        foreach (var chunk in chunks)
        {
            Assert.Contains(chunk, result);
        }
        Assert.DoesNotContain(10, result);
        Assert.DoesNotContain(11, result);
    }

    [Fact]
    public async Task HandlePeerDegradationAsync_Should_Return_Empty_List_When_No_Chunks_Assigned()
    {
        // Arrange
        var peerId = "nopeer";

        // Act
        var result = await scheduler.HandlePeerDegradationAsync(peerId, DegradationReason.HighErrorRate, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task AssignChunkAsync_Should_Register_Assignment()
    {
        // Arrange
        var request = new ChunkRequest { ChunkIndex = 42, Size = 1024 };
        var availablePeers = new List<string> { "peer1", "peer2" };
        metricsService.SetMetrics("peer1", new PeerPerformanceMetrics
        {
            PeerId = "peer1",
            RttAvgMs = 50,
            ThroughputAvgBytesPerSec = 1000000,
            ChunksRequested = 100,
            ChunksCompleted = 99,
            ChunksFailed = 1,
            ChunksTimedOut = 0,
            ReputationScore = 0.8
        });
        metricsService.SetMetrics("peer2", new PeerPerformanceMetrics
        {
            PeerId = "peer2",
            RttAvgMs = 100,
            ThroughputAvgBytesPerSec = 500000,
            ChunksRequested = 50,
            ChunksCompleted = 45,
            ChunksFailed = 3,
            ChunksTimedOut = 2,
            ReputationScore = 0.6
        });

        // Act
        var assignment = await scheduler.AssignChunkAsync(request, availablePeers, CancellationToken.None);

        // Assert
        Assert.True(assignment.Success);
        Assert.NotNull(assignment.AssignedPeer);
        
        // Verify assignment was registered
        // Note: HandlePeerDegradationAsync unregisters chunks, so we verify by calling it
        // If the chunk was registered, it will be in the returned list
        var degradedChunks = await scheduler.HandlePeerDegradationAsync(assignment.AssignedPeer, DegradationReason.HighErrorRate, CancellationToken.None);
        
        // The chunk should be in the list (it was registered during AssignChunkAsync)
        Assert.Contains(request.ChunkIndex, degradedChunks);
    }

    [Fact]
    public async Task Multiple_Peers_Degradation_Should_Return_Correct_Chunks()
    {
        // Arrange
        scheduler.RegisterAssignment(1, "peer1");
        scheduler.RegisterAssignment(2, "peer1");
        scheduler.RegisterAssignment(3, "peer2");
        scheduler.RegisterAssignment(4, "peer2");
        scheduler.RegisterAssignment(5, "peer3");

        // Act
        var peer1Chunks = await scheduler.HandlePeerDegradationAsync("peer1", DegradationReason.HighErrorRate, CancellationToken.None);
        var peer2Chunks = await scheduler.HandlePeerDegradationAsync("peer2", DegradationReason.HighErrorRate, CancellationToken.None);
        var peer3Chunks = await scheduler.HandlePeerDegradationAsync("peer3", DegradationReason.HighErrorRate, CancellationToken.None);

        // Assert
        Assert.Equal(2, peer1Chunks.Count);
        Assert.Contains(1, peer1Chunks);
        Assert.Contains(2, peer1Chunks);
        
        Assert.Equal(2, peer2Chunks.Count);
        Assert.Contains(3, peer2Chunks);
        Assert.Contains(4, peer2Chunks);
        
        Assert.Single(peer3Chunks);
        Assert.Contains(5, peer3Chunks);
    }

    /// <summary>
    /// Mock peer metrics service for testing.
    /// </summary>
    private class MockPeerMetricsService : IPeerMetricsService
    {
        private readonly Dictionary<string, PeerPerformanceMetrics> _metrics = new();

        public void SetMetrics(string peerId, PeerPerformanceMetrics metrics)
        {
            _metrics[peerId] = metrics;
        }

        public Task<PeerPerformanceMetrics> GetMetricsAsync(string peerId, PeerSource source, CancellationToken ct = default)
        {
            if (_metrics.TryGetValue(peerId, out var metrics))
            {
                return Task.FromResult(metrics);
            }

            // Return default metrics if not found
            return Task.FromResult(new PeerPerformanceMetrics
            {
                PeerId = peerId,
                RttAvgMs = 100,
                ThroughputAvgBytesPerSec = 500000,
                ChunksRequested = 100,
                ChunksCompleted = 93,
                ChunksFailed = 5,
                ChunksTimedOut = 2,
                ReputationScore = 0.5
            });
        }

        public Task RecordRttSampleAsync(string peerId, double rttMs, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordThroughputSampleAsync(string peerId, long bytesTransferred, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordChunkCompletionAsync(string peerId, ChunkCompletionResult result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<List<PeerPerformanceMetrics>> GetRankedPeersAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<PeerPerformanceMetrics>());
        }
    }
}
