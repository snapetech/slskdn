// <copyright file="IBackfillSchedulerService.cs" company="slskdn Team">
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

namespace slskd.Backfill
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for scheduling conservative header probing to discover FLAC hashes.
    /// </summary>
    public interface IBackfillSchedulerService
    {
        /// <summary>
        ///     Gets statistics about backfill operations.
        /// </summary>
        BackfillStats Stats { get; }

        /// <summary>
        ///     Gets backfill configuration.
        /// </summary>
        BackfillConfig Config { get; }

        /// <summary>
        ///     Gets a value indicating whether backfill is currently enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        ///     Gets a value indicating whether the system is currently idle (no active transfers).
        /// </summary>
        bool IsIdle { get; }

        /// <summary>
        ///     Enables or disables the backfill scheduler.
        /// </summary>
        void SetEnabled(bool enabled);

        /// <summary>
        ///     Gets the number of currently active backfill operations.
        /// </summary>
        int ActiveBackfillCount { get; }

        /// <summary>
        ///     Manually triggers a backfill cycle (for testing).
        /// </summary>
        Task<BackfillCycleResult> TriggerCycleAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets candidates for backfill.
        /// </summary>
        Task<IEnumerable<BackfillCandidate>> GetCandidatesAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Performs a single backfill operation for a specific file.
        /// </summary>
        Task<BackfillResult> BackfillFileAsync(string peerId, string path, long size, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Reports that the system is now idle (no active transfers).
        /// </summary>
        void ReportIdle();

        /// <summary>
        ///     Reports that the system is now busy (has active transfers).
        /// </summary>
        void ReportBusy();
    }

    /// <summary>
    ///     Backfill scheduler configuration.
    /// </summary>
    public class BackfillConfig
    {
        /// <summary>Maximum simultaneous backfill connections.</summary>
        public int MaxGlobalConnections { get; set; } = 2;

        /// <summary>Maximum probes per peer per day.</summary>
        public int MaxPerPeerPerDay { get; set; } = 10;

        /// <summary>Maximum bytes to read from header.</summary>
        public int MaxHeaderBytes { get; set; } = 65536; // 64KB

        /// <summary>Minimum idle time before backfill runs (seconds).</summary>
        public int MinIdleTimeSeconds { get; set; } = 300; // 5 minutes

        /// <summary>Interval between scheduler cycles (seconds).</summary>
        public int RunIntervalSeconds { get; set; } = 600; // 10 minutes

        /// <summary>Timeout for transfer acceptance (seconds).</summary>
        public int TransferTimeoutSeconds { get; set; } = 30;

        /// <summary>Whether backfill is enabled.</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    ///     Statistics about backfill operations.
    /// </summary>
    public class BackfillStats
    {
        /// <summary>Total backfill attempts.</summary>
        public int TotalAttempts { get; set; }

        /// <summary>Successful backfills.</summary>
        public int Successful { get; set; }

        /// <summary>Failed backfills.</summary>
        public int Failed { get; set; }

        /// <summary>Backfills skipped due to rate limits.</summary>
        public int RateLimited { get; set; }

        /// <summary>Currently active backfills.</summary>
        public int Active { get; set; }

        /// <summary>Total hashes discovered via backfill.</summary>
        public int HashesDiscovered { get; set; }

        /// <summary>Last cycle time.</summary>
        public DateTime? LastCycleTime { get; set; }

        /// <summary>Next scheduled cycle time.</summary>
        public DateTime? NextCycleTime { get; set; }

        /// <summary>Whether currently idle.</summary>
        public bool IsIdle { get; set; }

        /// <summary>Time system has been idle.</summary>
        public TimeSpan? IdleDuration { get; set; }
    }

    /// <summary>
    ///     A candidate file for backfill.
    /// </summary>
    public class BackfillCandidate
    {
        /// <summary>Gets or sets the file ID.</summary>
        public string FileId { get; set; }

        /// <summary>Gets or sets the peer ID (username).</summary>
        public string PeerId { get; set; }

        /// <summary>Gets or sets the file path.</summary>
        public string Path { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets when the file was discovered.</summary>
        public DateTime DiscoveredAt { get; set; }

        /// <summary>Gets or sets the peer's backfill count today.</summary>
        public int PeerBackfillsToday { get; set; }

        /// <summary>Gets or sets whether the peer is online.</summary>
        public bool IsPeerOnline { get; set; }

        /// <summary>Gets or sets whether the peer is a slskdn client.</summary>
        public bool IsPeerSlskdn { get; set; }
    }

    /// <summary>
    ///     Result of a single backfill operation.
    /// </summary>
    public class BackfillResult
    {
        /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the peer ID.</summary>
        public string PeerId { get; set; }

        /// <summary>Gets or sets the file path.</summary>
        public string Path { get; set; }

        /// <summary>Gets or sets the discovered hash (if successful).</summary>
        public string Hash { get; set; }

        /// <summary>Gets or sets the FLAC key (if successful).</summary>
        public string FlacKey { get; set; }

        /// <summary>Gets or sets the error message (if failed).</summary>
        public string Error { get; set; }

        /// <summary>Gets or sets the duration in milliseconds.</summary>
        public long DurationMs { get; set; }

        /// <summary>Gets or sets the bytes read.</summary>
        public int BytesRead { get; set; }
    }

    /// <summary>
    ///     Result of a backfill cycle.
    /// </summary>
    public class BackfillCycleResult
    {
        /// <summary>Gets or sets the number of candidates evaluated.</summary>
        public int CandidatesEvaluated { get; set; }

        /// <summary>Gets or sets the number of backfills attempted.</summary>
        public int BackfillsAttempted { get; set; }

        /// <summary>Gets or sets the number of successful backfills.</summary>
        public int Successful { get; set; }

        /// <summary>Gets or sets the number of failed backfills.</summary>
        public int Failed { get; set; }

        /// <summary>Gets or sets the number skipped due to rate limits.</summary>
        public int RateLimited { get; set; }

        /// <summary>Gets or sets the cycle duration in milliseconds.</summary>
        public long DurationMs { get; set; }

        /// <summary>Gets or sets individual results.</summary>
        public List<BackfillResult> Results { get; set; } = new();
    }
}


