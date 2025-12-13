namespace slskd.Transfers.MultiSource.Caching
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.HashDb;

    public interface IWarmCachePopularityService
    {
        Task RecordAccessAsync(string contentId, CancellationToken ct = default);

        Task<IReadOnlyList<string>> GetPopularContentAsync(int limit, CancellationToken ct = default);
    }

    public class WarmCachePopularityService : IWarmCachePopularityService
    {
        private readonly IHashDbService hashDb;
        private readonly IOptionsMonitor<WarmCacheOptions> optionsMonitor;

        public WarmCachePopularityService(IHashDbService hashDb, IOptionsMonitor<WarmCacheOptions> optionsMonitor)
        {
            this.hashDb = hashDb;
            this.optionsMonitor = optionsMonitor;
        }

        public async Task RecordAccessAsync(string contentId, CancellationToken ct = default)
        {
            var opts = optionsMonitor.CurrentValue;
            if (!opts.Enabled || string.IsNullOrWhiteSpace(contentId))
            {
                return;
            }

            await hashDb.IncrementPopularityAsync(contentId, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> GetPopularContentAsync(int limit, CancellationToken ct = default)
        {
            var opts = optionsMonitor.CurrentValue;
            if (!opts.Enabled)
            {
                return new List<string>();
            }

            // Translate min popularity threshold (0..1) to hits; for now, 1 == minimum.
            long minHits = opts.MinPopularityThreshold <= 0 ? 1 : 1;
            var top = await hashDb.GetTopPopularAsync(limit, minHits, ct).ConfigureAwait(false);
            return top.Select(t => t.ContentId).ToList();
        }
    }
}

















