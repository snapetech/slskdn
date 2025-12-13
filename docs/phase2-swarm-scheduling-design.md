# Phase 2C: RTT + Throughput-Aware Swarm Scheduler - Detailed Design

> **Tasks**: T-406 to T-408  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phase 1 (Multi-source downloads), T-400 to T-405

---

## Overview

The swarm scheduler optimizes chunk assignment in multi-source downloads by tracking per-peer performance metrics (RTT, throughput, error rates) and using a cost function to prioritize the best peers for high-priority chunks. This creates CDN-like performance from a heterogeneous set of Soulseek + overlay peers.

---

## 1. Per-Peer Metrics Collection (T-406)

### 1.1. Peer Performance Metrics Model

```csharp
namespace slskd.Transfers.MultiSource
{
    /// <summary>
    /// Performance metrics for a single peer in the swarm.
    /// </summary>
    public class PeerPerformanceMetrics
    {
        public string PeerId { get; set; }  // Mesh peer ID or Soulseek username
        public PeerSource Source { get; set; }  // Soulseek or Overlay
        
        // Connection metrics
        public double RttAvgMs { get; set; }  // Exponential moving average
        public double RttStdDevMs { get; set; }  // Standard deviation
        public DateTimeOffset? LastRttSample { get; set; }
        
        // Throughput metrics
        public double ThroughputAvgBytesPerSec { get; set; }  // EMA
        public double ThroughputStdDevBytesPerSec { get; set; }
        public long TotalBytesTransferred { get; set; }
        public DateTimeOffset? LastThroughputSample { get; set; }
        
        // Reliability metrics
        public int ChunksRequested { get; set; }
        public int ChunksCompleted { get; set; }
        public int ChunksFailed { get; set; }
        public int ChunksTimedOut { get; set; }
        public int ChunksCorrupted { get; set; }  // Hash mismatch
        
        // Computed rates
        public double ErrorRate => ChunksRequested > 0 
            ? (double)ChunksFailed / ChunksRequested 
            : 0.0;
        public double TimeoutRate => ChunksRequested > 0 
            ? (double)ChunksTimedOut / ChunksRequested 
            : 0.0;
        public double SuccessRate => ChunksRequested > 0 
            ? (double)ChunksCompleted / ChunksRequested 
            : 0.0;
        
        // Sliding window state (for recent samples)
        public Queue<RttSample> RecentRttSamples { get; set; } = new();
        public Queue<ThroughputSample> RecentThroughputSamples { get; set; } = new();
        
        // Metadata
        public DateTimeOffset FirstSeen { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public int SampleCount { get; set; }
    }
    
    public enum PeerSource
    {
        Soulseek,
        Overlay
    }
    
    public class RttSample
    {
        public DateTimeOffset Timestamp { get; set; }
        public double RttMs { get; set; }
    }
    
    public class ThroughputSample
    {
        public DateTimeOffset Timestamp { get; set; }
        public double BytesPerSec { get; set; }
        public long BytesTransferred { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
```

### 1.2. Database Schema

```sql
-- Peer performance metrics (persisted for reputation)
CREATE TABLE PeerPerformanceMetrics (
    peer_id TEXT PRIMARY KEY,
    source TEXT NOT NULL,  -- 'soulseek' | 'overlay'
    rtt_avg_ms REAL DEFAULT 0.0,
    rtt_stddev_ms REAL DEFAULT 0.0,
    throughput_avg_bytes_per_sec REAL DEFAULT 0.0,
    throughput_stddev_bytes_per_sec REAL DEFAULT 0.0,
    total_bytes_transferred INTEGER DEFAULT 0,
    chunks_requested INTEGER DEFAULT 0,
    chunks_completed INTEGER DEFAULT 0,
    chunks_failed INTEGER DEFAULT 0,
    chunks_timed_out INTEGER DEFAULT 0,
    chunks_corrupted INTEGER DEFAULT 0,
    first_seen INTEGER NOT NULL,
    last_updated INTEGER NOT NULL,
    sample_count INTEGER DEFAULT 0
);

CREATE INDEX idx_peer_metrics_source ON PeerPerformanceMetrics(source);
CREATE INDEX idx_peer_metrics_throughput ON PeerPerformanceMetrics(throughput_avg_bytes_per_sec DESC);
CREATE INDEX idx_peer_metrics_success ON PeerPerformanceMetrics(chunks_completed);
```

