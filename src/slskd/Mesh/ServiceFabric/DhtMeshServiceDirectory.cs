// <copyright file="DhtMeshServiceDirectory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// DHT-backed implementation of IMeshServiceDirectory.
/// Uses DHT key pattern: svc:&lt;ServiceName&gt;
/// </summary>
public class DhtMeshServiceDirectory : IMeshServiceDirectory
{
    private readonly ILogger<DhtMeshServiceDirectory> _logger;
    private readonly IMeshDhtClient _dhtClient;
    private readonly IMeshServiceDescriptorValidator _validator;
    private readonly MeshServiceFabricOptions _options;
    private readonly SecurityEventLogger? _securityLogger;

    // Discovery metrics: peerId -> (queryCount, serviceNamesQueried, windowStart)
    private readonly ConcurrentDictionary<string, DiscoveryStats> _discoveryMetrics = new();

    public DhtMeshServiceDirectory(
        ILogger<DhtMeshServiceDirectory> logger,
        IMeshDhtClient dhtClient,
        IMeshServiceDescriptorValidator validator,
        Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions> options,
        SecurityEventLogger? securityLogger = null)
    {
        _logger = logger;
        _dhtClient = dhtClient;
        _validator = validator;
        _options = options.Value;
        _securityLogger = securityLogger;
    }

