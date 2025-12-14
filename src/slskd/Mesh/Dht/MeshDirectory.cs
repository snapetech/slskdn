using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.MediaCore;
using MeshPeer = slskd.Mesh.MeshPeerDescriptor;
using MeshContent = slskd.Mesh.MeshContentDescriptor;

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

    public async Task<MeshPeerDescriptor?> FindPeerByIdAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer:{peerId}";
        var raw = await dht.GetRawAsync(key, ct);
        if (raw == null) return null;

        try
        {
            var desc = MessagePackSerializer.Deserialize<MeshPeerDescriptor>(raw);
            var endpoint = desc.Endpoints?.FirstOrDefault();
            return new MeshPeer(desc.PeerId, endpoint, null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDirectory] Failed to decode peer descriptor for {PeerId}", peerId);
            return null;
        }
    }

    public async Task<IReadOnlyList<MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default)
    {
        var key = $"mesh:content-peers:{contentId}";
        var hints = await dht.GetAsync<ContentPeerHints>(key, ct);
        if (hints?.Peers == null || hints.Peers.Count == 0) return Array.Empty<MeshPeer>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fresh = hints.Peers
            .Where(p => now - p.TimestampUnixMs < 3600_000) // 1h freshness
            .Select(p => new MeshPeer(p.PeerId, p.Endpoints?.FirstOrDefault(), null, null))
            .ToList();

        return fresh;
    }

    public async Task<IReadOnlyList<MeshContent>> FindContentByPeerAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer-content:{peerId}";
        var contentList = await dht.GetAsync<List<string>>(key, ct);
        if (contentList == null || contentList.Count == 0) return Array.Empty<MeshContent>();

        var results = new List<MeshContent>();
        foreach (var cid in contentList)
        {
            var contentDescriptor = await dht.GetAsync<MediaCore.ContentDescriptor>($"mesh:content:{cid}", ct);
            if (contentDescriptor == null) continue;
            if (!descriptorValidator.Validate(contentDescriptor, out var reason))
            {
                logger.LogWarning("[MeshDirectory] Invalid content descriptor for {ContentId}: {Reason}", cid, reason);
                continue;
            }

            results.Add(new MeshContent(
                cid,
                contentDescriptor.Hashes?.FirstOrDefault()?.Hex,
                contentDescriptor.SizeBytes ?? 0,
                contentDescriptor.Codec));
        }

        return results;
    }
}