### 1.3. Metrics Collection Service

```csharp
namespace slskd.Transfers.MultiSource
{
    public interface IPeerMetricsService
    {
        /// <summary>
        /// Get or create metrics for a peer.
        /// </summary>
        Task<PeerPerformanceMetrics> GetMetricsAsync(string peerId, PeerSource source, CancellationToken ct = default);
        
        /// <summary>
        /// Record an RTT sample.
        /// </summary>
        Task RecordRttSampleAsync(string peerId, double rttMs, CancellationToken ct = default);
        
        /// <summary>
        /// Record a throughput sample.
        /// </summary>
        Task RecordThroughputSampleAsync(string peerId, long bytesTransferred, TimeSpan duration, CancellationToken ct = default);
        
        /// <summary>
        /// Record chunk completion.
        /// </summary>
        Task RecordChunkCompletionAsync(string peerId, ChunkCompletionResult result, CancellationToken ct = default);
        
        /// <summary>
        /// Get ranked peers by performance.
        /// </summary>
        Task<List<PeerPerformanceMetrics>> GetRankedPeersAsync(int limit = 100, CancellationToken ct = default);
    }
    
    public class PeerMetricsService : IPeerMetricsService
    {
        private readonly ConcurrentDictionary<string, PeerPerformanceMetrics> metricsCache = new();
        private readonly ILogger<PeerMetricsService> log;
        
        // Configuration
        private const int MaxRecentSamples = 30;  // Sliding window size
        private const double EmaAlpha = 0.3;  // Exponential moving average weight
        
        public async Task RecordRttSampleAsync(string peerId, double rttMs, CancellationToken ct)
        {
            var metrics = await GetOrCreateMetricsAsync(peerId, ct);
            
            lock (metrics)
            {
                // Add to recent samples
                metrics.RecentRttSamples.Enqueue(new RttSample
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    RttMs = rttMs
                });
                
                // Trim sliding window
                while (metrics.RecentRttSamples.Count > MaxRecentSamples)
                {
                    metrics.RecentRttSamples.Dequeue();
                }
                
                // Update exponential moving average
                if (metrics.SampleCount == 0)
                {
                    metrics.RttAvgMs = rttMs;
                }
                else
                {
                    metrics.RttAvgMs = (EmaAlpha * rttMs) + ((1 - EmaAlpha) * metrics.RttAvgMs);
                }
                
                // Compute standard deviation from recent samples
                metrics.RttStdDevMs = ComputeStdDev(metrics.RecentRttSamples.Select(s => s.RttMs));
                
                metrics.LastRttSample = DateTimeOffset.UtcNow;
                metrics.SampleCount++;
                metrics.LastUpdated = DateTimeOffset.UtcNow;
            }
            
            await PersistMetricsAsync(metrics, ct);
        }
        
        public async Task RecordThroughputSampleAsync(string peerId, long bytesTransferred, TimeSpan duration, CancellationToken ct)
        {
            var metrics = await GetOrCreateMetricsAsync(peerId, ct);
            
            double bytesPerSec = bytesTransferred / duration.TotalSeconds;
            
            lock (metrics)
            {
                // Add to recent samples
                metrics.RecentThroughputSamples.Enqueue(new ThroughputSample
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    BytesPerSec = bytesPerSec,
                    BytesTransferred = bytesTransferred,
                    Duration = duration
                });
                
                // Trim sliding window
                while (metrics.RecentThroughputSamples.Count > MaxRecentSamples)
                {
                    metrics.RecentThroughputSamples.Dequeue();
                }
                
                // Update EMA
                if (metrics.TotalBytesTransferred == 0)
                {
                    metrics.ThroughputAvgBytesPerSec = bytesPerSec;
                }
                else
                {
                    metrics.ThroughputAvgBytesPerSec = (EmaAlpha * bytesPerSec) + ((1 - EmaAlpha) * metrics.ThroughputAvgBytesPerSec);
                }
                
                // Compute standard deviation
                metrics.ThroughputStdDevBytesPerSec = ComputeStdDev(metrics.RecentThroughputSamples.Select(s => s.BytesPerSec));
                
                metrics.TotalBytesTransferred += bytesTransferred;
                metrics.LastThroughputSample = DateTimeOffset.UtcNow;
                metrics.LastUpdated = DateTimeOffset.UtcNow;
            }
            
            await PersistMetricsAsync(metrics, ct);
        }
        
        public async Task RecordChunkCompletionAsync(string peerId, ChunkCompletionResult result, CancellationToken ct)
        {
            var metrics = await GetOrCreateMetricsAsync(peerId, ct);
            
            lock (metrics)
            {
                metrics.ChunksRequested++;
                
                switch (result)
                {
                    case ChunkCompletionResult.Success:
                        metrics.ChunksCompleted++;
                        break;
                    case ChunkCompletionResult.Failed:
                        metrics.ChunksFailed++;
                        break;
                    case ChunkCompletionResult.TimedOut:
                        metrics.ChunksTimedOut++;
                        break;
                    case ChunkCompletionResult.Corrupted:
                        metrics.ChunksCorrupted++;
                        break;
                }
                
                metrics.LastUpdated = DateTimeOffset.UtcNow;
            }
            
            await PersistMetricsAsync(metrics, ct);
        }
        
        private double ComputeStdDev(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return 0.0;
            
            double avg = valuesList.Average();
            double sumSquaredDiffs = valuesList.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquaredDiffs / valuesList.Count);
        }
        
        private async Task<PeerPerformanceMetrics> GetOrCreateMetricsAsync(string peerId, CancellationToken ct)
        {
            if (metricsCache.TryGetValue(peerId, out var cached))
            {
                return cached;
            }
            
            // Load from database or create new
            var metrics = await LoadMetricsFromDbAsync(peerId, ct) ?? new PeerPerformanceMetrics
            {
                PeerId = peerId,
                FirstSeen = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow
            };
            
            metricsCache[peerId] = metrics;
            return metrics;
        }
    }
    
    public enum ChunkCompletionResult
    {
        Success,
        Failed,
        TimedOut,
        Corrupted
    }
}
```

