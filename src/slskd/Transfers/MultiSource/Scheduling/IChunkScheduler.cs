// <copyright file="IChunkScheduler.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Transfers.MultiSource.Scheduling
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for chunk scheduling strategies.
    /// </summary>
    public interface IChunkScheduler
    {
        /// <summary>
        ///     Assign a single chunk to the best available peer.
        /// </summary>
        /// <param name="request">The chunk request.</param>
        /// <param name="availablePeers">List of available peer IDs.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The chunk assignment result.</returns>
        Task<ChunkAssignment> AssignChunkAsync(
            ChunkRequest request,
            List<string> availablePeers,
            CancellationToken ct = default);

        /// <summary>
        ///     Assign multiple chunks to peers in a batch.
        /// </summary>
        /// <param name="requests">List of chunk requests.</param>
        /// <param name="availablePeers">List of available peer IDs.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of chunk assignment results.</returns>
        Task<List<ChunkAssignment>> AssignMultipleChunksAsync(
            List<ChunkRequest> requests,
            List<string> availablePeers,
            CancellationToken ct = default);

        /// <summary>
        ///     Handle peer degradation event (high error rate, slow throughput, etc).
        /// </summary>
        /// <param name="peerId">The degraded peer ID.</param>
        /// <param name="reason">Reason for degradation.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of chunk indices that should be reassigned (T-1405).</returns>
        Task<List<int>> HandlePeerDegradationAsync(
            string peerId,
            DegradationReason reason,
            CancellationToken ct = default);

        /// <summary>
        ///     Register active chunk assignments for tracking (T-1405).
        /// </summary>
        /// <param name="chunkIndex">The chunk index.</param>
        /// <param name="peerId">The peer assigned to this chunk.</param>
        void RegisterAssignment(int chunkIndex, string peerId);

        /// <summary>
        ///     Unregister chunk assignment when completed or cancelled.
        /// </summary>
        /// <param name="chunkIndex">The chunk index.</param>
        void UnregisterAssignment(int chunkIndex);
    }
}
