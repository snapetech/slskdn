// <copyright file="IChunkRequestSender.cs" company="slskdn Team">
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
