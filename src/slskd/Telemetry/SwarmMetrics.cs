// <copyright file="SwarmMetrics.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Telemetry;

using Prometheus;

/// <summary>
/// Prometheus metrics for swarm download operations.
/// </summary>
public static class SwarmMetrics
{
    /// <summary>
    /// Total number of swarm downloads started.
    /// </summary>
    public static readonly Counter SwarmDownloadsTotal = Prometheus.Metrics.CreateCounter(
        "slskd_swarm_downloads_total",
        "Total number of swarm downloads started",
        new CounterConfiguration
        {
            LabelNames = new[] { "status" } // "success", "failed", "cancelled"
        });

    /// <summary>
    /// Current number of active swarm downloads.
    /// </summary>
    public static readonly Gauge SwarmDownloadsActive = Prometheus.Metrics.CreateGauge(
        "slskd_swarm_downloads_active",
        "Current number of active swarm downloads");

    /// <summary>
    /// Swarm download duration in seconds.
    /// </summary>
    public static readonly Histogram SwarmDownloadDurationSeconds = Prometheus.Metrics.CreateHistogram(
        "slskd_swarm_download_duration_seconds",
        "Swarm download duration in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(1.0, 2.0, 10) // 1s, 2s, 4s, 8s, 16s, 32s, 64s, 128s, 256s, 512s+
        });

    /// <summary>
    /// Swarm download speed in bytes per second.
    /// </summary>
    public static readonly Histogram SwarmDownloadSpeedBytesPerSecond = Prometheus.Metrics.CreateHistogram(
        "slskd_swarm_download_speed_bytes_per_second",
        "Swarm download speed in bytes per second",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(1024.0, 2.0, 15) // 1KB, 2KB, 4KB, ... up to 32MB/s
        });

    /// <summary>
    /// Number of sources used per swarm download.
    /// </summary>
    public static readonly Histogram SwarmDownloadSourcesUsed = Prometheus.Metrics.CreateHistogram(
        "slskd_swarm_download_sources_used",
        "Number of sources used per swarm download",
        new HistogramConfiguration
        {
            Buckets = new[] { 1.0, 2.0, 3.0, 5.0, 10.0, 20.0, 50.0, 100.0 }
        });

    /// <summary>
    /// Total chunks completed across all swarm downloads.
    /// </summary>
    public static readonly Counter SwarmChunksCompletedTotal = Prometheus.Metrics.CreateCounter(
        "slskd_swarm_chunks_completed_total",
        "Total chunks completed across all swarm downloads",
        new CounterConfiguration
        {
            LabelNames = new[] { "status" } // "success", "failed", "timeout", "corrupted"
        });

    /// <summary>
    /// Current number of active chunks being downloaded.
    /// </summary>
    public static readonly Gauge SwarmChunksActive = Prometheus.Metrics.CreateGauge(
        "slskd_swarm_chunks_active",
        "Current number of active chunks being downloaded");

    /// <summary>
    /// Chunk download duration in milliseconds.
    /// </summary>
    public static readonly Histogram SwarmChunkDurationMilliseconds = Prometheus.Metrics.CreateHistogram(
        "slskd_swarm_chunk_duration_milliseconds",
        "Chunk download duration in milliseconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(100.0, 2.0, 12) // 100ms, 200ms, 400ms, ... up to ~100s
        });

    /// <summary>
    /// Chunk download speed in bytes per second.
    /// </summary>
    public static readonly Histogram SwarmChunkSpeedBytesPerSecond = Prometheus.Metrics.CreateHistogram(
        "slskd_swarm_chunk_speed_bytes_per_second",
        "Chunk download speed in bytes per second",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(1024.0, 2.0, 15) // 1KB/s to 32MB/s
        });

    /// <summary>
    /// Total bytes downloaded via swarm downloads.
    /// </summary>
    public static readonly Counter SwarmBytesDownloadedTotal = Prometheus.Metrics.CreateCounter(
        "slskd_swarm_bytes_downloaded_total",
        "Total bytes downloaded via swarm downloads");

    /// <summary>
    /// Current download rate in bytes per second across all active swarm downloads.
    /// </summary>
    public static readonly Gauge SwarmDownloadRateBytesPerSecond = Prometheus.Metrics.CreateGauge(
        "slskd_swarm_download_rate_bytes_per_second",
        "Current download rate in bytes per second across all active swarm downloads");
}
