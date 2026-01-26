// <copyright file="NoOpFlacKeyToPathResolver.cs" company="slskdn Team">
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
    ///     No-op implementation of <see cref="IFlacKeyToPathResolver"/>. Always returns null.
    ///     Use when path resolution is not available; ReqChunk responses will have Success=false.
    /// </summary>
    public class NoOpFlacKeyToPathResolver : IFlacKeyToPathResolver
    {
        /// <inheritdoc/>
        public Task<string?> TryGetFilePathAsync(string flacKey, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
