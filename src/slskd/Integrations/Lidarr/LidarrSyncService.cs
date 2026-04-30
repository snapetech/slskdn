// <copyright file="LidarrSyncService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.Lidarr;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using slskd.Wishlist;

public interface ILidarrSyncService
{
    Task<LidarrSyncResult> SyncWantedToWishlistAsync(CancellationToken cancellationToken = default);
}

public sealed class LidarrSyncService : BackgroundService, ILidarrSyncService
{
    public LidarrSyncService(
        ILidarrClient lidarrClient,
        IWishlistService wishlistService,
        IOptionsMonitor<global::slskd.Options> optionsMonitor)
    {
        LidarrClient = lidarrClient;
        WishlistService = wishlistService;
        OptionsMonitor = optionsMonitor;
    }

    private ILidarrClient LidarrClient { get; }

    private IWishlistService WishlistService { get; }

    private IOptionsMonitor<global::slskd.Options> OptionsMonitor { get; }

    private ILogger Log { get; } = Serilog.Log.ForContext<LidarrSyncService>();

    public async Task<LidarrSyncResult> SyncWantedToWishlistAsync(CancellationToken cancellationToken = default)
    {
        var options = OptionsMonitor.CurrentValue.Integration.Lidarr;
        if (!options.Enabled)
        {
            return new LidarrSyncResult { Enabled = false };
        }

        var wanted = await LidarrClient.GetWantedMissingAsync(options.MaxItemsPerSync, cancellationToken).ConfigureAwait(false);
        var existing = await WishlistService.ListAsync().ConfigureAwait(false);
        var existingSearches = existing
            .Select(item => item.SearchText)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new LidarrSyncResult
        {
            Enabled = true,
            WantedCount = wanted.Count,
        };

        foreach (var album in wanted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchText = album.SearchText.Trim();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                result.SkippedCount++;
                continue;
            }

            if (existingSearches.Contains(searchText))
            {
                result.DuplicateCount++;
                continue;
            }

            var item = new WishlistItem
            {
                SearchText = searchText,
                Filter = options.WishlistFilter,
                Enabled = true,
                AutoDownload = options.AutoDownload,
                MaxResults = options.WishlistMaxResults,
            };

            await WishlistService.CreateAsync(item).ConfigureAwait(false);
            existingSearches.Add(searchText);
            result.CreatedCount++;
        }

        Log.Information(
            "Lidarr wanted sync complete: {Created} created, {Duplicates} duplicates, {Skipped} skipped from {Wanted} wanted albums",
            result.CreatedCount,
            result.DuplicateCount,
            result.SkippedCount,
            result.WantedCount);

        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = OptionsMonitor.CurrentValue.Integration.Lidarr;
            var delay = TimeSpan.FromSeconds(Math.Max(300, options.SyncIntervalSeconds));

            try
            {
                if (options.Enabled && options.SyncWantedToWishlist)
                {
                    await SyncWantedToWishlistAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Lidarr wanted sync failed: {Message}", ex.Message);
            }

            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}

public sealed record LidarrSyncResult
{
    public bool Enabled { get; init; }

    public int WantedCount { get; init; }

    public int CreatedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int SkippedCount { get; set; }
}
