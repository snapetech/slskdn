// <copyright file="TelemetryDashboard.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.DisasterMode;

namespace slskd.VirtualSoulfind.Integration;

/// <summary>
/// Telemetry dashboard data.
/// </summary>
public class TelemetryDashboard
{
    public ShadowIndexStats ShadowIndex { get; set; } = new();
    public DisasterModeStats DisasterMode { get; set; } = new();
    public SceneStats Scenes { get; set; } = new();
    public PerformanceStats Performance { get; set; } = new();
}

/// <summary>
/// Shadow index statistics.
/// </summary>
public class ShadowIndexStats
{
    public int TotalShards { get; set; }
    public int TotalPeerHints { get; set; }
    public int TotalVariants { get; set; }
    public DateTimeOffset? LastPublish { get; set; }
    public int PublishCount { get; set; }
}

/// <summary>
/// Disaster mode statistics.
/// </summary>
public class DisasterModeStats
{
    public bool IsActive { get; set; }
    public int TotalActivations { get; set; }
    public TimeSpan TotalDisasterTime { get; set; }
    public DateTimeOffset? LastActivation { get; set; }
    public DateTimeOffset? LastDeactivation { get; set; }
    public int MeshSearchCount { get; set; }
    public int MeshTransferCount { get; set; }
}

/// <summary>
/// Scene statistics.
/// </summary>
public class SceneStats
{
    public int JoinedSceneCount { get; set; }
    public int TotalSceneMembers { get; set; }
    public List<string> TopScenes { get; set; } = new();
}

/// <summary>
/// Performance statistics.
/// </summary>
public class PerformanceStats
{
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double CacheHitRate { get; set; }
    public TimeSpan AverageDhtQueryTime { get; set; }
}

/// <summary>
/// Telemetry dashboard service.
/// </summary>
public interface ITelemetryDashboard
{
    Task<TelemetryDashboard> GetDashboardDataAsync(CancellationToken ct = default);
}

/// <summary>
/// Telemetry dashboard implementation.
/// </summary>
public class TelemetryDashboardService : ITelemetryDashboard
{
    private readonly ILogger<TelemetryDashboardService> logger;
    private readonly IDisasterModeTelemetry disasterTelemetry;
    private readonly IPerformanceOptimizer perfOptimizer;

    public TelemetryDashboardService(
        ILogger<TelemetryDashboardService> logger,
        IDisasterModeTelemetry disasterTelemetry,
        IPerformanceOptimizer perfOptimizer)
    {
        this.logger = logger;
        this.disasterTelemetry = disasterTelemetry;
        this.perfOptimizer = perfOptimizer;
    }

    public Task<TelemetryDashboard> GetDashboardDataAsync(CancellationToken ct)
    {
        logger.LogDebug("[VSF-DASHBOARD] Generating telemetry dashboard");

        var disasterData = disasterTelemetry.GetTelemetry();
        var cacheStats = perfOptimizer.GetCacheStatistics();

        var dashboard = new TelemetryDashboard
        {
            DisasterMode = new DisasterModeStats
            {
                IsActive = disasterData.LastActivation > disasterData.LastDeactivation,
                TotalActivations = disasterData.ActivationCount,
                TotalDisasterTime = disasterData.TotalDisasterModeTime,
                LastActivation = disasterData.LastActivation,
                LastDeactivation = disasterData.LastDeactivation,
                MeshSearchCount = disasterData.MeshSearchCount,
                MeshTransferCount = disasterData.MeshTransferCount
            },
            Performance = new PerformanceStats
            {
                CacheHits = cacheStats.CacheHits,
                CacheMisses = cacheStats.CacheMisses,
                CacheHitRate = cacheStats.HitRate
            }
        };

        return Task.FromResult(dashboard);
    }
}
