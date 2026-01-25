// <copyright file="MeshHealthCheck.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh;

/// <summary>
/// Health check for mesh network connectivity and performance.
/// </summary>
public class MeshHealthCheck : IHealthCheck
{
    private readonly ILogger<MeshHealthCheck> _logger;
    private readonly IMeshStatsCollector _statsCollector;
    private readonly IMeshDirectory _directory;
    private readonly Dht.IMeshDhtClient _dhtClient;

    public MeshHealthCheck(
        ILogger<MeshHealthCheck> logger,
        IMeshStatsCollector statsCollector,
        IMeshDirectory directory,
        Dht.IMeshDhtClient dhtClient)
    {
        _logger = logger;
        _statsCollector = statsCollector;
        _directory = directory;
        _dhtClient = dhtClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsCollector.GetStatsAsync();

            var healthData = new Dictionary<string, object>
            {
                ["dht_sessions"] = stats.ActiveDhtSessions,
                ["overlay_sessions"] = stats.ActiveOverlaySessions,
                ["mirrored_sessions"] = stats.ActiveMirroredSessions,
                ["total_peers"] = stats.TotalPeers,
                ["messages_sent"] = stats.MessagesSent,
                ["messages_received"] = stats.MessagesReceived,
                ["dht_operations_per_second"] = stats.DhtOperationsPerSecond,
                ["routing_table_size"] = stats.RoutingTableSize,
                ["bootstrap_peers"] = stats.BootstrapPeers,
                ["peer_churn_events"] = stats.PeerChurnEvents,
                ["nat_type"] = stats.DetectedNatType.ToString()
            };

            // Check routing table health
            var routingTableHealthy = stats.RoutingTableSize > 0;
            healthData["routing_table_healthy"] = routingTableHealthy;

            // Check peer connectivity
            var peerConnectivityHealthy = stats.TotalPeers > 0;
            healthData["peer_connectivity_healthy"] = peerConnectivityHealthy;

            // Check message flow
            var messageFlowHealthy = stats.MessagesSent > 0 && stats.MessagesReceived > 0;
            healthData["message_flow_healthy"] = messageFlowHealthy;

            // Check DHT performance
            var dhtPerformanceHealthy = stats.DhtOperationsPerSecond >= 0; // Basic check that it's not negative
            healthData["dht_performance_healthy"] = dhtPerformanceHealthy;

            // Overall health assessment
            var isHealthy = routingTableHealthy && peerConnectivityHealthy;

            var status = isHealthy ? HealthStatus.Healthy : HealthStatus.Degraded;

            if (!routingTableHealthy)
            {
                _logger.LogWarning("[MeshHealth] Routing table is empty or unhealthy");
            }

            if (!peerConnectivityHealthy)
            {
                _logger.LogWarning("[MeshHealth] No peer connectivity detected");
            }

            _logger.LogDebug(
                "[MeshHealth] Health check completed: status={Status}, routing_table={RoutingSize}, peers={PeerCount}, dht_ops={DhtOps}/sec",
                status, stats.RoutingTableSize, stats.TotalPeers, stats.DhtOperationsPerSecond);

            return new HealthCheckResult(status, GetDescription(status, stats), data: healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MeshHealth] Health check failed");
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                $"Mesh health check failed: {ex.Message}",
                ex);
        }
    }

    private static string GetDescription(HealthStatus status, MeshTransportStats stats)
    {
        return status switch
        {
            HealthStatus.Healthy =>
                $"Mesh is healthy: {stats.TotalPeers} peers, {stats.RoutingTableSize} routing table entries, {stats.DhtOperationsPerSecond:F1} DHT ops/sec",

            HealthStatus.Degraded =>
                $"Mesh is degraded: {stats.TotalPeers} peers, {stats.RoutingTableSize} routing table entries",

            HealthStatus.Unhealthy =>
                "Mesh is unhealthy",

            _ => "Mesh status unknown"
        };
    }
}

/// <summary>
/// Extension methods for mesh health checks.
/// </summary>
public static class MeshHealthCheckExtensions
{
    /// <summary>
    /// Adds mesh health check to the health checks builder.
    /// </summary>
    public static IHealthChecksBuilder AddMeshHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<MeshHealthCheck>(
            "mesh",
            tags: new[] { "mesh", "network", "dht" });
    }
}