### 1.4. Integration with Multi-Source Downloads

```csharp
namespace slskd.Transfers.MultiSource
{
    public partial class MultiSourceDownloadService
    {
        private readonly IPeerMetricsService peerMetrics;
        
        private async Task DownloadChunkFromPeerAsync(
            VerifiedSource peer,
            ChunkRequest request,
            CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Send chunk request
                var response = await SendChunkRequestAsync(peer, request, ct);
                
                stopwatch.Stop();
                
                // Record RTT
                await peerMetrics.RecordRttSampleAsync(peer.PeerId, stopwatch.Elapsed.TotalMilliseconds, ct);
                
                // Validate chunk
                bool isValid = ValidateChunk(response, request);
                
                if (isValid)
                {
                    // Record throughput
                    await peerMetrics.RecordThroughputSampleAsync(
                        peer.PeerId,
                        response.Data.Length,
                        stopwatch.Elapsed,
                        ct);
                    
                    // Record success
                    await peerMetrics.RecordChunkCompletionAsync(peer.PeerId, ChunkCompletionResult.Success, ct);
                }
                else
                {
                    // Record corruption
                    await peerMetrics.RecordChunkCompletionAsync(peer.PeerId, ChunkCompletionResult.Corrupted, ct);
                    
                    log.Warning("[SWARM] Chunk corrupted from peer {Peer}", peer.PeerId);
                }
            }
            catch (TimeoutException)
            {
                await peerMetrics.RecordChunkCompletionAsync(peer.PeerId, ChunkCompletionResult.TimedOut, ct);
                log.Warning("[SWARM] Chunk request timed out for peer {Peer}", peer.PeerId);
            }
            catch (Exception ex)
            {
                await peerMetrics.RecordChunkCompletionAsync(peer.PeerId, ChunkCompletionResult.Failed, ct);
                log.Error(ex, "[SWARM] Chunk request failed for peer {Peer}", peer.PeerId);
            }
        }
    }
}
```

