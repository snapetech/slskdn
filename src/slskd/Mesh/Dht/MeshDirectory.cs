using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.MediaCore;

namespace slskd.Mesh.Dht;

/// <summary>
/// DHT-backed mesh directory implementation (basic lookup).
/// </summary>
public class MeshDirectory : IMeshDirectory
{
    private readonly ILogger<MeshDirectory> logger;
    private readonly IMeshDhtClient dht;
    private readonly MediaCore.IDescriptorValidator descriptorValidator;

    public MeshDirectory(
        ILogger<MeshDirectory> logger,
        IMeshDhtClient dht,
        MediaCore.IDescriptorValidator descriptorValidator)
    {
        this.logger = logger;
        this.dht = dht;
        this.descriptorValidator = descriptorValidator;
    }

    public async Task<slskd.Mesh.MeshPeerDescriptor?> FindPeerByIdAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer:{peerId}";
        var raw = await dht.GetRawAsync(key, ct);
        if (raw == null) return null;

        try
        {
            // Deserialize DHT descriptor
            var dhtDesc = MessagePackSerializer.Deserialize<MeshPeerDescriptor>(raw);
            
            // Convert to interface descriptor
            var endpoint = dhtDesc.Endpoints?.FirstOrDefault();
            var (address, port) = ParseEndpoint(endpoint);
            return new slskd.Mesh.MeshPeerDescriptor(dhtDesc.PeerId, address, port);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDirectory] Failed to decode peer descriptor for {PeerId}", peerId);
            return null;
        }
    }

    public async Task<IReadOnlyList<slskd.Mesh.MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default)
    {
        var key = $"mesh:content-peers:{contentId}";
        var hints = await dht.GetAsync<ContentPeerHints>(key, ct);
        if (hints?.Peers == null || hints.Peers.Count == 0) return Array.Empty<slskd.Mesh.MeshPeerDescriptor>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fresh = hints.Peers
            .Where(p => now - p.TimestampUnixMs < 3600_000) // 1h freshness
            .Select(p =>
            {
                var endpoint = p.Endpoints?.FirstOrDefault();
                var (address, port) = ParseEndpoint(endpoint);
                return new slskd.Mesh.MeshPeerDescriptor(p.PeerId, address, port);
            })
            .ToList();

        return fresh;
    }

    public async Task<IReadOnlyList<slskd.Mesh.MeshContentDescriptor>> FindContentByPeerAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer-content:{peerId}";
        var contentList = await dht.GetAsync<List<string>>(key, ct);
        if (contentList == null || contentList.Count == 0) return Array.Empty<slskd.Mesh.MeshContentDescriptor>();

        var results = new List<slskd.Mesh.MeshContentDescriptor>();
        foreach (var cid in contentList)
        {
            var contentDescriptor = await dht.GetAsync<MediaCore.ContentDescriptor>($"mesh:content:{cid}", ct);
            if (contentDescriptor == null) continue;
            if (!descriptorValidator.Validate(contentDescriptor, out var reason))
            {
                logger.LogWarning("[MeshDirectory] Invalid content descriptor for {ContentId}: {Reason}", cid, reason);
                continue;
            }

            results.Add(new slskd.Mesh.MeshContentDescriptor(
                cid,
                contentDescriptor.Hashes?.FirstOrDefault()?.Hex,
                contentDescriptor.SizeBytes ?? 0,
                contentDescriptor.Codec));
        }

        return results;
    }

    /// <summary>
    /// Parse an endpoint string (e.g., "host:port") into address and port.
    /// </summary>
    private (string? address, int? port) ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint)) return (null, null);
        
        var parts = endpoint.Split(':');
        if (parts.Length != 2) return (endpoint, null);
        
        var address = parts[0];
        var port = int.TryParse(parts[1], out var p) ? p : (int?)null;
        return (address, port);
    }
}
