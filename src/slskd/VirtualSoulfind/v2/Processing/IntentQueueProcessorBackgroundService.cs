// <copyright file="IntentQueueProcessorBackgroundService.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Processing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Background service that continuously processes the intent queue.
    /// </summary>
    /// <remarks>
    ///     This is the automation engine that makes VirtualSoulfind v2 autonomous.
    ///     It runs in the background, checking for pending intents every N seconds
    ///     and automatically acquiring content.
    /// </remarks>
    public sealed class IntentQueueProcessorBackgroundService : BackgroundService
    {
        private readonly IIntentQueueProcessor _processor;
        private readonly ILogger<IntentQueueProcessorBackgroundService> _logger;
        private readonly IOptionsMonitor<IntentQueueProcessorOptions> _options;

        /// <summary>
        ///     Initializes a new instance of the <see cref="IntentQueueProcessorBackgroundService"/> class.
        /// </summary>
        public IntentQueueProcessorBackgroundService(
            IIntentQueueProcessor processor,
            IOptionsMonitor<IntentQueueProcessorOptions> options,
            ILogger<IntentQueueProcessorBackgroundService> logger)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
            await Task.Yield();

            var opts = _options.CurrentValue;

            if (!opts.Enabled)
            {
                _logger.LogInformation("Intent queue processor is disabled, background service will not run");
                return;
            }

            _logger.LogInformation(
                "Intent queue processor starting (interval: {Interval}s, batch size: {BatchSize})",
                opts.ProcessingIntervalSeconds,
                opts.BatchSize);

            // Wait for startup to complete
            await Task.Delay(TimeSpan.FromSeconds(opts.StartupDelaySeconds), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var processed = await _processor.ProcessBatchAsync(opts.BatchSize, stoppingToken);

                    if (processed > 0)
                    {
                        _logger.LogInformation("Processed {Count} intents", processed);

                        // Get stats for monitoring
                        var stats = await _processor.GetStatsAsync();
                        _logger.LogDebug(
                            "Processor stats: Total={Total}, Success={Success}, Failed={Failed}, Pending={Pending}, InProgress={InProgress}",
                            stats.TotalProcessed,
                            stats.SuccessCount,
                            stats.FailureCount,
                            stats.PendingCount,
                            stats.InProgressCount);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in intent queue processor: {Message}", ex.Message);
                }

                // Wait before next iteration
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(opts.ProcessingIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
            }

            _logger.LogInformation("Intent queue processor stopping");
        }
    }

    /// <summary>
    ///     Configuration options for the intent queue processor.
    /// </summary>
    public sealed class IntentQueueProcessorOptions
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the processor is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets the processing interval in seconds.
        /// </summary>
        /// <remarks>
        ///     How often to check for pending intents. Default: 30 seconds.
        /// </remarks>
        public int ProcessingIntervalSeconds { get; set; } = 30;

        /// <summary>
        ///     Gets or sets the batch size (max intents per iteration).
        /// </summary>
        /// <remarks>
        ///     Maximum number of intents to process in a single batch. Default: 10.
        /// </remarks>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        ///     Gets or sets the startup delay in seconds.
        /// </summary>
        /// <remarks>
        ///     How long to wait after application startup before processing begins.
        ///     Gives other services time to initialize. Default: 10 seconds.
        /// </remarks>
        public int StartupDelaySeconds { get; set; } = 10;
    }
}
