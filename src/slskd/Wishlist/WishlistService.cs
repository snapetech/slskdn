// <copyright file="WishlistService.cs" company="slskd Team">
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

namespace slskd.Wishlist
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Serilog;
    using slskd.Search;
    using slskd.Transfers.Downloads;
    using slskd.Transfers.Ranking;
    using Soulseek;
    using SlskdSearch = slskd.Search.Search;

    /// <summary>
    ///     Wishlist service interface.
    /// </summary>
    public interface IWishlistService
    {
        /// <summary>
        ///     Gets all wishlist items.
        /// </summary>
        Task<List<WishlistItem>> ListAsync();

        /// <summary>
        ///     Gets a wishlist item by ID.
        /// </summary>
        Task<WishlistItem> GetAsync(Guid id);

        /// <summary>
        ///     Creates a new wishlist item.
        /// </summary>
        Task<WishlistItem> CreateAsync(WishlistItem item);

        /// <summary>
        ///     Updates an existing wishlist item.
        /// </summary>
        Task<WishlistItem> UpdateAsync(WishlistItem item);

        /// <summary>
        ///     Deletes a wishlist item.
        /// </summary>
        Task DeleteAsync(Guid id);

        /// <summary>
        ///     Manually triggers a search for a wishlist item.
        /// </summary>
        Task<SlskdSearch> RunSearchAsync(Guid id, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Handles wishlist management and background searches.
    /// </summary>
    public class WishlistService : BackgroundService, IWishlistService
    {
        public WishlistService(
            IDbContextFactory<WishlistDbContext> contextFactory,
            ISearchService searchService,
            ISoulseekClient soulseekClient,
            IOptionsMonitor<slskd.Options> optionsMonitor,
            ISourceRankingService rankingService,
            IDownloadService downloadService)
        {
            ContextFactory = contextFactory;
            SearchService = searchService;
            Client = soulseekClient;
            OptionsMonitor = optionsMonitor;
            RankingService = rankingService;
            DownloadService = downloadService;
        }

        private IDbContextFactory<WishlistDbContext> ContextFactory { get; }
        private ISearchService SearchService { get; }
        private ISoulseekClient Client { get; }
        private IOptionsMonitor<slskd.Options> OptionsMonitor { get; }
        private ISourceRankingService RankingService { get; }
        private IDownloadService DownloadService { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<WishlistService>();

        /// <inheritdoc/>
        public async Task<List<WishlistItem>> ListAsync()
        {
            using var context = ContextFactory.CreateDbContext();
            return await context.WishlistItems
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<WishlistItem> GetAsync(Guid id)
        {
            using var context = ContextFactory.CreateDbContext();
            return await context.WishlistItems.FindAsync(id);
        }

        /// <inheritdoc/>
        public async Task<WishlistItem> CreateAsync(WishlistItem item)
        {
            using var context = ContextFactory.CreateDbContext();

            item.Id = Guid.NewGuid();
            item.CreatedAt = DateTime.UtcNow;

            context.WishlistItems.Add(item);
            await context.SaveChangesAsync();

            Log.Information("Created wishlist item {Id} for search: {SearchText}", item.Id, item.SearchText);
            return item;
        }

        /// <inheritdoc/>
        public async Task<WishlistItem> UpdateAsync(WishlistItem item)
        {
            using var context = ContextFactory.CreateDbContext();

            var existing = await context.WishlistItems.FindAsync(item.Id);
            if (existing == null)
            {
                throw new NotFoundException($"Wishlist item {item.Id} not found");
            }

            existing.SearchText = item.SearchText;
            existing.Filter = item.Filter;
            existing.Enabled = item.Enabled;
            existing.AutoDownload = item.AutoDownload;
            existing.MaxResults = item.MaxResults;

            await context.SaveChangesAsync();

            Log.Information("Updated wishlist item {Id}", item.Id);
            return existing;
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(Guid id)
        {
            using var context = ContextFactory.CreateDbContext();

            var item = await context.WishlistItems.FindAsync(id);
            if (item != null)
            {
                context.WishlistItems.Remove(item);
                await context.SaveChangesAsync();
                Log.Information("Deleted wishlist item {Id}", id);
            }
        }

        /// <inheritdoc/>
        public async Task<SlskdSearch> RunSearchAsync(Guid id, CancellationToken cancellationToken = default)
        {
            using var context = ContextFactory.CreateDbContext();

            var item = await context.WishlistItems.FindAsync(id);
            if (item == null)
            {
                throw new NotFoundException($"Wishlist item {id} not found");
            }

            return await ExecuteWishlistSearchAsync(item, context, cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Wishlist background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var options = OptionsMonitor.CurrentValue;
                var intervalSeconds = options.Wishlist?.IntervalSeconds ?? 3600;

                try
                {
                    if (options.Wishlist?.Enabled == true && Client.State.HasFlag(SoulseekClientStates.Connected))
                    {
                        await ProcessWishlistItemsAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing wishlist items: {Message}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }

            Log.Information("Wishlist background service stopped");
        }

        private async Task ProcessWishlistItemsAsync(CancellationToken cancellationToken)
        {
            using var context = ContextFactory.CreateDbContext();

            var enabledItems = await context.WishlistItems
                .Where(w => w.Enabled)
                .ToListAsync(cancellationToken);

            Log.Information("Processing {Count} enabled wishlist items", enabledItems.Count);

            foreach (var item in enabledItems)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await ExecuteWishlistSearchAsync(item, context, cancellationToken);

                    // Small delay between searches to avoid hammering the network
                    await Task.Delay(5000, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error executing wishlist search for {Id}: {Message}", item.Id, ex.Message);
                }
            }
        }

        private async Task<SlskdSearch> ExecuteWishlistSearchAsync(
            WishlistItem item,
            WishlistDbContext context,
            CancellationToken cancellationToken)
        {
            Log.Information("Executing wishlist search: {SearchText}", item.SearchText);

            var searchId = Guid.NewGuid();
            var query = new SearchQuery(item.SearchText);
            var scope = SearchScope.Network;

            var searchOptions = new SearchOptions(
                searchTimeout: 15000,
                responseLimit: item.MaxResults,
                filterResponses: !string.IsNullOrEmpty(item.Filter));

            var search = await SearchService.StartAsync(searchId, query, scope, searchOptions);

            // Wait for search to complete
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);

            // Get full search with responses
            var searchWithResponses = await SearchService.FindAsync(s => s.Id == searchId, includeResponses: true);

            // Update wishlist item stats
            item.LastSearchedAt = DateTime.UtcNow;
            item.LastSearchId = searchId;
            item.TotalSearchCount++;
            item.LastMatchCount = searchWithResponses?.ResponseCount ?? 0;

            context.WishlistItems.Update(item);
            await context.SaveChangesAsync(cancellationToken);

            Log.Information("Wishlist search {Id} completed with {Count} responses", searchId, item.LastMatchCount);

            // If auto-download is enabled and we have results, download the best ones
            if (item.AutoDownload && searchWithResponses?.Responses?.Any() == true)
            {
                await AutoDownloadBestResultsAsync(searchWithResponses, cancellationToken);
            }

            return search;
        }

        private async Task AutoDownloadBestResultsAsync(SlskdSearch search, CancellationToken cancellationToken)
        {
            try
            {
                // Collect all files from all responses
                var candidates = new List<SourceCandidate>();

                foreach (var response in search.Responses)
                {
                    foreach (var file in response.Files)
                    {
                        candidates.Add(new SourceCandidate
                        {
                            Username = response.Username,
                            Filename = file.Filename,
                            Size = file.Size,
                            HasFreeUploadSlot = response.HasFreeUploadSlot,
                            QueueLength = (int)response.QueueLength,
                            UploadSpeed = response.UploadSpeed,
                        });
                    }
                }

                if (candidates.Count == 0)
                {
                    return;
                }

                // Rank all candidates using smart scoring
                var rankedCandidates = await RankingService.RankSourcesAsync(candidates, cancellationToken);

                // Take the top result (best scored)
                var best = rankedCandidates.FirstOrDefault();
                if (best == null)
                {
                    return;
                }

                Log.Information(
                    "Auto-downloading best result: {Filename} from {Username} (score: {Score:F1})",
                    best.Filename,
                    best.Username,
                    best.SmartScore);

                // Enqueue the download
                await DownloadService.EnqueueAsync(
                    best.Username,
                    new[] { (best.Filename, best.Size) },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error auto-downloading wishlist results: {Message}", ex.Message);
            }
        }
    }
}

