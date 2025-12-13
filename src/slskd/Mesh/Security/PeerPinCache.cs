// <copyright file="PeerPinCache.cs" company="slskdN Team">
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
/// Caches expected SPKI pins for mesh peers.
/// Fetches descriptors from DHT on demand.
/// </summary>
public interface IPeerPinCache
{
    Task<string?> GetExpectedControlSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default);
    Task<string?> GetExpectedDataSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default);
}

public class PeerPinCache : IPeerPinCache
{
    private readonly ILogger<PeerPinCache> logger;
    private readonly IMeshDhtClient dhtClient;
    private readonly IDescriptorSigner descriptorSigner;
    private readonly ConcurrentDictionary<string, CachedDescriptor> cache = new();

    public PeerPinCache(
        ILogger<PeerPinCache> logger,
        IMeshDhtClient dhtClient,
        IDescriptorSigner descriptorSigner)
    {
        this.logger = logger;
        this.dhtClient = dhtClient;
        this.descriptorSigner = descriptorSigner;
    }

    public async Task<string?> GetExpectedControlSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        var descriptor = await GetDescriptorForEndpointAsync(endpoint, ct);
        return descriptor?.TlsControlSpkiSha256;
    }

    public async Task<string?> GetExpectedDataSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        var descriptor = await GetDescriptorForEndpointAsync(endpoint, ct);
        return descriptor?.TlsDataSpkiSha256;
    }

    private async Task<MeshPeerDescriptor?> GetDescriptorForEndpointAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        var endpointKey = endpoint.ToString();

        // Check cache first
        if (cache.TryGetValue(endpointKey, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Descriptor;
        }

        // TODO: Implement reverse lookup from endpoint to PeerId
        // For now, we'll need to scan DHT or maintain a registry
        // This is a limitation - we need the PeerId to fetch the descriptor

        logger.LogWarning("[PeerPinCache] Cannot fetch descriptor for {Endpoint} - reverse lookup not implemented",
            endpoint);

        return null;
    }

    private class CachedDescriptor
    {
        public required MeshPeerDescriptor Descriptor { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}

