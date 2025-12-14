// <copyright file="WarmCacheService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Caching
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.HashDb;
    using slskd.HashDb.Models;

    public interface IWarmCacheService
    {
        Task RegisterAsync(string contentId, string path, long sizeBytes, bool pinned = false, CancellationToken ct = default);

        Task TouchAsync(string contentId, CancellationToken ct = default);

        Task EvictIfNeededAsync(CancellationToken ct = default);

        Task<IReadOnlyList<WarmCacheEntry>> ListAsync(CancellationToken ct = default);
    }

    /// <summary>
    ///     Warm cache manager (capacity + metadata). Does not delete files; leaves eviction actions to callers.
    /// </summary>
    public class WarmCacheService : IWarmCacheService
    {
        private readonly IHashDbService hashDb;
        private readonly IOptionsMonitor<WarmCacheOptions> optionsMonitor;

        public WarmCacheService(IHashDbService hashDb, IOptionsMonitor<WarmCacheOptions> optionsMonitor)
        {
            this.hashDb = hashDb;
            this.optionsMonitor = optionsMonitor;
        }

        public async Task RegisterAsync(string contentId, string path, long sizeBytes, bool pinned = false, CancellationToken ct = default)
        {
            var opts = optionsMonitor.CurrentValue;
            if (!opts.Enabled || string.IsNullOrWhiteSpace(contentId) || string.IsNullOrWhiteSpace(path) || sizeBytes <= 0)
            {
                return;
            }

            var entry = new WarmCacheEntry
            {
                ContentId = contentId,
                Path = path,
                SizeBytes = sizeBytes,
                Pinned = pinned,
                LastAccessed = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };

            await hashDb.UpsertWarmCacheEntryAsync(entry, ct).ConfigureAwait(false);
            await EvictIfNeededAsync(ct).ConfigureAwait(false);
        }

        public async Task TouchAsync(string contentId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return;
            }

            var existing = await hashDb.GetWarmCacheEntryAsync(contentId, ct).ConfigureAwait(false);
            if (existing == null)
            {
                return;
            }

            existing.LastAccessed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await hashDb.UpsertWarmCacheEntryAsync(existing, ct).ConfigureAwait(false);
        }

        public async Task EvictIfNeededAsync(CancellationToken ct = default)
        {
            var opts = optionsMonitor.CurrentValue;
            if (!opts.Enabled)
            {
                return;
            }

            var maxBytes = opts.MaxStorageGb * 1024L * 1024L * 1024L;
            var total = await hashDb.GetWarmCacheTotalSizeAsync(ct).ConfigureAwait(false);
            if (total <= maxBytes)
            {
                return;
            }

            var entries = await hashDb.ListWarmCacheEntriesAsync(ct).ConfigureAwait(false);
            var toEvict = entries
                .Where(e => !e.Pinned)
                .OrderBy(e => e.LastAccessed)
                .ToList();

            long reclaimed = 0;
            foreach (var e in toEvict)
            {
                if (total - reclaimed <= maxBytes)
                {
                    break;
                }

                // Do not delete files here; caller handles actual removal. We only drop metadata.
                await hashDb.DeleteWarmCacheEntryAsync(e.ContentId, ct).ConfigureAwait(false);
                reclaimed += e.SizeBytes;
            }
        }

        public Task<IReadOnlyList<WarmCacheEntry>> ListAsync(CancellationToken ct = default)
        {
            return hashDb.ListWarmCacheEntriesAsync(ct);
        }
    }
}
