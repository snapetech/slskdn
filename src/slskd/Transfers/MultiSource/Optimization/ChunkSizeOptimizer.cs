// <copyright file="ChunkSizeOptimizer.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Optimization
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Transfers.MultiSource;

    /// <summary>
    ///     Service for optimizing chunk sizes for swarm downloads.
    ///     Uses heuristics based on file size, peer count, and performance metrics.
    /// </summary>
    public class ChunkSizeOptimizer : IChunkSizeOptimizer
    {
        private readonly ILogger<ChunkSizeOptimizer> _logger;

        // Chunk size constraints
        private const int MinChunkSize = 64 * 1024;        // 64 KB minimum (overhead amortization)
        private const int MaxChunkSize = 10 * 1024 * 1024; // 10 MB maximum (failure recovery)
        private const int DefaultChunkSize = MultiSourceDownloadService.DefaultChunkSize; // 512 KB default

        // Optimal chunk count ranges
        private const int MinOptimalChunks = 4;            // Minimum chunks for parallelism
        private const int MaxOptimalChunks = 200;          // Maximum chunks (diminishing returns)
        private const int TargetChunksPerPeer = 2;         // Target chunks per peer for good parallelism

        public ChunkSizeOptimizer(ILogger<ChunkSizeOptimizer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<int> RecommendChunkSizeAsync(
            long fileSize,
            int peerCount,
            double? averageThroughputBps = null,
            double? averageRttMs = null,
            CancellationToken cancellationToken = default)
        {
            if (fileSize <= 0)
            {
                _logger.LogWarning("Invalid file size for chunk optimization: {FileSize}", fileSize);
                return Task.FromResult(DefaultChunkSize);
            }

            if (peerCount <= 0)
            {
                _logger.LogWarning("Invalid peer count for chunk optimization: {PeerCount}", peerCount);
                return Task.FromResult(DefaultChunkSize);
            }

            // Strategy 1: Base recommendation on file size and peer count
            var baseChunkSize = CalculateBaseChunkSize(fileSize, peerCount);

            // Strategy 2: Adjust based on performance metrics if available
            var optimizedChunkSize = baseChunkSize;
            if (averageThroughputBps.HasValue && averageThroughputBps.Value > 0)
            {
                optimizedChunkSize = AdjustForThroughput(baseChunkSize, averageThroughputBps.Value);
            }

            if (averageRttMs.HasValue && averageRttMs.Value > 0)
            {
                optimizedChunkSize = AdjustForLatency(optimizedChunkSize, averageRttMs.Value);
            }

            // Ensure within bounds
            optimizedChunkSize = Math.Clamp(optimizedChunkSize, MinChunkSize, MaxChunkSize);

            // Round to nearest 64KB for alignment
            optimizedChunkSize = RoundToAlignment(optimizedChunkSize, 64 * 1024);

            _logger.LogDebug(
                "Recommended chunk size: {ChunkSize} bytes (file: {FileSize}, peers: {PeerCount}, throughput: {Throughput} B/s, RTT: {Rtt} ms)",
                optimizedChunkSize,
                fileSize,
                peerCount,
                averageThroughputBps ?? 0,
                averageRttMs ?? 0);

            return Task.FromResult((int)optimizedChunkSize);
        }

        /// <inheritdoc/>
        public int CalculateChunkSizeForTargetCount(long fileSize, int targetChunkCount)
        {
            if (fileSize <= 0 || targetChunkCount <= 0)
            {
                return DefaultChunkSize;
            }

            var chunkSize = (int)(fileSize / targetChunkCount);
            chunkSize = Math.Clamp(chunkSize, MinChunkSize, MaxChunkSize);
            chunkSize = RoundToAlignment(chunkSize, 64 * 1024);

            return chunkSize;
        }

        /// <summary>
        ///     Calculates base chunk size based on file size and peer count.
        /// </summary>
        private int CalculateBaseChunkSize(long fileSize, int peerCount)
        {
            // Target: enough chunks for good parallelism (2 chunks per peer)
            var targetChunkCount = Math.Clamp(peerCount * TargetChunksPerPeer, MinOptimalChunks, MaxOptimalChunks);

            // Calculate chunk size to achieve target count
            var chunkSize = (int)(fileSize / targetChunkCount);

            // For very small files, use minimum chunk size
            if (chunkSize < MinChunkSize)
            {
                return MinChunkSize;
            }

            // For very large files with many peers, cap chunk size to avoid too many chunks
            if (chunkSize > MaxChunkSize)
            {
                return MaxChunkSize;
            }

            return chunkSize;
        }

        /// <summary>
        ///     Adjusts chunk size based on average throughput.
        ///     Higher throughput = larger chunks (less overhead).
        /// </summary>
        private int AdjustForThroughput(int baseChunkSize, double averageThroughputBps)
        {
            // Normalize throughput to MB/s
            var throughputMBps = averageThroughputBps / (1024.0 * 1024.0);

            // For high throughput (>5 MB/s), use larger chunks
            if (throughputMBps > 5.0)
            {
                return (int)(baseChunkSize * 1.5);
            }

            // For medium throughput (1-5 MB/s), use base size
            if (throughputMBps > 1.0)
            {
                return baseChunkSize;
            }

            // For low throughput (<1 MB/s), use smaller chunks for faster failure recovery
            return (int)(baseChunkSize * 0.75);
        }

        /// <summary>
        ///     Adjusts chunk size based on average RTT.
        ///     Higher RTT = smaller chunks (faster failure recovery).
        /// </summary>
        private int AdjustForLatency(int baseChunkSize, double averageRttMs)
        {
            // For high latency (>500ms), use smaller chunks
            if (averageRttMs > 500)
            {
                return (int)(baseChunkSize * 0.8);
            }

            // For medium latency (100-500ms), use base size
            if (averageRttMs > 100)
            {
                return baseChunkSize;
            }

            // For low latency (<100ms), can use slightly larger chunks
            return (int)(baseChunkSize * 1.1);
        }

        /// <summary>
        ///     Rounds chunk size to nearest alignment boundary.
        /// </summary>
        private int RoundToAlignment(int chunkSize, int alignment)
        {
            return (chunkSize / alignment) * alignment;
        }
    }
}
