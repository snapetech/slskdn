// <copyright file="AutoReplaceBackgroundService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Transfers.AutoReplace
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Background service for automatic retry/replacement of stuck downloads.
    /// </summary>
    public class AutoReplaceBackgroundService : BackgroundService
    {
        private static readonly string StateFileName = "auto-replace-state.json";

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoReplaceBackgroundService"/> class.
        /// </summary>
        public AutoReplaceBackgroundService(
            IAutoReplaceService autoReplaceService,
            ISoulseekClient client,
            IOptionsMonitor<slskd.Options> optionsMonitor,
            OptionsAtStartup optionsAtStartup)
        {
            AutoReplaceService = autoReplaceService;
            Client = client;
            OptionsMonitor = optionsMonitor;
            OptionsAtStartup = optionsAtStartup;

            StateFilePath = Path.Combine(Program.AppDirectory, StateFileName);
            LoadState();
        }

        /// <summary>
        ///     Gets a value indicating whether auto-replace is currently enabled.
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        ///     Gets the last time auto-replace was run.
        /// </summary>
        public DateTime? LastRunAt { get; private set; }

        /// <summary>
        ///     Gets the number of downloads processed in the last run.
        /// </summary>
        public int LastRunProcessedCount { get; private set; }

        /// <summary>
        ///     Gets the number of downloads replaced in the last run.
        /// </summary>
        public int LastRunReplacedCount { get; private set; }

        private IAutoReplaceService AutoReplaceService { get; }
        private ISoulseekClient Client { get; }
        private IOptionsMonitor<slskd.Options> OptionsMonitor { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private string StateFilePath { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<AutoReplaceBackgroundService>();

        /// <summary>
        ///     Enables auto-replace and persists the state.
        /// </summary>
        public void Enable()
        {
            IsEnabled = true;
            SaveState();
            Log.Information("Auto-replace enabled");
        }

        /// <summary>
        ///     Disables auto-replace and persists the state.
        /// </summary>
        public void Disable()
        {
            IsEnabled = false;
            SaveState();
            Log.Information("Auto-replace disabled");
        }

        /// <summary>
        ///     Gets the current status of the auto-replace service.
        /// </summary>
        public AutoReplaceStatus GetStatus()
        {
            return new AutoReplaceStatus
            {
                Enabled = IsEnabled,
                LastRunAt = LastRunAt,
                LastRunProcessedCount = LastRunProcessedCount,
                LastRunReplacedCount = LastRunReplacedCount,
                IntervalSeconds = OptionsMonitor.CurrentValue.AutoReplace?.IntervalSeconds ?? 300,
            };
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
            await Task.Yield();

            Log.Information("Auto-replace background service started (enabled: {Enabled})", IsEnabled);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var intervalSeconds = OptionsMonitor.CurrentValue.AutoReplace?.IntervalSeconds ?? 300;

                    try
                    {
                        if (IsEnabled && Client.State.HasFlag(SoulseekClientStates.Connected))
                        {
                            await ProcessStuckDownloadsAsync(stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing auto-replace: {Message}", ex.Message);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }

            Log.Information("Auto-replace background service stopped");
        }

        private async Task ProcessStuckDownloadsAsync(CancellationToken cancellationToken)
        {
            Log.Debug("Running auto-replace cycle...");

            var request = new AutoReplaceRequest
            {
                Threshold = OptionsMonitor.CurrentValue.AutoReplace?.SizeThresholdPercent ?? 5.0,
            };

            var result = await AutoReplaceService.ProcessStuckDownloadsAsync(request, cancellationToken);

            var totalProcessed = result.Replaced + result.Failed + result.Skipped;

            LastRunAt = DateTime.UtcNow;
            LastRunProcessedCount = totalProcessed;
            LastRunReplacedCount = result.Replaced;

            if (totalProcessed > 0)
            {
                Log.Information(
                    "Auto-replace cycle complete: {Processed} processed, {Replaced} replaced, {Failed} failed, {Skipped} skipped",
                    totalProcessed,
                    result.Replaced,
                    result.Failed,
                    result.Skipped);
            }
            else
            {
                Log.Debug("Auto-replace cycle complete: no stuck downloads found");
            }
        }

        private void LoadState()
        {
            try
            {
                var configuredDefault = OptionsAtStartup.Global.Download.AutoReplaceStuck;

                if (System.IO.File.Exists(StateFilePath))
                {
                    var json = System.IO.File.ReadAllText(StateFilePath);
                    var state = JsonSerializer.Deserialize<AutoReplaceState>(json);
                    IsEnabled = state?.UserConfigured == true
                        ? state.Enabled
                        : configuredDefault;
                    Log.Debug("Loaded auto-replace state: enabled={Enabled}", IsEnabled);
                }
                else
                {
                    IsEnabled = configuredDefault;
                    SaveState();
                    Log.Information("Auto-replace state file not found, defaulting to configured value: {Enabled}", IsEnabled);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load auto-replace state, defaulting to configured value");
                IsEnabled = OptionsAtStartup.Global.Download.AutoReplaceStuck;
            }
        }

        private void SaveState()
        {
            try
            {
                var state = new AutoReplaceState { Enabled = IsEnabled, UserConfigured = true };
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(StateFilePath, json);
                Log.Debug("Saved auto-replace state: enabled={Enabled}", IsEnabled);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save auto-replace state");
            }
        }

        private class AutoReplaceState
        {
            public bool Enabled { get; set; }

            public bool UserConfigured { get; set; }
        }
    }

    /// <summary>
    ///     Status of the auto-replace background service.
    /// </summary>
    public class AutoReplaceStatus
    {
        /// <summary>
        ///     Gets or sets a value indicating whether auto-replace is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        ///     Gets or sets the last time auto-replace was run.
        /// </summary>
        public DateTime? LastRunAt { get; set; }

        /// <summary>
        ///     Gets or sets the number of downloads processed in the last run.
        /// </summary>
        public int LastRunProcessedCount { get; set; }

        /// <summary>
        ///     Gets or sets the number of downloads replaced in the last run.
        /// </summary>
        public int LastRunReplacedCount { get; set; }

        /// <summary>
        ///     Gets or sets the interval between auto-replace runs in seconds.
        /// </summary>
        public int IntervalSeconds { get; set; }
    }
}
