// <copyright file="PeerPinCache.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// Caches expected SPKI pins for mesh peers.
/// Fetches descriptors from DHT on demand with anti-rollback protection.
/// </summary>
public interface IPeerPinCache
{
    Task<string?> GetExpectedControlSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default);
    Task<string?> GetExpectedDataSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default);
    MeshPeerDescriptor? GetDescriptor(string peerId);
}

public class PeerPinCache : IPeerPinCache
{
    private readonly ILogger<PeerPinCache> logger;
    private readonly IMeshDhtClient dhtClient;
    private readonly IDescriptorSigner descriptorSigner;
    private readonly IPeerEndpointRegistry endpointRegistry;
    private readonly IDescriptorSeqTracker seqTracker;
    private readonly ConcurrentDictionary<string, CachedDescriptor> cache = new();

    public PeerPinCache(
        ILogger<PeerPinCache> logger,
        IMeshDhtClient dhtClient,
        IDescriptorSigner descriptorSigner,
        IPeerEndpointRegistry endpointRegistry,
        IDescriptorSeqTracker seqTracker)
    {
        this.logger = logger;
        this.dhtClient = dhtClient;
        this.descriptorSigner = descriptorSigner;
        this.endpointRegistry = endpointRegistry;
        this.seqTracker = seqTracker;
    }

    public async Task<string?> GetExpectedControlSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        var descriptor = await GetDescriptorForEndpointAsync(endpoint, ct);
        return descriptor?.TlsControlPins?.FirstOrDefault()?.SpkiSha256;
    }

    public async Task<string?> GetExpectedDataSpkiAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        var descriptor = await GetDescriptorForEndpointAsync(endpoint, ct);
        return descriptor?.TlsDataPins?.FirstOrDefault()?.SpkiSha256;
    }

    public MeshPeerDescriptor? GetDescriptor(string peerId)
    {
        if (cache.TryGetValue(peerId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Descriptor;
        }

        return null;
    }

    private async Task<MeshPeerDescriptor?> GetDescriptorForEndpointAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        // Try to resolve PeerId from endpoint registry
        var peerId = endpointRegistry.GetPeerId(endpoint);
        if (peerId == null)
        {
            logger.LogDebug("[PeerPinCache] No PeerId mapping for {Endpoint}, cannot fetch descriptor", endpoint);
            return null;
        }

        // Check cache first
        if (cache.TryGetValue(peerId, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Descriptor;
        }

        // Fetch from DHT
        try
        {
            var key = $"mesh:peer:{peerId}";
            var descriptor = await dhtClient.GetAsync<MeshPeerDescriptor>(key, ct);

            if (descriptor == null)
            {
                logger.LogWarning("[PeerPinCache] No descriptor found in DHT for PeerId={PeerId}", peerId);
                return null;
            }

            // Verify signature and PeerId derivation
            if (!descriptorSigner.Verify(descriptor))
            {
                logger.LogWarning("[PeerPinCache] Invalid descriptor signature for PeerId={PeerId}", peerId);
                return null;
            }

            // Anti-rollback: check sequence number
            if (!seqTracker.ValidateAndUpdate(peerId, descriptor.DescriptorSeq))
            {
                logger.LogWarning("[PeerPinCache] Descriptor rollback attack detected for PeerId={PeerId}, seq={Seq}",
                    peerId, descriptor.DescriptorSeq);
                return null;
            }

            // Cache for 5 minutes
            cache[peerId] = new CachedDescriptor
            {
                Descriptor = descriptor,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            };

            logger.LogDebug("[PeerPinCache] Cached descriptor for PeerId={PeerId} seq={Seq}",
                peerId, descriptor.DescriptorSeq);
            return descriptor;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PeerPinCache] Failed to fetch descriptor for PeerId={PeerId}", peerId);
            return null;
        }
    }

    private class CachedDescriptor
    {
        public required MeshPeerDescriptor Descriptor { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}

