// <copyright file="FingerprintDetection.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects protocol fingerprinting and reconnaissance attempts.
/// SECURITY: Identifies entities probing the system for vulnerabilities.
/// </summary>
public sealed class FingerprintDetection : IDisposable
{
    private readonly ILogger<FingerprintDetection> _logger;
    private readonly ConcurrentDictionary<string, ReconnaissanceProfile> _profiles = new();
    private readonly ConcurrentQueue<ReconnaissanceEvent> _events = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Maximum events to keep.
    /// </summary>
    public const int MaxEvents = 5000;

    /// <summary>
    /// Maximum profiles to track.
    /// </summary>
    public const int MaxProfiles = 1000;

    /// <summary>
    /// How long to retain profiles.
    /// </summary>
    public TimeSpan ProfileRetention { get; init; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Threshold for flagging as scanner.
    /// </summary>
    public int ScannerThreshold { get; init; } = 10;

    /// <summary>
    /// Event raised when reconnaissance detected.
    /// </summary>
    public event EventHandler<ReconnaissanceEventArgs>? ReconnaissanceDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprintDetection"/> class.
    /// </summary>
    public FingerprintDetection(ILogger<FingerprintDetection> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupOld, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Record a connection attempt for fingerprint analysis.
    /// </summary>
    public ReconnaissanceAnalysis RecordConnection(
        IPAddress ip,
        int port,
        string? protocolVersion = null,
        string? userAgent = null,
        bool succeeded = true)
    {
        var profile = GetOrCreateProfile(ip);
        var now = DateTimeOffset.UtcNow;
        var indicators = new List<ReconnaissanceIndicator>();

        lock (profile)
        {
            profile.ConnectionAttempts++;
            profile.LastSeen = now;
            profile.PortsProbed.Add(port);

            if (!succeeded)
            {
                profile.FailedConnections++;
            }

            if (!string.IsNullOrEmpty(protocolVersion))
            {
                profile.ProtocolVersionsSeen.Add(protocolVersion);
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                profile.UserAgentsSeen.Add(userAgent);
            }

            // Detect patterns

            // 1. Multiple ports probed (port scanning)
            if (profile.PortsProbed.Count > 3)
            {
                indicators.Add(new ReconnaissanceIndicator
                {
                    Type = IndicatorType.PortScanning,
                    Description = $"Probed {profile.PortsProbed.Count} different ports",
                    Severity = profile.PortsProbed.Count > 10 ? Severity.High : Severity.Medium,
                });
            }

            // 2. Multiple protocol versions (version enumeration)
            if (profile.ProtocolVersionsSeen.Count > 2)
            {
                indicators.Add(new ReconnaissanceIndicator
                {
                    Type = IndicatorType.VersionEnumeration,
                    Description = $"Tried {profile.ProtocolVersionsSeen.Count} protocol versions",
                    Severity = Severity.Medium,
                });
            }

            // 3. Multiple user agents (fingerprinting tools often cycle)
            if (profile.UserAgentsSeen.Count > 3)
            {
                indicators.Add(new ReconnaissanceIndicator
                {
                    Type = IndicatorType.UserAgentRotation,
                    Description = $"Used {profile.UserAgentsSeen.Count} different user agents",
                    Severity = Severity.Medium,
                });
            }

            // 4. High failure rate (probing)
            if (profile.ConnectionAttempts > 5)
            {
                var failRate = (double)profile.FailedConnections / profile.ConnectionAttempts;
                if (failRate > 0.5)
                {
                    indicators.Add(new ReconnaissanceIndicator
                    {
                        Type = IndicatorType.HighFailureRate,
                        Description = $"{failRate:P0} connection failure rate",
                        Severity = Severity.Medium,
                    });
                }
            }

            // 5. Rapid connection attempts (automated tool)
            if (profile.ConnectionAttempts > 3)
            {
                var duration = now - profile.FirstSeen;
                if (duration.TotalSeconds > 0)
                {
                    var rate = profile.ConnectionAttempts / duration.TotalSeconds;
                    if (rate > 1) // More than 1 connection/second
                    {
                        indicators.Add(new ReconnaissanceIndicator
                        {
                            Type = IndicatorType.RapidConnections,
                            Description = $"{rate:F1} connections/second",
                            Severity = Severity.High,
                        });
                    }
                }
            }

            // Update profile
            foreach (var indicator in indicators)
            {
                profile.Indicators[indicator.Type] = indicator;
            }

            profile.IsScanner = profile.Indicators.Count >= 2 ||
                               profile.Indicators.Any(i => i.Value.Severity == Severity.High);
        }

        // Record event if suspicious
        if (indicators.Count > 0)
        {
            var evt = new ReconnaissanceEvent
            {
                Timestamp = now,
                IpAddress = ip.ToString(),
                Indicators = indicators,
                IsScanner = profile.IsScanner,
            };

            _events.Enqueue(evt);
            while (_events.Count > MaxEvents)
            {
                _events.TryDequeue(out _);
            }

            if (profile.IsScanner)
            {
                _logger.LogWarning(
                    "Scanner detected from {Ip}: {Indicators}",
                    ip,
                    string.Join(", ", indicators.Select(i => i.Type)));

                ReconnaissanceDetected?.Invoke(this, new ReconnaissanceEventArgs(evt));
            }
        }

        return new ReconnaissanceAnalysis
        {
            IsScanner = profile.IsScanner,
            Indicators = indicators,
            TotalAttempts = profile.ConnectionAttempts,
            PortsProbed = profile.PortsProbed.Count,
            FirstSeen = profile.FirstSeen,
        };
    }

    /// <summary>
    /// Record an unusual request pattern.
    /// </summary>
    public void RecordAnomalousRequest(IPAddress ip, string requestType, string details)
    {
        var profile = GetOrCreateProfile(ip);

        lock (profile)
        {
            profile.AnomalousRequests++;
            profile.LastSeen = DateTimeOffset.UtcNow;

            if (profile.AnomalousRequests > 5)
            {
                profile.Indicators[IndicatorType.AnomalousRequests] = new ReconnaissanceIndicator
                {
                    Type = IndicatorType.AnomalousRequests,
                    Description = $"{profile.AnomalousRequests} anomalous requests",
                    Severity = Severity.High,
                };
                profile.IsScanner = true;
            }
        }

        _logger.LogDebug("Anomalous request from {Ip}: {Type} - {Details}", ip, requestType, details);
    }

    /// <summary>
    /// Check if an IP is a known scanner.
    /// </summary>
    public bool IsKnownScanner(IPAddress ip)
    {
        return _profiles.TryGetValue(ip.ToString(), out var profile) && profile.IsScanner;
    }

    /// <summary>
    /// Get profile for an IP.
    /// </summary>
    public ReconnaissanceProfile? GetProfile(IPAddress ip)
    {
        return _profiles.TryGetValue(ip.ToString(), out var p) ? p : null;
    }

    /// <summary>
    /// Get recent events.
    /// </summary>
    public IReadOnlyList<ReconnaissanceEvent> GetRecentEvents(int count = 100)
    {
        return _events.Reverse().Take(count).ToList();
    }

    /// <summary>
    /// Get known scanners.
    /// </summary>
    public IReadOnlyList<ReconnaissanceProfile> GetKnownScanners()
    {
        return _profiles.Values
            .Where(p => p.IsScanner)
            .OrderByDescending(p => p.ConnectionAttempts)
            .ToList();
    }

    /// <summary>
    /// Get statistics.
    /// </summary>
    public ReconnaissanceStats GetStats()
    {
        var profiles = _profiles.Values.ToList();
        return new ReconnaissanceStats
        {
            TrackedIps = profiles.Count,
            KnownScanners = profiles.Count(p => p.IsScanner),
            TotalConnectionAttempts = profiles.Sum(p => p.ConnectionAttempts),
            TotalAnomalousRequests = profiles.Sum(p => p.AnomalousRequests),
            EventCount = _events.Count,
        };
    }

    private ReconnaissanceProfile GetOrCreateProfile(IPAddress ip)
    {
        var key = ip.ToString();

        // Enforce max size
        if (_profiles.Count >= MaxProfiles && !_profiles.ContainsKey(key))
        {
            var oldest = _profiles
                .OrderBy(kvp => kvp.Value.LastSeen)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(oldest.Key))
            {
                _profiles.TryRemove(oldest.Key, out _);
            }
        }

        return _profiles.GetOrAdd(key, _ => new ReconnaissanceProfile
        {
            IpAddress = ip.ToString(),
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }

    private void CleanupOld(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow - ProfileRetention;

        var toRemove = _profiles
            .Where(kvp => kvp.Value.LastSeen < cutoff && !kvp.Value.IsScanner)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _profiles.TryRemove(key, out _);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// Profile tracking reconnaissance from an IP.
/// </summary>
public sealed class ReconnaissanceProfile
{
    /// <summary>Gets the IP address.</summary>
    public required string IpAddress { get; init; }

    /// <summary>Gets when first seen.</summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Gets or sets when last seen.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets connection attempts.</summary>
    public int ConnectionAttempts { get; set; }

    /// <summary>Gets or sets failed connections.</summary>
    public int FailedConnections { get; set; }

    /// <summary>Gets or sets anomalous requests.</summary>
    public int AnomalousRequests { get; set; }

    /// <summary>Gets ports probed.</summary>
    public HashSet<int> PortsProbed { get; } = new();

    /// <summary>Gets protocol versions seen.</summary>
    public HashSet<string> ProtocolVersionsSeen { get; } = new();

    /// <summary>Gets user agents seen.</summary>
    public HashSet<string> UserAgentsSeen { get; } = new();

    /// <summary>Gets indicators detected.</summary>
    public Dictionary<IndicatorType, ReconnaissanceIndicator> Indicators { get; } = new();

    /// <summary>Gets or sets whether flagged as scanner.</summary>
    public bool IsScanner { get; set; }
}

/// <summary>
/// A reconnaissance indicator.
/// </summary>
public sealed class ReconnaissanceIndicator
{
    /// <summary>Gets the indicator type.</summary>
    public required IndicatorType Type { get; init; }

    /// <summary>Gets the description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the severity.</summary>
    public required Severity Severity { get; init; }
}

/// <summary>
/// Types of reconnaissance indicators.
/// </summary>
public enum IndicatorType
{
    /// <summary>Multiple ports being probed.</summary>
    PortScanning,

    /// <summary>Multiple protocol versions tried.</summary>
    VersionEnumeration,

    /// <summary>Rotating user agent strings.</summary>
    UserAgentRotation,

    /// <summary>High connection failure rate.</summary>
    HighFailureRate,

    /// <summary>Rapid connection attempts.</summary>
    RapidConnections,

    /// <summary>Anomalous request patterns.</summary>
    AnomalousRequests,
}

/// <summary>
/// Severity level.
/// </summary>
public enum Severity
{
    /// <summary>Low severity.</summary>
    Low,

    /// <summary>Medium severity.</summary>
    Medium,

    /// <summary>High severity.</summary>
    High,
}

/// <summary>
/// A reconnaissance event.
/// </summary>
public sealed class ReconnaissanceEvent
{
    /// <summary>Gets when detected.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the IP address.</summary>
    public required string IpAddress { get; init; }

    /// <summary>Gets indicators detected.</summary>
    public required List<ReconnaissanceIndicator> Indicators { get; init; }

    /// <summary>Gets whether flagged as scanner.</summary>
    public required bool IsScanner { get; init; }
}

/// <summary>
/// Event args for reconnaissance detection.
/// </summary>
public sealed class ReconnaissanceEventArgs : EventArgs
{
    /// <summary>Gets the event.</summary>
    public ReconnaissanceEvent Event { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconnaissanceEventArgs"/> class.
    /// </summary>
    public ReconnaissanceEventArgs(ReconnaissanceEvent evt)
    {
        Event = evt;
    }
}

/// <summary>
/// Analysis result.
/// </summary>
public sealed class ReconnaissanceAnalysis
{
    /// <summary>Gets whether flagged as scanner.</summary>
    public required bool IsScanner { get; init; }

    /// <summary>Gets indicators.</summary>
    public required List<ReconnaissanceIndicator> Indicators { get; init; }

    /// <summary>Gets total connection attempts.</summary>
    public required int TotalAttempts { get; init; }

    /// <summary>Gets ports probed count.</summary>
    public required int PortsProbed { get; init; }

    /// <summary>Gets when first seen.</summary>
    public required DateTimeOffset FirstSeen { get; init; }
}

/// <summary>
/// Statistics about fingerprint detection.
/// </summary>
public sealed class ReconnaissanceStats
{
    /// <summary>Gets tracked IPs.</summary>
    public int TrackedIps { get; init; }

    /// <summary>Gets known scanners.</summary>
    public int KnownScanners { get; init; }

    /// <summary>Gets total connection attempts.</summary>
    public long TotalConnectionAttempts { get; init; }

    /// <summary>Gets total anomalous requests.</summary>
    public long TotalAnomalousRequests { get; init; }

    /// <summary>Gets event count.</summary>
    public int EventCount { get; init; }
}