---

## 2. Cost Function for Peer Ranking (T-407)

### 2.1. Cost Function Model

```csharp
namespace slskd.Transfers.MultiSource
{
    /// <summary>
    /// Cost function for ranking peers in swarm scheduling.
    /// </summary>
    public class PeerCostFunction
    {
        // Configurable weights
        public double ThroughputWeight { get; set; } = 1.0;    // α (alpha)
        public double ErrorRateWeight { get; set; } = 0.5;     // β (beta)
        public double TimeoutRateWeight { get; set; } = 0.3;   // γ (gamma)
        public double RttWeight { get; set; } = 0.2;           // δ (delta)
        
        // Penalty multipliers
        public double ZeroThroughputPenalty { get; set; } = 1000.0;
        public double HighErrorRatePenalty { get; set; } = 10.0;
        
        /// <summary>
        /// Compute cost for a peer. Lower cost = better peer.
        /// </summary>
        public double ComputeCost(PeerPerformanceMetrics metrics)
        {
            double cost = 0.0;
            
            // Component 1: Inverse throughput (lower throughput = higher cost)
            if (metrics.ThroughputAvgBytesPerSec > 0)
            {
                // Normalize to MB/s for readability
                double throughputMBps = metrics.ThroughputAvgBytesPerSec / (1024.0 * 1024.0);
                cost += ThroughputWeight / throughputMBps;
            }
            else
            {
                // No throughput data = very high cost
                cost += ZeroThroughputPenalty;
            }
            
            // Component 2: Error rate penalty
            cost += ErrorRateWeight * metrics.ErrorRate * HighErrorRatePenalty;
            
            // Component 3: Timeout rate penalty
            cost += TimeoutRateWeight * metrics.TimeoutRate * HighErrorRatePenalty;
            
            // Component 4: RTT penalty (higher RTT = higher cost)
            if (metrics.RttAvgMs > 0)
            {
                // Normalize RTT to seconds
                double rttSec = metrics.RttAvgMs / 1000.0;
                cost += RttWeight * rttSec;
            }
            
            // Component 5: Variance penalty (unstable peers get penalized)
            if (metrics.ThroughputStdDevBytesPerSec > 0 && metrics.ThroughputAvgBytesPerSec > 0)
            {
                double coefficientOfVariation = metrics.ThroughputStdDevBytesPerSec / metrics.ThroughputAvgBytesPerSec;
                cost += 0.1 * coefficientOfVariation;
            }
            
            return cost;
        }
        
        /// <summary>
        /// Rank peers by cost (best peers first).
        /// </summary>
        public List<RankedPeer> RankPeers(List<PeerPerformanceMetrics> peers)
        {
            return peers
                .Select(p => new RankedPeer
                {
                    PeerId = p.PeerId,
                    Metrics = p,
                    Cost = ComputeCost(p)
                })
                .OrderBy(rp => rp.Cost)
                .ToList();
        }
    }
    
    public class RankedPeer
    {
        public string PeerId { get; set; }
        public PeerPerformanceMetrics Metrics { get; set; }
        public double Cost { get; set; }
        public int Rank { get; set; }  // 1-based rank
    }
}
```

### 2.2. Configuration Options

```yaml
multi_source:
  swarm_scheduler:
    cost_function:
      enabled: true
      
      # Cost function weights
      throughput_weight: 1.0      # α: Higher = prefer fast peers
      error_rate_weight: 0.5      # β: Higher = avoid unreliable peers
      timeout_rate_weight: 0.3    # γ: Higher = avoid slow/stalled peers
      rtt_weight: 0.2             # δ: Higher = prefer low-latency peers
      
      # Penalty multipliers
      zero_throughput_penalty: 1000.0
      high_error_rate_penalty: 10.0
      
    # Rebalancing
    rebalance_enabled: true
    rebalance_interval_seconds: 30
    rebalance_cost_threshold: 2.0  # Rebalance if peer cost increases by this factor
```

---

## 3. Cost-Based Scheduling Integration (T-408)

