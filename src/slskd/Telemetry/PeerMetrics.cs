// <copyright file="PeerMetrics.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Telemetry;

using Prometheus;

/// <summary>
/// Prometheus metrics for peer performance tracking.
/// </summary>
public static class PeerMetrics
{
    /// <summary>
    /// Total number of peers tracked.
    /// </summary>
    public static readonly Gauge PeersTrackedTotal = Prometheus.Metrics.CreateGauge(
        "slskd_peers_tracked_total",
        "Total number of peers tracked",
        new GaugeConfiguration
        {
            LabelNames = new[] { "source" } // "soulseek", "overlay"
        });

    /// <summary>
    /// Peer RTT (round-trip time) in milliseconds.
    /// </summary>
    public static readonly Histogram PeerRttMilliseconds = Prometheus.Metrics.CreateHistogram(
        "slskd_peer_rtt_milliseconds",
        "Peer round-trip time in milliseconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "source" }, // "soulseek", "overlay"
            Buckets = Histogram.ExponentialBuckets(10.0, 2.0, 10) // 10ms, 20ms, 40ms, ... up to ~10s
        });

    /// <summary>
    /// Peer throughput in bytes per second.
    /// </summary>
    public static readonly Histogram PeerThroughputBytesPerSecond = Prometheus.Metrics.CreateHistogram(
        "slskd_peer_throughput_bytes_per_second",
        "Peer throughput in bytes per second",
        new HistogramConfiguration
        {
            LabelNames = new[] { "source" }, // "soulseek", "overlay"
            Buckets = Histogram.ExponentialBuckets(1024.0, 2.0, 15) // 1KB/s to 32MB/s
        });

    /// <summary>
    /// Total bytes transferred from peers.
    /// </summary>
    public static readonly Counter PeerBytesTransferredTotal = Prometheus.Metrics.CreateCounter(
        "slskd_peer_bytes_transferred_total",
        "Total bytes transferred from peers",
        new CounterConfiguration
        {
            LabelNames = new[] { "source" } // "soulseek", "overlay"
        });

    /// <summary>
    /// Total chunks requested from peers.
    /// </summary>
    public static readonly Counter PeerChunksRequestedTotal = Prometheus.Metrics.CreateCounter(
        "slskd_peer_chunks_requested_total",
        "Total chunks requested from peers",
        new CounterConfiguration
        {
            LabelNames = new[] { "source" } // "soulseek", "overlay"
        });

    /// <summary>
    /// Total chunks completed successfully from peers.
    /// </summary>
    public static readonly Counter PeerChunksCompletedTotal = Prometheus.Metrics.CreateCounter(
        "slskd_peer_chunks_completed_total",
        "Total chunks completed successfully from peers",
        new CounterConfiguration
        {
            LabelNames = new[] { "source", "status" } // source: "soulseek"/"overlay", status: "success"/"failed"/"timeout"/"corrupted"
        });

    /// <summary>
    /// Peer reputation score (0.0 to 1.0).
    /// </summary>
    public static readonly Gauge PeerReputationScore = Prometheus.Metrics.CreateGauge(
        "slskd_peer_reputation_score",
        "Peer reputation score (0.0 to 1.0)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "source" } // "soulseek", "overlay"
        });

    /// <summary>
    /// Number of consecutive failures from a peer.
    /// </summary>
    public static readonly Gauge PeerConsecutiveFailures = Prometheus.Metrics.CreateGauge(
        "slskd_peer_consecutive_failures",
        "Number of consecutive failures from a peer",
        new GaugeConfiguration
        {
            LabelNames = new[] { "source" } // "soulseek", "overlay"
        });
}
