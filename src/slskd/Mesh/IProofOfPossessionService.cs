// <copyright file="IProofOfPossessionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Verifies that a peer possesses the file for a hash entry by requesting a chunk and checking its SHA256 (T-1434).
    /// </summary>
    public interface IProofOfPossessionService
    {
        /// <summary>
        ///     Verifies the peer has the file: requests first 32KB (or less if file is smaller), SHA256, compares to expectedByteHash.
        /// </summary>
        /// <param name="peer">Peer username.</param>
        /// <param name="flacKey">FLAC key.</param>
        /// <param name="expectedByteHash">Expected SHA256 of first 32KB (hex).</param>
        /// <param name="size">File size in bytes.</param>
        /// <param name="chunkSender">Sender used to request the chunk (avoids DI cycle with MeshSyncService).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the peer returned a chunk whose SHA256 matches expectedByteHash.</returns>
        Task<bool> VerifyAsync(string peer, string flacKey, string expectedByteHash, long size, IChunkRequestSender chunkSender, CancellationToken cancellationToken = default);
    }
}
