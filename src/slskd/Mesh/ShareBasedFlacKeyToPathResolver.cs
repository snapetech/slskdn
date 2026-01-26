// <copyright file="ShareBasedFlacKeyToPathResolver.cs" company="slskdn Team">
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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.HashDb.Models;
    using slskd.Shares;

    /// <summary>
    ///     Resolves FlacKey to local path using the share repository. Used to serve ReqChunk
    ///     for proof-of-possession (T-1434). Builds an in-memory FlacKey→path cache from
    ///     <see cref="IShareRepository.ListLocalPathsAndSizes"/> and refreshes it after
    ///     <see cref="CacheTTL"/>.
    /// </summary>
    public class ShareBasedFlacKeyToPathResolver : IFlacKeyToPathResolver
    {
        /// <summary>Default cache TTL: 5 minutes.</summary>
        public static readonly TimeSpan DefaultCacheTTL = TimeSpan.FromMinutes(5);

        private readonly IShareRepositoryFactory _repositoryFactory;
        private readonly string _localHostName;
        private readonly TimeSpan _cacheTTL;
        private readonly object _cacheLock = new();
        private Dictionary<string, string> _cache = new(StringComparer.Ordinal);
        private DateTime _cacheBuilt = DateTime.MinValue;

        /// <summary>
        ///     Creates a new resolver.
        /// </summary>
        /// <param name="repositoryFactory">Share repository factory.</param>
        /// <param name="localHostName">Local host name (e.g. <c>Program.LocalHostName</c>).</param>
        /// <param name="cacheTTL">How long to reuse the path cache before rebuilding. Default 5 minutes.</param>
        public ShareBasedFlacKeyToPathResolver(
            IShareRepositoryFactory repositoryFactory,
            string localHostName = "local",
            TimeSpan? cacheTTL = null)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _localHostName = localHostName ?? "local";
            _cacheTTL = cacheTTL ?? DefaultCacheTTL;
        }

        /// <inheritdoc/>
        public Task<string?> TryGetFilePathAsync(string flacKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(flacKey))
                return Task.FromResult<string?>(null);

            EnsureCache();

            lock (_cacheLock)
            {
                return Task.FromResult(_cache.TryGetValue(flacKey, out var path) ? path : null);
            }
        }

        private void EnsureCache()
        {
            lock (_cacheLock)
            {
                if ((DateTime.UtcNow - _cacheBuilt) < _cacheTTL)
                    return;

                var next = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    using var repo = _repositoryFactory.CreateFromHost(_localHostName);
                    var list = repo.ListLocalPathsAndSizes().ToList();
                    foreach (var (localPath, size) in list)
                    {
                        if (string.IsNullOrEmpty(localPath))
                            continue;
                        try
                        {
                            var key = HashDbEntry.GenerateFlacKey(localPath, size);
                            if (!string.IsNullOrEmpty(key))
                                next[key] = localPath;
                        }
                        catch
                        {
                            // Skip entries that fail key generation
                        }
                    }

                    // Only replace if we got a result; on exception leave old cache
                    _cache = next;
                    _cacheBuilt = DateTime.UtcNow;
                }
                catch
                {
                    // On error (e.g. no DB yet), leave _cache as-is and _cacheBuilt old so we retry next time
                }
            }
        }
    }
}
