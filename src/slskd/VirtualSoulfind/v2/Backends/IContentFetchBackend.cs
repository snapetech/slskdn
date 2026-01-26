// <copyright file="IContentFetchBackend.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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

namespace slskd.VirtualSoulfind.v2.Backends
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Optional interface for backends that can fetch content to a stream.
    ///     Used by the resolver to perform the actual download after validation.
    /// </summary>
    /// <remarks>
    ///     Http, WebDav, and S3 implement this. Torrent uses IBitTorrentBackend.FetchByInfoHashOrMagnetAsync.
    ///     NativeMesh requires the mesh GetContentByContentId RPC (not yet implemented).
    /// </remarks>
    public interface IContentFetchBackend : IContentBackend
    {
        /// <summary>
        ///     Fetches content for the candidate and writes it to the destination stream.
        /// </summary>
        /// <param name="candidate">The validated source candidate.</param>
        /// <param name="destination">Stream to write the content to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        ///     Must enforce the same security (allowlist, MaxFileSizeBytes) as ValidateCandidateAsync.
        ///     Throws on failure (network error, size exceeded, etc.).
        /// </remarks>
        Task FetchToStreamAsync(
            SourceCandidate candidate,
            Stream destination,
            CancellationToken cancellationToken = default);
    }
}
