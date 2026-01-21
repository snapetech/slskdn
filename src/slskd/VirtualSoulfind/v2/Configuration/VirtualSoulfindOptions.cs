// <copyright file="VirtualSoulfindOptions.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Configuration
{
    using slskd.VirtualSoulfind.v2.Planning;

    /// <summary>
    ///     Configuration options for VirtualSoulfind v2.
    /// </summary>
    public sealed class VirtualSoulfindOptions
    {
        /// <summary>
        ///     Gets or initializes the default planning mode.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         - <see cref="PlanningMode.SoulseekFriendly"/>: Balanced mode (default).
        ///         - <see cref="PlanningMode.OfflinePlanning"/>: Only use local/cached sources.
        ///         - <see cref="PlanningMode.MeshOnly"/>: Only use mesh network sources.
        ///     </para>
        /// </remarks>
        public PlanningMode DefaultMode { get; init; } = PlanningMode.SoulseekFriendly;

        /// <summary>
        ///     Gets or initializes whether VirtualSoulfind v2 is enabled.
        /// </summary>
        /// <remarks>
        ///     If false, all v2 endpoints return 503 Service Unavailable.
        /// </remarks>
        public bool Enabled { get; init; } = true;

        /// <summary>
        ///     Gets or initializes the maximum number of concurrent plan executions.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This limits how many acquisition plans can be executing simultaneously.
        ///         Higher values allow more parallelism but consume more resources.
        ///     </para>
        ///     <para>
        ///         Default: 3
        ///     </para>
        /// </remarks>
        public int MaxConcurrentExecutions { get; init; } = 3;

        /// <summary>
        ///     Gets or initializes the maximum number of intents to process per batch.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The background processor will pick up to this many pending intents
        ///         in each processing cycle.
        ///     </para>
        ///     <para>
        ///         Default: 10
        ///     </para>
        /// </remarks>
        public int ProcessorBatchSize { get; init; } = 10;

        /// <summary>
        ///     Gets or initializes the interval (in milliseconds) between processor runs.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         How often the background processor checks for new intents to process.
        ///     </para>
        ///     <para>
        ///         Default: 5000ms (5 seconds)
        ///     </para>
        /// </remarks>
        public int ProcessorIntervalMs { get; init; } = 5000;

        /// <summary>
        ///     Gets or initializes backend-specific options.
        /// </summary>
        public BackendLimits Backends { get; init; } = new BackendLimits();

        /// <summary>
        ///     Gets or initializes work budget limits for plan execution.
        /// </summary>
        public WorkBudgetLimits WorkBudget { get; init; } = new WorkBudgetLimits();
    }

    /// <summary>
    ///     Per-backend configuration limits.
    /// </summary>
    public sealed class BackendLimits
    {
        /// <summary>
        ///     Gets or initializes Soulseek backend options.
        /// </summary>
        public SoulseekBackendLimits Soulseek { get; init; } = new SoulseekBackendLimits();

        /// <summary>
        ///     Gets or initializes Mesh DHT backend options.
        /// </summary>
        public MeshBackendLimits Mesh { get; init; } = new MeshBackendLimits();

        /// <summary>
        ///     Gets or initializes Torrent backend options.
        /// </summary>
        public TorrentBackendLimits Torrent { get; init; } = new TorrentBackendLimits();

        /// <summary>
        ///     Gets or initializes HTTP backend options.
        /// </summary>
        public HttpBackendLimits Http { get; init; } = new HttpBackendLimits();
    }

    /// <summary>
    ///     Soulseek backend limits (H-08 compliance).
    /// </summary>
    public sealed class SoulseekBackendLimits
    {
        /// <summary>
        ///     Gets or initializes the maximum number of searches per minute.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         H-08 requirement: Prevent excessive search rate that could lead to bans.
        ///     </para>
        ///     <para>
        ///         Default: 10 searches/minute
        ///     </para>
        /// </remarks>
        public int MaxSearchesPerMinute { get; init; } = 10;

        /// <summary>
        ///     Gets or initializes the maximum number of parallel searches.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 2 parallel searches
        ///     </para>
        /// </remarks>
        public int MaxParallelSearches { get; init; } = 2;

        /// <summary>
        ///     Gets or initializes the maximum number of browse requests per minute.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         H-08 requirement: Limit user browsing to prevent abuse.
        ///     </para>
        ///     <para>
        ///         Default: 5 browses/minute
        ///     </para>
        /// </remarks>
        public int MaxBrowsesPerMinute { get; init; } = 5;

        /// <summary>
        ///     Gets or initializes the minimum upload speed (bytes/sec) for trust scoring.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Peers with upload speeds below this threshold get lower trust scores.
        ///     </para>
        ///     <para>
        ///         Default: 50 KB/s
        ///     </para>
        /// </remarks>
        public int MinUploadSpeedBytesPerSec { get; init; } = 50 * 1024;

        /// <summary>
        ///     Gets or initializes the search timeout in milliseconds.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 10000ms (10 seconds)
        ///     </para>
        /// </remarks>
        public int SearchTimeoutMs { get; init; } = 10_000;
    }

    /// <summary>
    ///     Mesh DHT backend limits.
    /// </summary>
    public sealed class MeshBackendLimits
    {
        /// <summary>
        ///     Gets or initializes the maximum number of mesh queries per minute.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 30 queries/minute
        ///     </para>
        /// </remarks>
        public int MaxQueriesPerMinute { get; init; } = 30;

        /// <summary>
        ///     Gets or initializes the query timeout in milliseconds.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 5000ms (5 seconds)
        ///     </para>
        /// </remarks>
        public int QueryTimeoutMs { get; init; } = 5000;

        /// <summary>
        ///     Gets or initializes the minimum trust score for mesh sources.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Sources with trust scores below this threshold are excluded.
        ///     </para>
        ///     <para>
        ///         Default: 0.5 (50%)
        ///     </para>
        /// </remarks>
        public float MinTrustScore { get; init; } = 0.5f;
    }

    /// <summary>
    ///     Torrent backend limits.
    /// </summary>
    public sealed class TorrentBackendLimits
    {
        /// <summary>
        ///     Gets or initializes the maximum number of DHT queries per minute.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 20 queries/minute
        ///     </para>
        /// </remarks>
        public int MaxDhtQueriesPerMinute { get; init; } = 20;

        /// <summary>
        ///     Gets or initializes the minimum number of seeders for trust scoring.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Torrents with fewer seeders get lower trust scores.
        ///     </para>
        ///     <para>
        ///         Default: 3 seeders
        ///     </para>
        /// </remarks>
        public int MinSeeders { get; init; } = 3;
    }

    /// <summary>
    ///     HTTP backend limits.
    /// </summary>
    public sealed class HttpBackendLimits
    {
        /// <summary>
        ///     Gets or initializes the maximum number of HTTP requests per minute.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 60 requests/minute
        ///     </para>
        /// </remarks>
        public int MaxRequestsPerMinute { get; init; } = 60;

        /// <summary>
        ///     Gets or initializes the request timeout in milliseconds.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Default: 30000ms (30 seconds)
        ///     </para>
        /// </remarks>
        public int RequestTimeoutMs { get; init; } = 30_000;

        /// <summary>
        ///     Gets or initializes whether to only allow HTTPS URLs.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If true, HTTP URLs are rejected and only HTTPS is allowed.
        ///     </para>
        ///     <para>
        ///         Default: false (allow both HTTP and HTTPS)
        ///     </para>
        /// </remarks>
        public bool RequireHttps { get; init; } = false;
    }

    /// <summary>
    ///     Work budget limits for plan execution (H-02 integration).
    /// </summary>
    public sealed class WorkBudgetLimits
    {
        /// <summary>
        ///     Gets or initializes the default work budget per plan execution.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         How many work units a single plan execution is allowed to consume.
        ///     </para>
        ///     <para>
        ///         Default: 1000 units
        ///     </para>
        /// </remarks>
        public int DefaultBudgetPerExecution { get; init; } = 1000;

        /// <summary>
        ///     Gets or initializes the maximum work budget per execution.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Even high-priority plans cannot exceed this limit.
        ///     </para>
        ///     <para>
        ///         Default: 5000 units
        ///     </para>
        /// </remarks>
        public int MaxBudgetPerExecution { get; init; } = 5000;
    }
}
