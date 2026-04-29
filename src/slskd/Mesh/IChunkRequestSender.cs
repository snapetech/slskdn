// <copyright file="IChunkRequestSender.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Sends ReqChunk to a peer and returns the RespChunk payload. Used by proof-of-possession (T-1434).
    /// </summary>
    public interface IChunkRequestSender
    {
        /// <summary>
        ///     Requests a file chunk from a peer. Sends ReqChunk and waits for RespChunk.
        /// </summary>
        /// <param name="peer">Peer username.</param>
        /// <param name="flacKey">FLAC key.</param>
        /// <param name="offset">Byte offset.</param>
        /// <param name="length">Number of bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>DataBase64 and Success from the response; (null, false) on failure.</returns>
        Task<(string? DataBase64, bool Success)> RequestChunkAsync(string peer, string flacKey, long offset, int length, CancellationToken cancellationToken = default);
    }
}