### 3.1. Enhanced Swarm Scheduler

```csharp
namespace slskd.Transfers.MultiSource
{
    public interface ISwarmScheduler
    {
        /// <summary>
        /// Assign chunks to peers using cost-based scheduling.
        /// </summary>
        Task<Dictionary<VerifiedSource, List<ChunkRequest>>> AssignChunksAsync(
            List<VerifiedSource> availablePeers,
            List<ChunkRequest> pendingChunks,
            CancellationToken ct = default);
        
        /// <summary>
        /// Rebalance chunk assignments based on updated peer metrics.
        /// </summary>
        Task RebalanceAsync(MultiSourceDownloadJob job, CancellationToken ct = default);
    }
    
    public class SwarmScheduler : ISwarmScheduler
    {
        private readonly IPeerMetricsService peerMetrics;
        private readonly PeerCostFunction costFunction;
        private readonly ILogger<SwarmScheduler> log;
        
        public SwarmScheduler(
            IPeerMetricsService peerMetrics,
            IOptions<Options> options,
            ILogger<SwarmScheduler> log)
        {
            this.peerMetrics = peerMetrics;
            this.log = log;
            
            // Load cost function config
            var config = options.Value.MultiSource.SwarmScheduler.CostFunction;
            this.costFunction = new PeerCostFunction
            {
                ThroughputWeight = config.ThroughputWeight,
                ErrorRateWeight = config.ErrorRateWeight,
                TimeoutRateWeight = config.TimeoutRateWeight,
                RttWeight = config.RttWeight
            };
        }
        
        public async Task<Dictionary<VerifiedSource, List<ChunkRequest>>> AssignChunksAsync(
            List<VerifiedSource> availablePeers,
            List<ChunkRequest> pendingChunks,
            CancellationToken ct)
        {
            // Step 1: Get metrics for all peers
            var peerMetricsList = new List<PeerPerformanceMetrics>();
            foreach (var peer in availablePeers)
            {
                var metrics = await peerMetrics.GetMetricsAsync(peer.PeerId, peer.Source, ct);
                peerMetricsList.Add(metrics);
            }
            
            // Step 2: Rank peers by cost
            var rankedPeers = costFunction.RankPeers(peerMetricsList);
            
            for (int i = 0; i < rankedPeers.Count; i++)
            {
                rankedPeers[i].Rank = i + 1;
            }
            
            log.Information("[SWARM] Peer ranking:");
            foreach (var rp in rankedPeers.Take(5))
            {
                log.Information("  #{Rank}: {Peer} - cost={Cost:F2}, throughput={Throughput:F1} MB/s, rtt={Rtt:F0}ms, errors={Errors:P0}",
                    rp.Rank,
                    rp.PeerId,
                    rp.Cost,
                    rp.Metrics.ThroughputAvgBytesPerSec / (1024.0 * 1024.0),
                    rp.Metrics.RttAvgMs,
                    rp.Metrics.ErrorRate);
            }
            
            // Step 3: Assign chunks using priority-aware strategy
            var assignments = new Dictionary<VerifiedSource, List<ChunkRequest>>();
            
            // Sort chunks by priority (descending)
            var sortedChunks = pendingChunks.OrderByDescending(c => c.Priority).ToList();
            
            int peerIndex = 0;
            foreach (var chunk in sortedChunks)
            {
                // Assign high-priority chunks to best peers
                // Use round-robin within top N peers for load balancing
                int topPeerCount = Math.Min(5, rankedPeers.Count);
                var assignedPeer = rankedPeers[peerIndex % topPeerCount];
                
                var peer = availablePeers.First(p => p.PeerId == assignedPeer.PeerId);
                
                if (!assignments.ContainsKey(peer))
                {
                    assignments[peer] = new List<ChunkRequest>();
                }
                
                assignments[peer].Add(chunk);
                
                peerIndex++;
            }
            
            log.Information("[SWARM] Assigned {ChunkCount} chunks to {PeerCount} peers",
                sortedChunks.Count, assignments.Count);
            
            return assignments;
        }
        
        public async Task RebalanceAsync(MultiSourceDownloadJob job, CancellationToken ct)
        {
            // Step 1: Identify underperforming peers
            var degradedPeers = new List<VerifiedSource>();
            
            foreach (var peer in job.ActivePeers)
            {
                var currentMetrics = await peerMetrics.GetMetricsAsync(peer.PeerId, peer.Source, ct);
                var currentCost = costFunction.ComputeCost(currentMetrics);
                
                // Check if cost has increased significantly since assignment
                if (peer.InitialCost.HasValue && currentCost > peer.InitialCost * job.Config.RebalanceCostThreshold)
                {
                    degradedPeers.Add(peer);
                    log.Warning("[SWARM] Peer {Peer} degraded: cost {Old:F2} → {New:F2}",
                        peer.PeerId, peer.InitialCost, currentCost);
                }
            }
            
            if (degradedPeers.Count == 0)
            {
                return;  // No rebalancing needed
            }
            
            // Step 2: Reassign chunks from degraded peers to better peers
            foreach (var degradedPeer in degradedPeers)
            {
                var pendingChunks = job.GetPendingChunksForPeer(degradedPeer);
                
                if (pendingChunks.Count > 0)
                {
                    log.Information("[SWARM] Rebalancing {Count} chunks from peer {Peer}",
                        pendingChunks.Count, degradedPeer.PeerId);
                    
                    // Cancel pending chunks for this peer
                    await job.CancelPeerChunksAsync(degradedPeer, ct);
                    
                    // Reassign to other peers
                    var otherPeers = job.ActivePeers.Except(degradedPeers).ToList();
                    var newAssignments = await AssignChunksAsync(otherPeers, pendingChunks, ct);
                    
                    foreach (var (peer, chunks) in newAssignments)
                    {
                        await job.AssignChunksToPeerAsync(peer, chunks, ct);
                    }
                }
            }
        }
    }
}
```

