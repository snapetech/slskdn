// <copyright file="PeerEndpointRegistry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// Maintains a registry of endpoint -> PeerId mappings.
/// This enables reverse lookup for certificate pinning.
/// </summary>
public interface IPeerEndpointRegistry
{
    /// <summary>
    /// Registers an endpoint -> PeerId mapping.
    /// </summary>
    void RegisterEndpoint(IPEndPoint endpoint, string peerId);

    /// <summary>
    /// Gets the PeerId for an endpoint, if known.
    /// </summary>
    string? GetPeerId(IPEndPoint endpoint);

    /// <summary>
    /// Removes stale endpoint mappings.
    /// </summary>
    void Cleanup(TimeSpan maxAge);
}

/// <summary>
/// Implementation of peer endpoint registry.
/// </summary>
public class PeerEndpointRegistry : IPeerEndpointRegistry
{
    private readonly ILogger<PeerEndpointRegistry> logger;
    private readonly ConcurrentDictionary<string, EndpointMapping> registry = new();

    public PeerEndpointRegistry(ILogger<PeerEndpointRegistry> logger)
    {
        this.logger = logger;
    }

    public void RegisterEndpoint(IPEndPoint endpoint, string peerId)
    {
        var key = endpoint.ToString();
        var mapping = new EndpointMapping
        {
            PeerId = peerId,
            LastSeen = DateTimeOffset.UtcNow,
        };

        registry.AddOrUpdate(key, mapping, (_, _) => mapping);
        logger.LogDebug("[PeerEndpointRegistry] Registered {Endpoint} -> {PeerId}", endpoint, peerId);
    }

    public string? GetPeerId(IPEndPoint endpoint)
    {
        var key = endpoint.ToString();
        if (registry.TryGetValue(key, out var mapping))
        {
            return mapping.PeerId;
        }

        return null;
    }

    public void Cleanup(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var toRemove = registry
            .Where(kv => kv.Value.LastSeen < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            registry.TryRemove(key, out _);
        }

        if (toRemove.Count > 0)
        {
            logger.LogDebug("[PeerEndpointRegistry] Cleaned up {Count} stale mappings", toRemove.Count);
        }
    }

    private class EndpointMapping
    {
        public required string PeerId { get; init; }
        public required DateTimeOffset LastSeen { get; init; }
    }
}

