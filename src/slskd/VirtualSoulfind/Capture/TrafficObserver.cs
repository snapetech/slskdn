// <copyright file="TrafficObserver.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Capture;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soulseek;
using slskd;
using OptionsModel = slskd.Options;

public interface ITrafficObserver
{
    Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct = default);
    Task OnTransferCompleteAsync(Transfers.Transfer transfer, CancellationToken ct = default);
}

/// <summary>
/// Observes Soulseek traffic (search results and transfers) and feeds to normalization pipeline.
/// Phase 6A: T-800 - Real implementation.
/// </summary>
public class TrafficObserver : ITrafficObserver
{
    private readonly ILogger<TrafficObserver> logger;
    private readonly INormalizationPipeline normalization;
    private readonly IObservationStore observationStore;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;

    public TrafficObserver(
        ILogger<TrafficObserver> logger,
        INormalizationPipeline normalization,
        IObservationStore observationStore,
        IOptionsMonitor<OptionsModel> optionsMonitor)
    {
        this.logger = logger;
        this.normalization = normalization;
        this.observationStore = observationStore;
        this.optionsMonitor = optionsMonitor;
    }

    public async Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Capture?.Enabled != true)
        {
            return;
        }

        try
        {
            // SearchResponse is a single user's response, iterate over Files directly
            foreach (var file in response.Files)
            {
                var observation = new SearchObservation
                {
                    ObservationId = Ulid.NewUlid().ToString(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Query = query,
                    SoulseekUsername = response.Username,
                    FilePath = file.Filename,
                    SizeBytes = file.Size,
                    BitRate = file.BitRate,
                    DurationSeconds = file.Length,
                    Extension = System.IO.Path.GetExtension(file.Filename)
                };

                // Extract metadata from path (heuristic)
                ExtractMetadataFromPath(observation);

                // Store observation (optional, for debugging)
                await observationStore.StoreSearchObservationAsync(observation, ct);

                // Send to normalization pipeline
                await normalization.ProcessSearchObservationAsync(observation, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-CAPTURE] Failed to process search results for query: {Query}", query);
        }
    }

    public async Task OnTransferCompleteAsync(Transfers.Transfer transfer, CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Capture?.Enabled != true)
        {
            return;
        }

        // Only process completed downloads
        if (transfer.Direction != Soulseek.TransferDirection.Download ||
            !transfer.State.HasFlag(Soulseek.TransferStates.Completed) ||
            !transfer.State.HasFlag(Soulseek.TransferStates.Succeeded))
        {
            return;
        }

        try
        {
            var optionsValue = optionsMonitor.CurrentValue;
            var localPath = System.IO.Path.Combine(
                optionsValue.Directories.Downloads,
                transfer.Filename.ToLocalFilename(baseDirectory: optionsValue.Directories.Downloads));

            if (!System.IO.File.Exists(localPath))
            {
                logger.LogDebug("[VSF-CAPTURE] Transfer completed but file not found: {Path}", localPath);
                return;
            }

            // Check if file meets minimum size and extension requirements
            var fileInfo = new System.IO.FileInfo(localPath);
            var captureOptions = options.VirtualSoulfind.Capture;
            if (fileInfo.Length < captureOptions.MinimumFileSizeBytes)
            {
                return;
            }

            var ext = System.IO.Path.GetExtension(localPath).ToLowerInvariant();
            if (!captureOptions.AudioExtensions.Contains(ext))
            {
                return;
            }

            var observation = new TransferObservation
            {
                TransferId = transfer.Id.ToString("N"),
                CompletedAt = transfer.EndedAt ?? DateTimeOffset.UtcNow,
                SoulseekUsername = transfer.Username,
                FilePath = transfer.Filename,
                LocalPath = localPath,
                SizeBytes = transfer.Size,
                Duration = transfer.ElapsedTime ?? TimeSpan.Zero,
                ThroughputBytesPerSec = transfer.AverageSpeed > 0 ? transfer.AverageSpeed : 0.0,
                Success = true
            };

            // Store observation (optional, for debugging)
            await observationStore.StoreTransferObservationAsync(observation, ct);

            // Send to normalization (includes fingerprinting)
            await normalization.ProcessTransferObservationAsync(observation, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-CAPTURE] Failed to process transfer completion for {TransferId}", transfer.Id);
        }
    }

    private void ExtractMetadataFromPath(SearchObservation obs)
    {
        // Heuristic parsing of "Artist - Album/Track.flac" style paths
        // This is best-effort; real metadata comes from fingerprinting
        var parts = obs.FilePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)
        {
            // Common pattern: "Artist/Album/Track.ext"
            obs.Artist = parts[0];
            obs.Album = parts.Length > 2 ? parts[1] : null;
            obs.Title = System.IO.Path.GetFileNameWithoutExtension(parts[^1]);
        }
        else if (parts.Length == 1)
        {
            // Try "Artist - Title.ext" pattern
            var filename = System.IO.Path.GetFileNameWithoutExtension(parts[0]);
            var dashIndex = filename.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                obs.Artist = filename.Substring(0, dashIndex).Trim();
                obs.Title = filename.Substring(dashIndex + 3).Trim();
            }
        }
    }
}
