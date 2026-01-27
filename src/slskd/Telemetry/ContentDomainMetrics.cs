// <copyright file="ContentDomainMetrics.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Telemetry;

using Prometheus;

/// <summary>
/// Prometheus metrics for content domain operations (music, movies, TV, books).
/// </summary>
public static class ContentDomainMetrics
{
    /// <summary>
    /// Total content items indexed by domain.
    /// </summary>
    public static readonly Gauge ContentItemsIndexedTotal = Prometheus.Metrics.CreateGauge(
        "slskd_content_items_indexed_total",
        "Total content items indexed",
        new GaugeConfiguration
        {
            LabelNames = new[] { "domain" } // "music", "movies", "tv", "books"
        });

    /// <summary>
    /// Content lookup operations by domain.
    /// </summary>
    public static readonly Counter ContentLookupsTotal = Prometheus.Metrics.CreateCounter(
        "slskd_content_lookups_total",
        "Total content lookup operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "domain", "status" } // domain: "music"/"movies"/"tv"/"books", status: "found"/"not_found"
        });

    /// <summary>
    /// Content lookup duration in milliseconds.
    /// </summary>
    public static readonly Histogram ContentLookupDurationMilliseconds = Prometheus.Metrics.CreateHistogram(
        "slskd_content_lookup_duration_milliseconds",
        "Content lookup duration in milliseconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "domain" }, // "music", "movies", "tv", "books"
            Buckets = Histogram.ExponentialBuckets(1.0, 2.0, 12) // 1ms to ~4s
        });

    /// <summary>
    /// Total downloads by content domain.
    /// </summary>
    public static readonly Counter ContentDownloadsTotal = Prometheus.Metrics.CreateCounter(
        "slskd_content_downloads_total",
        "Total downloads by content domain",
        new CounterConfiguration
        {
            LabelNames = new[] { "domain", "status" } // domain: "music"/"movies"/"tv"/"books", status: "success"/"failed"/"cancelled"
        });

    /// <summary>
    /// Total bytes downloaded by content domain.
    /// </summary>
    public static readonly Counter ContentBytesDownloadedTotal = Prometheus.Metrics.CreateCounter(
        "slskd_content_bytes_downloaded_total",
        "Total bytes downloaded by content domain",
        new CounterConfiguration
        {
            LabelNames = new[] { "domain" } // "music", "movies", "tv", "books"
        });

    /// <summary>
    /// Content quality scores by domain.
    /// </summary>
    public static readonly Histogram ContentQualityScore = Prometheus.Metrics.CreateHistogram(
        "slskd_content_quality_score",
        "Content quality score (0.0 to 1.0)",
        new HistogramConfiguration
        {
            LabelNames = new[] { "domain" }, // "music", "movies", "tv", "books"
            Buckets = new[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 }
        });
}
