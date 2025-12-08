// <copyright file="SecurityHealthCheck.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for security services.
/// Reports on the health of security infrastructure.
/// </summary>
public sealed class SecurityHealthCheck : IHealthCheck
{
    private readonly SecurityServices _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHealthCheck"/> class.
    /// </summary>
    /// <param name="services">Security services.</param>
    public SecurityHealthCheck(SecurityServices services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var issues = new List<string>();

        // Check if security is enabled
        if (_services.IsDisabled)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Security is disabled",
                data: data));
        }

        // Check network guard
        if (_services.NetworkGuard != null)
        {
            var stats = _services.NetworkGuard.GetStats();
            data["networkGuard.connections"] = stats.GlobalConnections;
            data["networkGuard.rateLimitHits"] = stats.RateLimitHits;

            if (stats.GlobalConnections >= stats.MaxGlobalConnections * 0.9)
            {
                issues.Add("Connection limit nearly reached");
            }
        }

        // Check violation tracker
        if (_services.ViolationTracker != null)
        {
            var stats = _services.ViolationTracker.GetStats();
            data["violations.trackedIps"] = stats.TrackedIps;
            data["violations.trackedUsernames"] = stats.TrackedUsernames;
        }

        // Check entropy monitor
        if (_services.EntropyMonitor != null)
        {
            var stats = _services.EntropyMonitor.GetStats();
            data["entropy.checkCount"] = stats.CheckCount;
            data["entropy.criticalCount"] = stats.CriticalCount;
            data["entropy.warningCount"] = stats.WarningCount;

            if (stats.CriticalCount > 0)
            {
                issues.Add("Entropy critical alerts detected");
            }
        }

        // Check event sink
        if (_services.EventSink != null)
        {
            var stats = _services.EventSink.GetStats();
            data["events.total"] = stats.TotalEvents;
            data["events.critical"] = stats.CriticalEvents;
            data["events.high"] = stats.HighEvents;

            if (stats.CriticalEvents > 0)
            {
                issues.Add($"{stats.CriticalEvents} critical security events");
            }
        }

        // Check peer reputation
        if (_services.PeerReputation != null)
        {
            var stats = _services.PeerReputation.GetStats();
            data["reputation.peers"] = stats.TotalPeers;
            data["reputation.untrusted"] = stats.UntrustedPeers;

            var untrustedRatio = stats.TotalPeers > 0
                ? (double)stats.UntrustedPeers / stats.TotalPeers
                : 0;

            if (untrustedRatio > 0.3)
            {
                issues.Add("High ratio of untrusted peers");
            }
        }

        // Check fingerprint detection
        if (_services.FingerprintDetection != null)
        {
            var stats = _services.FingerprintDetection.GetStats();
            data["recon.scanners"] = stats.KnownScanners;

            if (stats.KnownScanners > 10)
            {
                issues.Add("Multiple scanners detected");
            }
        }

        // Check honeypot
        if (_services.Honeypot != null)
        {
            var stats = _services.Honeypot.GetStats();
            data["honeypot.interactions"] = stats.TotalInteractions;
            data["honeypot.threats"] = stats.HighThreats;

            if (stats.HighThreats > 0)
            {
                issues.Add($"{stats.HighThreats} high-threat actors detected");
            }
        }

        // Determine overall health
        if (issues.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Security services healthy",
                data));
        }

        if (issues.Exists(i => i.Contains("critical") || i.Contains("Entropy")))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                string.Join("; ", issues),
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            string.Join("; ", issues),
            data: data));
    }
}

/// <summary>
/// Extension methods for adding security health checks.
/// </summary>
public static class SecurityHealthCheckExtensions
{
    /// <summary>
    /// Add security health check.
    /// </summary>
    public static IHealthChecksBuilder AddSecurityHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "security",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new SecurityHealthCheck(sp.GetRequiredService<SecurityServices>()),
            failureStatus,
            tags));
    }
}
