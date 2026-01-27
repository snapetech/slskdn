// <copyright file="IChunkSizeOptimizer.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Optimization
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for optimizing chunk sizes for swarm downloads.
    /// </summary>
    public interface IChunkSizeOptimizer
    {
        /// <summary>
        ///     Recommends an optimal chunk size for a swarm download.
        /// </summary>
        /// <param name="fileSize">The total file size in bytes.</param>
        /// <param name="peerCount">The number of available peers.</param>
        /// <param name="averageThroughputBps">Average throughput across peers in bytes per second (optional).</param>
        /// <param name="averageRttMs">Average RTT across peers in milliseconds (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The recommended chunk size in bytes.</returns>
        Task<int> RecommendChunkSizeAsync(
            long fileSize,
            int peerCount,
            double? averageThroughputBps = null,
            double? averageRttMs = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Calculates the optimal number of chunks for a given file size and target chunk count.
        /// </summary>
        /// <param name="fileSize">The total file size in bytes.</param>
        /// <param name="targetChunkCount">The desired number of chunks.</param>
        /// <returns>The chunk size in bytes that would result in approximately the target chunk count.</returns>
        int CalculateChunkSizeForTargetCount(long fileSize, int targetChunkCount);
    }
}
