namespace slskd.VirtualSoulfind.Capture;

using Microsoft.Extensions.Options;
using Soulseek;
using slskd.Search;
using slskd.Transfers;

/// <summary>
/// Interface for observing Soulseek traffic.
/// </summary>
public interface ITrafficObserver
{
    /// <summary>
    /// Called when search results are received from Soulseek server.
    /// </summary>
    Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct = default);
    
    /// <summary>
    /// Called when a Soulseek transfer completes.
    /// </summary>
    Task OnTransferCompleteAsync(Transfer transfer, CancellationToken ct = default);
}

/// <summary>
/// Passively observes Soulseek traffic to build knowledge graph.
/// </summary>
public class TrafficObserver : ITrafficObserver
{
    private readonly ILogger<TrafficObserver> logger;
    private readonly INormalizationPipeline normalization;
    private readonly IOptionsMonitor<Options> optionsMonitor;

    public TrafficObserver(
        ILogger<TrafficObserver> logger,
        INormalizationPipeline normalization,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.normalization = normalization;
        this.optionsMonitor = optionsMonitor;
    }

    public async Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Capture?.Enabled != true)
        {
            return;
        }

        logger.LogDebug("[VSF-CAPTURE] Observing search results for query: {Query}, {ResponseCount} responses",
            query, response.ResponseCount);

        try
        {
            foreach (var user in response.Responses)
            {
                foreach (var file in user.Files)
                {
                    var observation = new SearchObservation
                    {
                        ObservationId = Ulid.NewUlid().ToString(),
                        Timestamp = DateTimeOffset.UtcNow,
                        Query = query,
                        SoulseekUsername = user.Username,
                        FilePath = file.Filename,
                        SizeBytes = file.Size,
                        BitRate = file.BitRate,
                        DurationSeconds = file.Length,
                        Extension = Path.GetExtension(file.Filename)
                    };
                    
                    // Extract metadata from path (heuristic)
                    ExtractMetadataFromPath(observation);
                    
                    // Send to normalization pipeline
                    await normalization.ProcessSearchObservationAsync(observation, ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-CAPTURE] Failed to process search results");
        }
    }

    public async Task OnTransferCompleteAsync(Transfer transfer, CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Capture?.Enabled != true)
        {
            return;
        }

        if (transfer.State != TransferStates.Completed)
        {
            return;
        }

        logger.LogDebug("[VSF-CAPTURE] Observing completed transfer: {Username}/{Filename}",
            transfer.Username, transfer.Filename);

        try
        {
            var observation = new TransferObservation
            {
                TransferId = transfer.Id.ToString(),
                CompletedAt = DateTimeOffset.UtcNow,
                SoulseekUsername = transfer.Username,
                FilePath = transfer.Filename,
                LocalPath = transfer.Data.LocalFilename,
                SizeBytes = transfer.Size,
                Duration = transfer.ElapsedTime ?? TimeSpan.Zero,
                ThroughputBytesPerSec = transfer.AverageSpeed ?? 0,
                Success = true
            };
            
            // Send to normalization (includes fingerprinting)
            await normalization.ProcessTransferObservationAsync(observation, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-CAPTURE] Failed to process transfer observation");
        }
    }

    private void ExtractMetadataFromPath(SearchObservation obs)
    {
        // Heuristic parsing of "Artist - Album/Track.flac" style paths
        // This is best-effort; real metadata comes from fingerprinting
        try
        {
            var parts = obs.FilePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length >= 2)
            {
                // Common pattern: "Artist/Album/Track.ext"
                obs.Artist = parts[0];
                obs.Album = parts.Length > 2 ? parts[1] : null;
                obs.Title = Path.GetFileNameWithoutExtension(parts[^1]);
            }
            else if (parts.Length == 1)
            {
                // Just filename, try to parse "Artist - Title.ext"
                var filename = Path.GetFileNameWithoutExtension(parts[0]);
                var dashIndex = filename.IndexOf(" - ", StringComparison.Ordinal);
                if (dashIndex > 0)
                {
                    obs.Artist = filename.Substring(0, dashIndex).Trim();
                    obs.Title = filename.Substring(dashIndex + 3).Trim();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-CAPTURE] Failed to extract metadata from path: {Path}", obs.FilePath);
        }
    }
}
