// <copyright file="IPeerMetricsService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Transfers.MultiSource.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for tracking per-peer performance metrics.
    /// </summary>
    public interface IPeerMetricsService
    {
        /// <summary>
        ///     Get or create metrics for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="source">The peer source (Soulseek or Overlay).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Peer performance metrics.</returns>
        Task<PeerPerformanceMetrics> GetMetricsAsync(string peerId, PeerSource source, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Record an RTT sample for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="rttMs">RTT in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordRttSampleAsync(string peerId, double rttMs, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Record a throughput sample for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="bytesTransferred">Bytes transferred.</param>
        /// <param name="duration">Transfer duration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordThroughputSampleAsync(string peerId, long bytesTransferred, TimeSpan duration, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Record a chunk completion result for a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="result">The chunk completion result.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordChunkCompletionAsync(string peerId, ChunkCompletionResult result, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get ranked peers by performance.
        /// </summary>
        /// <param name="limit">Maximum number of peers to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of peer performance metrics ordered by quality.</returns>
        Task<List<PeerPerformanceMetrics>> GetRankedPeersAsync(int limit = 100, CancellationToken cancellationToken = default);
    }
}
















