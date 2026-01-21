// <copyright file="PeerDescriptorRefreshService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;

namespace slskd.Mesh.Dht;

/// <summary>
/// Periodically republishes our peer descriptor to the DHT (refresh TTL) and handles IP changes.
/// </summary>
public class PeerDescriptorRefreshService : BackgroundService
{
    private readonly ILogger<PeerDescriptorRefreshService> logger;
    private readonly IPeerDescriptorPublisher publisher;
    private readonly MeshOptions options;
    private HashSet<string> lastKnownEndpoints = new();

    public PeerDescriptorRefreshService(
        ILogger<PeerDescriptorRefreshService> logger,
        IPeerDescriptorPublisher publisher,
        IOptions<MeshOptions> meshOptions)
    {
        logger.LogInformation("[PeerDescriptorRefreshService] Constructor called");
        this.logger = logger;
        this.publisher = publisher;
        options = meshOptions.Value;

        // Initialize with current endpoints if IP change detection is enabled
        if (options.PeerDescriptorRefresh.EnableIpChangeDetection)
        {
            logger.LogInformation("[PeerDescriptorRefreshService] Getting current endpoints...");
            lastKnownEndpoints = GetCurrentEndpoints();
            logger.LogInformation("[PeerDescriptorRefreshService] Got {Count} endpoints", lastKnownEndpoints.Count);
        }
        logger.LogInformation("[PeerDescriptorRefreshService] Constructor completed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[PeerDescriptorRefreshService] ExecuteAsync called");
        logger.LogInformation("[MeshDHT] Starting peer descriptor refresh service");

        // Use configurable intervals
        var refreshInterval = options.PeerDescriptorRefresh.RefreshInterval;
        var ipCheckInterval = options.PeerDescriptorRefresh.IpCheckInterval;
        var lastRefresh = DateTime.MinValue;
        var lastIpCheck = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var shouldRefresh = false;
            var reason = "unknown";

            // Check for periodic refresh
            if (now - lastRefresh >= refreshInterval)
            {
                shouldRefresh = true;
                reason = "periodic";
                lastRefresh = now;
            }

            // Check for IP changes (if enabled)
            if (options.PeerDescriptorRefresh.EnableIpChangeDetection && now - lastIpCheck >= ipCheckInterval)
            {
                lastIpCheck = now;
                var currentEndpoints = GetCurrentEndpoints();

                if (!EndpointsEqual(lastKnownEndpoints, currentEndpoints))
                {
                    shouldRefresh = true;
                    reason = "ip_change";
                    lastKnownEndpoints = currentEndpoints;
                    logger.LogInformation("[MeshDHT] Detected IP address change, triggering immediate refresh");
                }
            }

            // Perform refresh if needed
            if (shouldRefresh)
            {
                try
                {
                    await publisher.PublishSelfAsync(stoppingToken);
                    logger.LogDebug("[MeshDHT] Refreshed peer descriptor (reason: {Reason})", reason);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[MeshDHT] Peer descriptor refresh failed (reason: {Reason})", reason);
                }
            }

            // Wait for next check interval (shorter of the two intervals)
            var nextCheck = TimeSpan.FromMinutes(1); // Check every minute for responsiveness
            try
            {
                await Task.Delay(nextCheck, stoppingToken);
            }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Gets the current network endpoints for IP change detection.
    /// </summary>
    private HashSet<string> GetCurrentEndpoints()
    {
        var endpoints = new HashSet<string>();

        try
        {
            // Get all network interfaces
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in networkInterfaces)
            {
                var ipProperties = ni.GetIPProperties();

                // Get IPv4 addresses
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        // Only include non-link-local addresses
                        if (!IPAddress.IsLoopback(unicast.Address) &&
                            unicast.Address.ToString() != "0.0.0.0")
                        {
                            endpoints.Add(unicast.Address.ToString());
                        }
                    }
                }

                // Get IPv6 addresses (global scope only)
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        if (!IPAddress.IsLoopback(unicast.Address) &&
                            unicast.Address.ToString() != "::" &&
                            !unicast.Address.ToString().StartsWith("fe80::", StringComparison.OrdinalIgnoreCase)) // Skip link-local
                        {
                            endpoints.Add($"[{unicast.Address}]");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDHT] Failed to enumerate network interfaces for IP change detection");
        }

        return endpoints;
    }

    /// <summary>
    /// Compares two sets of endpoints to determine if they are equal.
    /// </summary>
    private bool EndpointsEqual(HashSet<string> set1, HashSet<string> set2)
    {
        return set1.SetEquals(set2);
    }
}
