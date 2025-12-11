// <copyright file="PeerPerformanceMetrics.cs" company="slskdn Team">
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

namespace slskd.Transfers.MultiSource.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Performance metrics for a single peer in the swarm.
    /// </summary>
    public class PeerPerformanceMetrics
    {
        /// <summary>
        ///     Gets or sets the peer identifier (mesh peer ID or Soulseek username).
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        ///     Gets or sets the peer source (Soulseek or Overlay).
        /// </summary>
        public PeerSource Source { get; set; }

        // Connection metrics
        /// <summary>
        ///     Gets or sets the RTT exponential moving average in milliseconds.
        /// </summary>
        public double RttAvgMs { get; set; }

        /// <summary>
        ///     Gets or sets the RTT standard deviation in milliseconds.
        /// </summary>
        public double RttStdDevMs { get; set; }

        /// <summary>
        ///     Gets or sets the timestamp of the last RTT sample.
        /// </summary>
        public DateTimeOffset? LastRttSample { get; set; }

        // Throughput metrics
        /// <summary>
        ///     Gets or sets the throughput exponential moving average in bytes per second.
        /// </summary>
        public double ThroughputAvgBytesPerSec { get; set; }

        /// <summary>
        ///     Gets or sets the throughput standard deviation in bytes per second.
        /// </summary>
        public double ThroughputStdDevBytesPerSec { get; set; }

        /// <summary>
        ///     Gets or sets the total bytes transferred from this peer.
        /// </summary>
        public long TotalBytesTransferred { get; set; }

        /// <summary>
        ///     Gets or sets the timestamp of the last throughput sample.
        /// </summary>
        public DateTimeOffset? LastThroughputSample { get; set; }

        // Reliability metrics
        /// <summary>
        ///     Gets or sets the total number of chunks requested from this peer.
        /// </summary>
        public int ChunksRequested { get; set; }

        /// <summary>
        ///     Gets or sets the number of chunks successfully completed.
        /// </summary>
        public int ChunksCompleted { get; set; }

        /// <summary>
        ///     Gets or sets the number of chunks that failed.
        /// </summary>
        public int ChunksFailed { get; set; }

        /// <summary>
        ///     Gets or sets the number of chunks that timed out.
        /// </summary>
        public int ChunksTimedOut { get; set; }

        /// <summary>
        ///     Gets or sets the number of chunks that failed hash verification.
        /// </summary>
        public int ChunksCorrupted { get; set; }

        // Computed rates
        /// <summary>
        ///     Gets the error rate (failed chunks / total requested).
        /// </summary>
        public double ErrorRate => ChunksRequested > 0
            ? (double)ChunksFailed / ChunksRequested
            : 0.0;

        /// <summary>
        ///     Gets the timeout rate (timed out chunks / total requested).
        /// </summary>
        public double TimeoutRate => ChunksRequested > 0
            ? (double)ChunksTimedOut / ChunksRequested
            : 0.0;

        /// <summary>
        ///     Gets the success rate (completed chunks / total requested).
        /// </summary>
        public double SuccessRate => ChunksRequested > 0
            ? (double)ChunksCompleted / ChunksRequested
            : 0.0;

        // Sliding window state (for recent samples)
        /// <summary>
        ///     Gets or sets the recent RTT samples (sliding window).
        /// </summary>
        public Queue<RttSample> RecentRttSamples { get; set; } = new();

        /// <summary>
        ///     Gets or sets the recent throughput samples (sliding window).
        /// </summary>
        public Queue<ThroughputSample> RecentThroughputSamples { get; set; } = new();

        // Metadata
        /// <summary>
        ///     Gets or sets when this peer was first seen.
        /// </summary>
        public DateTimeOffset FirstSeen { get; set; }

        /// <summary>
        ///     Gets or sets when this peer's metrics were last updated.
        /// </summary>
        public DateTimeOffset LastUpdated { get; set; }

        /// <summary>
        ///     Gets or sets the total number of samples recorded.
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        ///     Gets or sets the local reputation score (0..1, decaying toward 0.5).
        /// </summary>
        public double ReputationScore { get; set; } = 0.5;

        /// <summary>
        ///     Gets or sets when the reputation score was last updated (UTC).
        /// </summary>
        public DateTimeOffset? ReputationUpdatedAt { get; set; }
    }

    /// <summary>
    ///     Peer source type.
    /// </summary>
    public enum PeerSource
    {
        /// <summary>
        ///     Soulseek network peer.
        /// </summary>
        Soulseek,

        /// <summary>
        ///     DHT overlay network peer.
        /// </summary>
        Overlay,
    }

    /// <summary>
    ///     RTT sample.
    /// </summary>
    public class RttSample
    {
        /// <summary>
        ///     Gets or sets the sample timestamp.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        ///     Gets or sets the RTT in milliseconds.
        /// </summary>
        public double RttMs { get; set; }
    }

    /// <summary>
    ///     Throughput sample.
    /// </summary>
    public class ThroughputSample
    {
        /// <summary>
        ///     Gets or sets the sample timestamp.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        ///     Gets or sets the throughput in bytes per second.
        /// </summary>
        public double BytesPerSec { get; set; }

        /// <summary>
        ///     Gets or sets the bytes transferred in this sample.
        /// </summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        ///     Gets or sets the sample duration.
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    ///     Chunk completion result.
    /// </summary>
    public enum ChunkCompletionResult
    {
        /// <summary>
        ///     Chunk completed successfully.
        /// </summary>
        Success,

        /// <summary>
        ///     Chunk failed with an error.
        /// </summary>
        Failed,

        /// <summary>
        ///     Chunk request timed out.
        /// </summary>
        TimedOut,

        /// <summary>
        ///     Chunk failed hash verification.
        /// </summary>
        Corrupted,
    }
}