### 3.2. Chunk Priority System

```csharp
namespace slskd.Transfers.MultiSource
{
    public class ChunkRequest
    {
        public long Offset { get; set; }
        public int Length { get; set; }
        public int Priority { get; set; }  // 0-10, higher = more urgent
        public ChunkPriorityReason PriorityReason { get; set; }
    }
    
    public enum ChunkPriorityReason
    {
        Normal,                 // Regular chunk
        NearPlaybackHead,       // For streaming: close to playback position
        EndOfFile,              // EOF verification chunk
        Retry,                  // Previously failed chunk (higher priority)
        UserRequested,          // User explicitly requested this range
        RescueMode              // Rescue mode override (highest priority)
    }
    
    public static class ChunkPrioritizer
    {
        public static int ComputePriority(ChunkRequest chunk, MultiSourceDownloadJob job)
        {
            int priority = 5;  // Base priority
            
            switch (chunk.PriorityReason)
            {
                case ChunkPriorityReason.UserRequested:
                    priority = 10;
                    break;
                case ChunkPriorityReason.RescueMode:
                    priority = 9;
                    break;
                case ChunkPriorityReason.NearPlaybackHead:
                    priority = 8;
                    break;
                case ChunkPriorityReason.EndOfFile:
                    priority = 7;
                    break;
                case ChunkPriorityReason.Retry:
                    priority = 6;
                    break;
                default:
                    priority = 5;
                    break;
            }
            
            // Boost priority for chunks near completion
            double completionRatio = job.BytesCompleted / (double)job.TotalBytes;
            if (completionRatio > 0.9)
            {
                priority = Math.Min(10, priority + 2);
            }
            
            return priority;
        }
    }
}
```

---

## 4. Implementation Checklist

### T-406: Per-peer metrics collection

- [ ] Define `PeerPerformanceMetrics` model
- [ ] Create database schema for metrics persistence
- [ ] Implement `IPeerMetricsService` interface
- [ ] Implement RTT sample recording with EMA
- [ ] Implement throughput sample recording with EMA
- [ ] Implement chunk completion recording
- [ ] Integrate metrics recording into `MultiSourceDownloadService`
- [ ] Add sliding window management (30 samples)
- [ ] Add unit tests for EMA calculations
- [ ] Add integration tests for metrics persistence

