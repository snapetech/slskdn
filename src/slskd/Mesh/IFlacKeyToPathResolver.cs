// <copyright file="IFlacKeyToPathResolver.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Resolves a FLAC key to a local file path for proof-of-possession chunk reads (T-1434).
    ///     HashDb does not store paths; a real implementation would use shares or a path index.
    /// </summary>
    public interface IFlacKeyToPathResolver
    {
        /// <summary>
        ///     Tries to get the local file path for a FLAC key.
        /// </summary>
        /// <param name="flacKey">The FLAC key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Full file path if the file is locally available; null otherwise.</returns>
        Task<string?> TryGetFilePathAsync(string flacKey, CancellationToken cancellationToken = default);
    }
}