    public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return await FindByNameAsync(serviceName, requestPeerId: null, cancellationToken);
    }

    public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
        string serviceName,
        string? requestPeerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogWarning("[ServiceDirectory] FindByName called with empty service name");
            return Array.Empty<MeshServiceDescriptor>();
        }

        // Track discovery metrics if peer ID provided
        if (!string.IsNullOrWhiteSpace(requestPeerId))
        {
            TrackDiscoveryQuery(requestPeerId, serviceName);
        }

        try
        {
            var dhtKey = $"svc:{serviceName}";
            var rawValue = await _dhtClient.GetRawAsync(dhtKey, cancellationToken);

            if (rawValue == null || rawValue.Length == 0)
            {
                _logger.LogDebug("[ServiceDirectory] No DHT value found for service: {ServiceName}", serviceName);
                return Array.Empty<MeshServiceDescriptor>();
            }

            // Check DHT value size limit
            if (rawValue.Length > _options.MaxDhtValueBytes)
            {
                _logger.LogWarning(
                    "[ServiceDirectory] DHT value too large for {ServiceName}: {Size} > {Max}",
                    serviceName, rawValue.Length, _options.MaxDhtValueBytes);
                return Array.Empty<MeshServiceDescriptor>();
            }

            // Deserialize list of descriptors
            List<MeshServiceDescriptor> descriptors;
            try
            {
                descriptors = MessagePackSerializer.Deserialize<List<MeshServiceDescriptor>>(rawValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ServiceDirectory] Failed to deserialize descriptors for {ServiceName}", serviceName);
                return Array.Empty<MeshServiceDescriptor>();
            }

            if (descriptors == null || descriptors.Count == 0)
            {
                return Array.Empty<MeshServiceDescriptor>();
            }

            // Validate and filter descriptors
            var validated = new List<MeshServiceDescriptor>();
            foreach (var descriptor in descriptors)
            {
                if (!_validator.Validate(descriptor, out var reason))
                {
                    _logger.LogDebug(
                        "[ServiceDirectory] Invalid descriptor for {ServiceName}: {Reason}",
                        serviceName, reason);
                    continue;
                }

                validated.Add(descriptor);

                // Stop at max limit
                if (validated.Count >= _options.MaxDescriptorsPerLookup)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "[ServiceDirectory] Found {Count} valid descriptors for service: {ServiceName}",
                validated.Count, serviceName);

            return validated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServiceDirectory] Error finding service by name: {ServiceName}", serviceName);
            return Array.Empty<MeshServiceDescriptor>();
        }
    }

    public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByIdAsync(
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(serviceId, requestPeerId: null, cancellationToken);
    }

    public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByIdAsync(
        string serviceId,
        string? requestPeerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            _logger.LogWarning("[ServiceDirectory] FindById called with empty service ID");
            return Array.Empty<MeshServiceDescriptor>();
        }

        // Track discovery metrics if peer ID provided
        if (!string.IsNullOrWhiteSpace(requestPeerId))
        {
            TrackDiscoveryQuery(requestPeerId, $"id:{serviceId}");
        }

        try
        {
            // Service ID format: hash("svc:" + ServiceName + ":" + OwnerPeerId)
            // We need to query DHT by scanning service names or use a reverse index
            // For now, this is a stub - full implementation would require additional DHT structures

            _logger.LogDebug("[ServiceDirectory] FindById not yet fully implemented: {ServiceId}", serviceId);

            // TODO: Implement efficient FindById
            // Options:
            // 1. Maintain a separate DHT key: svcid:<ServiceId> -> descriptor
            // 2. Scan known service names (inefficient but works for now)
            // 3. Use DHT FindValue with serviceId directly

            return Array.Empty<MeshServiceDescriptor>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServiceDirectory] Error finding service by ID: {ServiceId}", serviceId);
            return Array.Empty<MeshServiceDescriptor>();
        }
    }

    /// <summary>
    /// Track discovery query for abuse detection.
    /// </summary>
    private void TrackDiscoveryQuery(string peerId, string serviceName)
    {
        var now = DateTimeOffset.UtcNow;
        var windowDuration = TimeSpan.FromMinutes(1);

        var stats = _discoveryMetrics.AddOrUpdate(
            peerId,
            _ => new DiscoveryStats
            {
                QueryCount = 1,
                ServiceNamesQueried = new HashSet<string> { serviceName },
                WindowStart = now
            },
            (_, existing) =>
            {
                // Reset window if expired
                if (now - existing.WindowStart > windowDuration)
                {
                    return new DiscoveryStats
                    {
                        QueryCount = 1,
                        ServiceNamesQueried = new HashSet<string> { serviceName },
                        WindowStart = now
                    };
                }

                // Increment count and add service name
                existing.ServiceNamesQueried.Add(serviceName);
                existing.QueryCount++;
                return existing;
            });

        // Check for suspicious patterns
        DetectDiscoveryAbuse(peerId, stats);
    }

    /// <summary>
    /// Detect suspicious discovery patterns.
    /// </summary>
    private void DetectDiscoveryAbuse(string peerId, DiscoveryStats stats)
    {
        // Pattern 1: Enumeration (querying many different service names)
        if (stats.ServiceNamesQueried.Count > 10)
        {
            _securityLogger?.LogDiscoveryAbuse(
                peerId, 
                "Enumeration", 
                stats.QueryCount, 
                stats.ServiceNamesQueried.Count);
            
            _logger.LogWarning(
                "[ServiceDirectory] Possible enumeration attack from {PeerId}: {Count} unique service names queried in last minute",
                peerId, stats.ServiceNamesQueried.Count);
        }

        // Pattern 2: Rapid-fire queries (too many queries per minute)
        if (stats.QueryCount > 50)
        {
            _securityLogger?.LogDiscoveryAbuse(
                peerId, 
                "RapidFire", 
                stats.QueryCount, 
                stats.ServiceNamesQueried.Count);
            
            _logger.LogWarning(
                "[ServiceDirectory] Possible discovery spam from {PeerId}: {Count} queries in last minute",
                peerId, stats.QueryCount);
        }

        // Pattern 3: Scanning behavior (combination of high count + high diversity)
        if (stats.QueryCount > 30 && stats.ServiceNamesQueried.Count > 5)
        {
            _securityLogger?.LogDiscoveryAbuse(
                peerId, 
                "Scanning", 
                stats.QueryCount, 
                stats.ServiceNamesQueried.Count);
            
            _logger.LogWarning(
                "[ServiceDirectory] Possible scanning behavior from {PeerId}: {QueryCount} queries, {UniqueCount} unique services",
                peerId, stats.QueryCount, stats.ServiceNamesQueried.Count);
        }
    }

    /// <summary>
    /// Get discovery metrics for monitoring.
    /// </summary>
    public DiscoveryMetrics GetDiscoveryMetrics()
    {
        var now = DateTimeOffset.UtcNow;
        var windowDuration = TimeSpan.FromMinutes(1);

        // Filter to recent activity only
        var recentStats = _discoveryMetrics
            .Where(kvp => now - kvp.Value.WindowStart <= windowDuration)
            .ToList();

        return new DiscoveryMetrics
        {
            TotalPeersTracked = _discoveryMetrics.Count,
            ActivePeersLastMinute = recentStats.Count,
            TotalQueriesLastMinute = recentStats.Sum(kvp => kvp.Value.QueryCount),
            SuspiciousPeers = recentStats
                .Where(kvp => kvp.Value.QueryCount > 30 || kvp.Value.ServiceNamesQueried.Count > 5)
                .Select(kvp => new SuspiciousPeerInfo
                {
                    PeerId = kvp.Key,
                    QueryCount = kvp.Value.QueryCount,
                    UniqueServiceCount = kvp.Value.ServiceNamesQueried.Count
                })
                .ToList()
        };
    }
}

/// <summary>
/// Discovery statistics per peer.
/// </summary>
internal class DiscoveryStats
{
    public int QueryCount { get; set; }
    public HashSet<string> ServiceNamesQueried { get; set; } = new();
    public DateTimeOffset WindowStart { get; set; }
}

/// <summary>
/// Discovery metrics for monitoring.
/// </summary>
public sealed record DiscoveryMetrics
{
    public int TotalPeersTracked { get; init; }
    public int ActivePeersLastMinute { get; init; }
    public int TotalQueriesLastMinute { get; init; }
    public List<SuspiciousPeerInfo> SuspiciousPeers { get; init; } = new();
}

/// <summary>
/// Information about a suspicious peer.
/// </summary>
public sealed record SuspiciousPeerInfo
{
    public string PeerId { get; init; } = string.Empty;
    public int QueryCount { get; init; }
    public int UniqueServiceCount { get; init; }
}