### T-407: Cost function for peer ranking

- [ ] Define `PeerCostFunction` class
- [ ] Implement `ComputeCost()` method
- [ ] Implement `RankPeers()` method
- [ ] Add configuration options to `Options.cs`
- [ ] Add cost function weight tuning CLI tool
- [ ] Add unit tests for cost calculations
- [ ] Add integration tests with mock peer data
- [ ] Document cost function parameters

### T-408: Cost-based scheduling integration

- [ ] Define `ISwarmScheduler` interface
- [ ] Implement `SwarmScheduler.AssignChunksAsync()`
- [ ] Implement `SwarmScheduler.RebalanceAsync()`
- [ ] Define `ChunkPriorityReason` enum
- [ ] Implement `ChunkPrioritizer` utility
- [ ] Add chunk priority to `ChunkRequest`
- [ ] Integrate scheduler into `MultiSourceDownloadService`
- [ ] Add rebalancing background task
- [ ] Add logging for scheduling decisions
- [ ] Add unit tests for chunk assignment
- [ ] Add integration tests for rebalancing
- [ ] Add performance benchmarks

---

## 5. Testing Strategy

### Unit Tests

```csharp
[Fact]
public void PeerCostFunction_LowThroughput_Should_IncreaseCost()
{
    var costFunction = new PeerCostFunction();
    
    var fastPeer = new PeerPerformanceMetrics
    {
        ThroughputAvgBytesPerSec = 5 * 1024 * 1024,  // 5 MB/s
        ErrorRate = 0.0,
        RttAvgMs = 50
    };
    
    var slowPeer = new PeerPerformanceMetrics
    {
        ThroughputAvgBytesPerSec = 0.5 * 1024 * 1024,  // 0.5 MB/s
        ErrorRate = 0.0,
        RttAvgMs = 50
    };
    
    double fastCost = costFunction.ComputeCost(fastPeer);
    double slowCost = costFunction.ComputeCost(slowPeer);
    
    Assert.True(slowCost > fastCost);
}

[Fact]
public void SwarmScheduler_Should_AssignHighPriorityChunks_ToBestPeers()
{
    var scheduler = new SwarmScheduler(mockMetrics, mockOptions, mockLogger);
    
    var chunks = new List<ChunkRequest>
    {
        new() { Offset = 0, Length = 1024, Priority = 10 },      // High
        new() { Offset = 1024, Length = 1024, Priority = 5 },    // Normal
        new() { Offset = 2048, Length = 1024, Priority = 1 }     // Low
    };
    
    var assignments = await scheduler.AssignChunksAsync(peers, chunks, ct);
    
    // Verify high-priority chunk assigned to best peer
    var bestPeer = assignments.Keys.OrderBy(p => p.Cost).First();
    Assert.Contains(assignments[bestPeer], c => c.Priority == 10);
}
```

---

## 6. Performance Considerations

1. **Metrics Caching**: Keep recent metrics in memory, persist periodically
2. **Cost Computation**: Pre-compute costs during idle time
3. **Rebalancing Throttling**: Don't rebalance more than once per 30 seconds
4. **Sample Window Management**: Use circular buffers for fixed-size windows
5. **Database Writes**: Batch metrics updates to reduce I/O

---

## 7. Tuning Guide

### Default Cost Function Weights

- **High-throughput priority**: `throughput_weight = 2.0`
- **Reliability priority**: `error_rate_weight = 1.0, timeout_rate_weight = 0.8`
- **Low-latency priority**: `rtt_weight = 0.5`
- **Balanced (default)**: `throughput_weight = 1.0, error_rate_weight = 0.5, timeout_rate_weight = 0.3, rtt_weight = 0.2`

### When to Rebalance

- **Aggressive**: `rebalance_interval = 15s, rebalance_cost_threshold = 1.5`
- **Conservative**: `rebalance_interval = 60s, rebalance_cost_threshold = 3.0`
- **Default**: `rebalance_interval = 30s, rebalance_cost_threshold = 2.0`

---

This design creates a sophisticated, CDN-like swarm scheduler that adapts to real-world peer performance!

















