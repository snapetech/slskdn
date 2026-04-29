// <copyright file="NoOpFlacKeyToPathResolver.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     No-op implementation of <see cref="IFlacKeyToPathResolver"/>. Always returns null.
    ///     Use when path resolution is not available; ReqChunk responses will have Success=false.
    /// </summary>
    public class NoOpFlacKeyToPathResolver : IFlacKeyToPathResolver
    {
        /// <inheritdoc/>
        public Task<string?> TryGetFilePathAsync(string flacKey, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
