// <copyright file="LidarrImportService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.Lidarr;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using slskd.Events;

public interface ILidarrImportService
{
    Task<LidarrImportResult> ImportCompletedDirectoryAsync(string localDirectory, CancellationToken cancellationToken = default);
}

public sealed class LidarrImportService : BackgroundService, ILidarrImportService
{
    private const string SubscriberName = "LidarrImportService.DownloadDirectoryComplete";

    public LidarrImportService(
        ILidarrClient lidarrClient,
        EventBus eventBus,
        IOptionsMonitor<global::slskd.Options> optionsMonitor)
    {
        LidarrClient = lidarrClient;
        EventBus = eventBus;
        OptionsMonitor = optionsMonitor;
    }

    private ILidarrClient LidarrClient { get; }

    private EventBus EventBus { get; }

    private IOptionsMonitor<global::slskd.Options> OptionsMonitor { get; }

    private ConcurrentDictionary<string, DateTime> RecentlyProcessed { get; } = new(StringComparer.Ordinal);

    private ILogger Log { get; } = Serilog.Log.ForContext<LidarrImportService>();

    public async Task<LidarrImportResult> ImportCompletedDirectoryAsync(string localDirectory, CancellationToken cancellationToken = default)
    {
        var options = OptionsMonitor.CurrentValue.Integration.Lidarr;
        if (!options.Enabled || !options.AutoImportCompleted)
        {
            return new LidarrImportResult { Enabled = options.Enabled, AutoImportEnabled = options.AutoImportCompleted };
        }

        if (string.IsNullOrWhiteSpace(localDirectory))
        {
            return new LidarrImportResult { Enabled = true, AutoImportEnabled = true, SkippedReason = "Directory is empty" };
        }

        var lidarrDirectory = MapPath(localDirectory, options.ImportPathFrom, options.ImportPathTo);
        if (IsDebounced(lidarrDirectory))
        {
            return new LidarrImportResult { Enabled = true, AutoImportEnabled = true, Directory = lidarrDirectory, SkippedReason = "Recently processed" };
        }

        var candidates = await LidarrClient
            .GetManualImportCandidatesAsync(
                lidarrDirectory,
                filterExistingFiles: true,
                replaceExistingFiles: options.ImportReplaceExistingFiles,
                cancellationToken)
            .ConfigureAwait(false);

        var safeCandidates = candidates
            .Where(candidate => candidate.IsSafeAutomaticImportCandidate)
            .ToList();

        foreach (var candidate in safeCandidates)
        {
            candidate.ReplaceExistingFiles = options.ImportReplaceExistingFiles;
        }

        var result = new LidarrImportResult
        {
            Enabled = true,
            AutoImportEnabled = true,
            Directory = lidarrDirectory,
            CandidateCount = candidates.Count,
            SafeCandidateCount = safeCandidates.Count,
            RejectedCandidateCount = candidates.Count - safeCandidates.Count,
        };

        if (safeCandidates.Count == 0)
        {
            result.SkippedReason = candidates.Count == 0
                ? "Lidarr found no import candidates"
                : "Lidarr candidates had rejections or ambiguous matches";
            Log.Information(
                "Lidarr auto-import skipped {Directory}: {Reason} ({Candidates} candidates)",
                lidarrDirectory,
                result.SkippedReason,
                candidates.Count);
            MarkProcessed(lidarrDirectory);
            return result;
        }

        var importMode = NormalizeImportMode(options.ImportMode);
        var command = await LidarrClient
            .StartManualImportAsync(safeCandidates, importMode, options.ImportReplaceExistingFiles, cancellationToken)
            .ConfigureAwait(false);

        result.CommandId = command.Id;
        result.ImportMode = importMode;
        Log.Information(
            "Queued Lidarr manual import command {CommandId} for {Directory}: {SafeCandidates}/{Candidates} safe candidates",
            command.Id,
            lidarrDirectory,
            safeCandidates.Count,
            candidates.Count);

        MarkProcessed(lidarrDirectory);
        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        EventBus.Subscribe<DownloadDirectoryCompleteEvent>(
            SubscriberName,
            async evt =>
            {
                var options = OptionsMonitor.CurrentValue.Integration.Lidarr;
                if (!options.Enabled || !options.AutoImportCompleted)
                {
                    return;
                }

                try
                {
                    await ImportCompletedDirectoryAsync(evt.LocalDirectoryName, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Lidarr auto-import failed for {Directory}: {Message}", evt.LocalDirectoryName, ex.Message);
                }
            });

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            EventBus.Unsubscribe<DownloadDirectoryCompleteEvent>(SubscriberName);
        }
    }

    private static string NormalizeImportMode(string importMode)
        => string.Equals(importMode, "copy", StringComparison.OrdinalIgnoreCase) ? "Copy" : "Move";

    private static string MapPath(string path, string fromPrefix, string toPrefix)
    {
        var fullPath = Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(fromPrefix) || string.IsNullOrWhiteSpace(toPrefix))
        {
            return fullPath;
        }

        var normalizedFrom = Path.GetFullPath(fromPrefix).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!IsSameOrChildPath(fullPath, normalizedFrom))
        {
            return fullPath;
        }

        var relative = fullPath[normalizedFrom.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (toPrefix.Contains('/') && !toPrefix.Contains('\\'))
        {
            return toPrefix.TrimEnd('/', '\\') + "/" + relative.Replace('\\', '/');
        }

        return Path.Combine(toPrefix, relative);
    }

    private bool IsDebounced(string directory)
    {
        var now = DateTime.UtcNow;
        foreach (var item in RecentlyProcessed.Where(item => now - item.Value > TimeSpan.FromHours(1)).ToArray())
        {
            RecentlyProcessed.TryRemove(item.Key, out _);
        }

        return RecentlyProcessed.ContainsKey(directory);
    }

    private void MarkProcessed(string directory)
    {
        RecentlyProcessed[directory] = DateTime.UtcNow;
    }

    private static bool IsSameOrChildPath(string path, string prefix)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(path, prefix, comparison))
        {
            return true;
        }

        if (!path.StartsWith(prefix, comparison))
        {
            return false;
        }

        var next = path.Length > prefix.Length ? path[prefix.Length] : '\0';
        return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
    }
}

public sealed record LidarrImportResult
{
    public bool Enabled { get; init; }

    public bool AutoImportEnabled { get; init; }

    public string Directory { get; init; } = string.Empty;

    public int CandidateCount { get; init; }

    public int SafeCandidateCount { get; init; }

    public int RejectedCandidateCount { get; init; }

    public int CommandId { get; set; }

    public string ImportMode { get; set; } = string.Empty;

    public string SkippedReason { get; set; } = string.Empty;
}
