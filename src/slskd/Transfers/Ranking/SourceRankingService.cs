// <copyright file="SourceRankingService.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//     Licensed under the GNU Affero General Public License v3.0.
// </copyright>

namespace slskd.Transfers.Ranking
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using slskd.Events;

    /// <summary>
    ///     Service for ranking download sources using smart scoring.
    /// </summary>
    public class SourceRankingService : ISourceRankingService
    {
        // Scoring weights (same as frontend for consistency)
        private const double MaxSpeedScore = 40.0;
        private const double MaxQueueScore = 30.0;
        private const double FreeSlotBonus = 15.0;
        private const double MaxHistoryScore = 15.0;
        private const double MaxSizeMatchScore = 20.0;

        // Thresholds
        private const int MaxSpeedForScoring = 10_000_000; // 10 MB/s = max speed score
        private const int MaxQueueForScoring = 100; // Queue >= 100 = 0 score

        private readonly IDbContextFactory<SourceRankingDbContext> contextFactory;
        private readonly ILogger<SourceRankingService> logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SourceRankingService"/> class.
        /// </summary>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="eventBus">The event bus for subscribing to download events.</param>
        public SourceRankingService(
            IDbContextFactory<SourceRankingDbContext> contextFactory,
            ILogger<SourceRankingService> logger,
            EventBus eventBus)
        {
            this.contextFactory = contextFactory;
            this.logger = logger;

            // Subscribe to download events to track history
            eventBus.Subscribe<DownloadFileCompleteEvent>("SourceRankingService.Success", OnDownloadComplete);
            eventBus.Subscribe<DownloadFileFailedEvent>("SourceRankingService.Failure", OnDownloadFailed);
        }

        private async Task OnDownloadComplete(DownloadFileCompleteEvent evt)
        {
            try
            {
                await RecordSuccessAsync(evt.Transfer.Username);
                logger.LogDebug("Recorded successful download from {Username}", evt.Transfer.Username);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record download success for {Username}", evt.Transfer.Username);
            }
        }

        private async Task OnDownloadFailed(DownloadFileFailedEvent evt)
        {
            try
            {
                await RecordFailureAsync(evt.Transfer.Username);
                logger.LogDebug("Recorded failed download from {Username}: {Error}", evt.Transfer.Username, evt.ErrorMessage);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record download failure for {Username}", evt.Transfer.Username);
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<RankedSource>> RankSourcesAsync(
            IEnumerable<SourceCandidate> candidates,
            CancellationToken cancellationToken = default)
        {
            var candidateList = candidates.ToList();
            if (candidateList.Count == 0)
            {
                return Enumerable.Empty<RankedSource>();
            }

            // Get usernames for history lookup
            var usernames = candidateList.Select(c => c.Username).Distinct().ToList();
            var histories = await GetHistoriesAsync(usernames, cancellationToken);

            // Calculate scores for each candidate
            var ranked = candidateList.Select(candidate =>
            {
                histories.TryGetValue(candidate.Username, out var history);
                return CalculateScore(candidate, history);
            });

            // Sort by smart score descending
            return ranked.OrderByDescending(r => r.SmartScore).ToList();
        }

        /// <inheritdoc/>
        public async Task RecordSuccessAsync(string username, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var entry = await context.DownloadHistory.FindAsync(new object[] { username }, cancellationToken);
            if (entry == null)
            {
                entry = new DownloadHistoryEntry { Username = username };
                context.DownloadHistory.Add(entry);
            }

            entry.Successes++;
            entry.LastUpdated = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Recorded success for {Username}: {Successes}/{Failures}", username, entry.Successes, entry.Failures);
        }

        /// <inheritdoc/>
        public async Task RecordFailureAsync(string username, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var entry = await context.DownloadHistory.FindAsync(new object[] { username }, cancellationToken);
            if (entry == null)
            {
                entry = new DownloadHistoryEntry { Username = username };
                context.DownloadHistory.Add(entry);
            }

            entry.Failures++;
            entry.LastUpdated = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Recorded failure for {Username}: {Successes}/{Failures}", username, entry.Successes, entry.Failures);
        }

        /// <inheritdoc/>
        public async Task<UserDownloadHistory> GetHistoryAsync(string username, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var entry = await context.DownloadHistory.FindAsync(new object[] { username }, cancellationToken);
            if (entry == null)
            {
                return new UserDownloadHistory { Username = username, Successes = 0, Failures = 0 };
            }

            return new UserDownloadHistory
            {
                Username = entry.Username,
                Successes = entry.Successes,
                Failures = entry.Failures,
            };
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, UserDownloadHistory>> GetHistoriesAsync(
            IEnumerable<string> usernames,
            CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var usernameList = usernames.ToList();
            var entries = await context.DownloadHistory
                .Where(e => usernameList.Contains(e.Username))
                .ToDictionaryAsync(e => e.Username, cancellationToken);

            var result = new Dictionary<string, UserDownloadHistory>();
            foreach (var username in usernameList)
            {
                if (entries.TryGetValue(username, out var entry))
                {
                    result[username] = new UserDownloadHistory
                    {
                        Username = entry.Username,
                        Successes = entry.Successes,
                        Failures = entry.Failures,
                    };
                }
                else
                {
                    result[username] = new UserDownloadHistory { Username = username, Successes = 0, Failures = 0 };
                }
            }

            return result;
        }

        private RankedSource CalculateScore(SourceCandidate candidate, UserDownloadHistory history)
        {
            // Speed score: 0-40 points based on upload speed
            // Scale: 0 B/s = 0, 10 MB/s+ = 40
            var speedScore = Math.Min(MaxSpeedScore, (double)candidate.UploadSpeed / MaxSpeedForScoring * MaxSpeedScore);

            // Queue score: 0-30 points, lower queue = higher score
            // Scale: 0 queue = 30, 100+ queue = 0
            var queueScore = Math.Max(0, MaxQueueScore * (1 - ((double)candidate.QueueLength / MaxQueueForScoring)));

            // Free slot bonus: 15 points if has free slot
            var freeSlotScore = candidate.HasFreeUploadSlot ? FreeSlotBonus : 0;

            // History score: -15 to +15 based on past success rate
            double historyScore = 0;
            if (history != null && history.Successes + history.Failures > 0)
            {
                // Center at 0.5 success rate = 0 points
                // 1.0 success rate = +15, 0.0 success rate = -15
                historyScore = (history.SuccessRate - 0.5) * 2 * MaxHistoryScore;
            }

            // Size match score: 0-20 points for auto-replace scenarios
            // Perfect match (0% diff) = 20, 10%+ diff = 0
            double sizeMatchScore = 0;
            if (candidate.SizeDiffPercent.HasValue)
            {
                sizeMatchScore = Math.Max(0, MaxSizeMatchScore * (1 - (candidate.SizeDiffPercent.Value / 10.0)));
            }

            var totalScore = speedScore + queueScore + freeSlotScore + historyScore + sizeMatchScore;

            return new RankedSource
            {
                Username = candidate.Username,
                Filename = candidate.Filename,
                Size = candidate.Size,
                HasFreeUploadSlot = candidate.HasFreeUploadSlot,
                QueueLength = candidate.QueueLength,
                UploadSpeed = candidate.UploadSpeed,
                SizeDiffPercent = candidate.SizeDiffPercent,
                SmartScore = totalScore,
                SpeedScore = speedScore,
                QueueScore = queueScore,
                FreeSlotScore = freeSlotScore,
                HistoryScore = historyScore,
                SizeMatchScore = sizeMatchScore,
            };
        }
    }
}

