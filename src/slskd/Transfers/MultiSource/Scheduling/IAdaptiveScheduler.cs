// <copyright file="IAdaptiveScheduler.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Scheduling;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Adaptive scheduler with learning and dynamic optimization.
/// </summary>
public interface IAdaptiveScheduler : IChunkScheduler
{
    /// <summary>
    ///     Records chunk completion feedback for learning.
    /// </summary>
    Task RecordChunkCompletionAsync(
        int chunkIndex,
        string peerId,
        bool success,
        long durationMs,
        long bytesTransferred,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adapts scheduling weights based on recent performance.
    /// </summary>
    Task AdaptWeightsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets adaptive scheduling statistics.
    /// </summary>
    AdaptiveSchedulingStats GetStats();
}
