// <copyright file="SwarmAnalyticsModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Analytics;

using System;
using System.Collections.Generic;

/// <summary>
///     Overall swarm performance metrics.
/// </summary>
public class SwarmPerformanceMetrics
{
    /// <summary>
    ///     Total downloads completed.
    /// </summary>
    public long TotalDownloads { get; set; }

    /// <summary>
    ///     Successful downloads.
    /// </summary>
    public long SuccessfulDownloads { get; set; }

    /// <summary>
    ///     Failed downloads.
    /// </summary>
    public long FailedDownloads { get; set; }

    /// <summary>
    ///     Success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    ///     Average download duration in seconds.
    /// </summary>
    public double AverageDurationSeconds { get; set; }

    /// <summary>
    ///     Average download speed in bytes per second.
    /// </summary>
    public double AverageSpeedBytesPerSecond { get; set; }

    /// <summary>
    ///     Average number of sources used per download.
    /// </summary>
    public double AverageSourcesUsed { get; set; }

    /// <summary>
    ///     Total bytes downloaded.
    /// </summary>
    public long TotalBytesDownloaded { get; set; }

    /// <summary>
    ///     Total chunks completed.
    /// </summary>
    public long TotalChunksCompleted { get; set; }

    /// <summary>
    ///     Chunk success rate (0.0 to 1.0).
    /// </summary>
    public double ChunkSuccessRate { get; set; }

    /// <summary>
    ///     Time window for these metrics.
    /// </summary>
    public TimeSpan TimeWindow { get; set; }
}

/// <summary>
///     Peer performance ranking.
/// </summary>
public class PeerPerformanceRanking
{
    /// <summary>
    ///     Peer identifier (username or peer ID).
    /// </summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>
    ///     Peer source (soulseek, overlay).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     Reputation score (0.0 to 1.0).
    /// </summary>
    public double ReputationScore { get; set; }

    /// <summary>
    ///     Average RTT in milliseconds.
    /// </summary>
    public double AverageRttMs { get; set; }

    /// <summary>
    ///     Average throughput in bytes per second.
    /// </summary>
    public double AverageThroughputBytesPerSecond { get; set; }

    /// <summary>
    ///     Total chunks completed.
    /// </summary>
    public long ChunksCompleted { get; set; }

    /// <summary>
    ///     Total chunks failed.
    /// </summary>
    public long ChunksFailed { get; set; }

    /// <summary>
    ///     Chunk success rate (0.0 to 1.0).
    /// </summary>
    public double ChunkSuccessRate { get; set; }

    /// <summary>
    ///     Total bytes transferred.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    ///     Ranking position (1-based).
    /// </summary>
    public int Rank { get; set; }
}

/// <summary>
///     Swarm efficiency metrics.
/// </summary>
public class SwarmEfficiencyMetrics
{
    /// <summary>
    ///     Average chunk utilization (chunks used / chunks available).
    /// </summary>
    public double ChunkUtilization { get; set; }

    /// <summary>
    ///     Average peer utilization (active peers / total peers).
    /// </summary>
    public double PeerUtilization { get; set; }

    /// <summary>
    ///     Average redundancy factor (chunks per unique content).
    /// </summary>
    public double RedundancyFactor { get; set; }

    /// <summary>
    ///     Average time to first byte in milliseconds.
    /// </summary>
    public double AverageTimeToFirstByteMs { get; set; }

    /// <summary>
    ///     Average chunk reassignment rate (reassignments per download).
    /// </summary>
    public double AverageReassignmentRate { get; set; }

    /// <summary>
    ///     Average rescue mode invocation rate (rescues per download).
    /// </summary>
    public double AverageRescueRate { get; set; }
}

/// <summary>
///     Historical trends for swarm metrics.
/// </summary>
public class SwarmTrends
{
    /// <summary>
    ///     Time points for the trend data.
    /// </summary>
    public List<DateTime> TimePoints { get; set; } = new();

    /// <summary>
    ///     Download success rates over time.
    /// </summary>
    public List<double> SuccessRates { get; set; } = new();

    /// <summary>
    ///     Average speeds over time (bytes per second).
    /// </summary>
    public List<double> AverageSpeeds { get; set; } = new();

    /// <summary>
    ///     Average durations over time (seconds).
    /// </summary>
    public List<double> AverageDurations { get; set; } = new();

    /// <summary>
    ///     Average sources used over time.
    /// </summary>
    public List<double> AverageSourcesUsed { get; set; } = new();

    /// <summary>
    ///     Total downloads per time point.
    /// </summary>
    public List<long> DownloadCounts { get; set; } = new();
}

/// <summary>
///     Recommendation for optimizing swarm performance.
/// </summary>
public class SwarmRecommendation
{
    /// <summary>
    ///     Recommendation type.
    /// </summary>
    public RecommendationType Type { get; set; }

    /// <summary>
    ///     Priority level.
    /// </summary>
    public RecommendationPriority Priority { get; set; }

    /// <summary>
    ///     Recommendation title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Detailed description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Suggested action.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    ///     Estimated impact (0.0 to 1.0).
    /// </summary>
    public double EstimatedImpact { get; set; }
}

/// <summary>
///     Recommendation type.
/// </summary>
public enum RecommendationType
{
    /// <summary>
    ///     Peer selection optimization.
    /// </summary>
    PeerSelection,

    /// <summary>
    ///     Chunk size optimization.
    /// </summary>
    ChunkSize,

    /// <summary>
    ///     Source count optimization.
    /// </summary>
    SourceCount,

    /// <summary>
    ///     Network configuration.
    /// </summary>
    NetworkConfig,

    /// <summary>
    ///     Performance tuning.
    /// </summary>
    PerformanceTuning
}

/// <summary>
///     Recommendation priority.
/// </summary>
public enum RecommendationPriority
{
    /// <summary>
    ///     Low priority.
    /// </summary>
    Low,

    /// <summary>
    ///     Medium priority.
    /// </summary>
    Medium,

    /// <summary>
    ///     High priority.
    /// </summary>
    High,

    /// <summary>
    ///     Critical priority.
    /// </summary>
    Critical
}
