// <copyright file="IContentFetchBackend.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
