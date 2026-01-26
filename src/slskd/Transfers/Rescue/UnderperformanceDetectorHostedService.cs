// <copyright file="UnderperformanceDetectorHostedService.cs" company="slskdn Team">
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

namespace slskd.Transfers.Rescue
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Serilog;
    using slskd.Transfers.Downloads;
    using Soulseek;

    /// <summary>
    ///     Hosted service that periodically checks active downloads for underperformance
    ///     (queued too long, throughput too low, stalled) and triggers rescue mode.
    /// </summary>
    public class UnderperformanceDetectorHostedService : IHostedService
    {
        private readonly IDownloadService downloadService;
        private readonly IRescueService rescueService;
        private readonly IOptionsMonitor<slskd.Options> options;
        private readonly ILogger log = Log.ForContext<UnderperformanceDetectorHostedService>();

        // For Stalled rule: transferId -> (lastBytesTransferred, first time we saw no progress)
        private readonly ConcurrentDictionary<Guid, (long LastBytes, DateTime? FirstNoProgressAt)> stalledState = new();

        public UnderperformanceDetectorHostedService(
            IDownloadService downloadService,
            IRescueService rescueService,
            IOptionsMonitor<slskd.Options> options)
        {
            this.downloadService = downloadService;
            this.rescueService = rescueService;
            this.options = options;
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            log.Information("[RESCUE] Underperformance detector started");
            _ = RunLoopAsync(cancellationToken);
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var rescue = options.CurrentValue?.RescueMode;
                    if (rescue?.Enabled != true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(rescue?.CheckIntervalSeconds ?? 45), ct).ConfigureAwait(false);
                        continue;
                    }

                    var active = downloadService.List(t => !t.State.HasFlag(TransferStates.Completed), includeRemoved: false);
                    var activeIds = active.Select(t => t.Id).ToHashSet();

                    // Prune stalled state for transfers that are no longer active
                    foreach (var id in stalledState.Keys.ToArray())
                    {
                        if (!activeIds.Contains(id))
                            stalledState.TryRemove(id, out _);
                    }

                    foreach (var t in active)
                    {
                        if (ct.IsCancellationRequested) break;
                        var idStr = t.Id.ToString();
                        if (rescueService.IsRescueActive(idStr)) continue;

                        // 1) QueuedTooLong
                        if (t.State.HasFlag(TransferStates.Queued))
                        {
                            var since = (t.EnqueuedAt ?? t.RequestedAt);
                            var elapsed = (DateTime.UtcNow - since).TotalSeconds;
                            if (elapsed >= rescue.MaxQueueTimeSeconds)
                            {
                                await TriggerRescueAsync(t, UnderperformanceReason.QueuedTooLong, ct).ConfigureAwait(false);
                                continue;
                            }
                        }

                        // 2) ThroughputTooLow and 3) Stalled — only for InProgress
                        if (t.State != TransferStates.InProgress) continue;

                        var minBytesPerSec = (rescue.MinThroughputKBps) * 1024L;
                        var duration = t.StartedAt.HasValue ? (DateTime.UtcNow - t.StartedAt.Value).TotalSeconds : 0;

                        // 2) ThroughputTooLow: require MinDurationSeconds before judging
                        if (duration >= rescue.MinDurationSeconds && t.AverageSpeed < minBytesPerSec)
                        {
                            await TriggerRescueAsync(t, UnderperformanceReason.ThroughputTooLow, ct).ConfigureAwait(false);
                            continue;
                        }

                        // 3) Stalled: no increase in BytesTransferred for StalledTimeoutSeconds
                        var last = stalledState.AddOrUpdate(
                            t.Id,
                            (t.BytesTransferred, t.BytesTransferred > 0 ? null : DateTime.UtcNow),
                            (_, prev) =>
                            {
                                if (t.BytesTransferred > prev.LastBytes)
                                    return (t.BytesTransferred, (DateTime?)null);
                                var first = prev.FirstNoProgressAt ?? DateTime.UtcNow;
                                return (prev.LastBytes, first);
                            });

                        if (last.FirstNoProgressAt.HasValue)
                        {
                            var stalledElapsed = (DateTime.UtcNow - last.FirstNoProgressAt.Value).TotalSeconds;
                            if (stalledElapsed >= rescue.StalledTimeoutSeconds)
                            {
                                await TriggerRescueAsync(t, UnderperformanceReason.Stalled, ct).ConfigureAwait(false);
                                stalledState.TryRemove(t.Id, out _);
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(rescue.CheckIntervalSeconds), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[RESCUE] Underperformance detector loop error: {Message}", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
                }
            }
        }

        private async Task TriggerRescueAsync(slskd.Transfers.Transfer t, UnderperformanceReason reason, CancellationToken ct)
        {
            log.Information("[RESCUE] Triggering rescue for {File} from {User} (reason: {Reason})", t.Filename, t.Username, reason);
            try
            {
                await rescueService.ActivateRescueModeAsync(
                    t.Id.ToString(),
                    t.Username,
                    t.Filename,
                    t.Size,
                    t.BytesTransferred,
                    reason,
                    ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[RESCUE] ActivateRescueModeAsync failed for {TransferId}: {Message}", t.Id, ex.Message);
            }
        }
    }
}
