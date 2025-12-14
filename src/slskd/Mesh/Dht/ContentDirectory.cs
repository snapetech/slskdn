using Microsoft.Extensions.Logging;
using slskd.MediaCore;
using System.Linq;
using MeshPeer = slskd.Mesh.MeshPeerDescriptor;
using MeshContent = slskd.Mesh.MeshContentDescriptor;

namespace slskd.Mesh.Dht;

/// <summary>
/// DHT-backed content lookup.
/// </summary>
public class ContentDirectory : IMeshDirectory
{
    private readonly ILogger<ContentDirectory> logger;
    private readonly IMeshDhtClient dht;
    private readonly IDescriptorValidator validator;

    public ContentDirectory(
        ILogger<ContentDirectory> logger,
        IMeshDhtClient dht,
        IDescriptorValidator validator)
    {
        this.logger = logger;
        this.dht = dht;
        this.validator = validator;
    }

    public async Task<MeshPeerDescriptor?> FindPeerByIdAsync(string peerId, CancellationToken ct = default)
    {
        // TODO: Implement proper MeshPeerDescriptor return
        throw new NotImplementedException("ContentDirectory.FindPeerByIdAsync not yet implemented");
    }

    public async Task<IReadOnlyList<MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default)
    {
        // TODO: Implement proper MeshPeerDescriptor return
        throw new NotImplementedException("ContentDirectory.FindPeersByContentAsync not yet implemented");
    }

    public async Task<IReadOnlyList<MeshContentDescriptor>> FindContentByPeerAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer-content:{peerId}";
        var contentList = await dht.GetAsync<List<string>>(key, ct);
        if (contentList == null || contentList.Count == 0) return Array.Empty<MeshContent>();

        var results = new List<MeshContent>();
        foreach (var cid in contentList)
        {
            var descriptor = await GetContentDescriptorAsync(cid, ct);
            if (descriptor != null)
            {
                results.Add(new MeshContent(cid, descriptor.Hashes?.FirstOrDefault()?.Hex, descriptor.SizeBytes ?? 0, descriptor.Codec));
            }
        }

        return results;
    }

    /// <summary>
    /// Get a content descriptor by ID (single value).
    /// </summary>
    public async Task<ContentDescriptor?> GetContentDescriptorAsync(string contentId, CancellationToken ct = default)
    {
        var key = $"mesh:content:{contentId}";
        var descriptor = await dht.GetAsync<ContentDescriptor>(key, ct);
        if (descriptor == null)
        {
            return null;
        }

        if (!validator.Validate(descriptor, out var reason))
        {
            logger.LogWarning("[MeshContent] Descriptor invalid for {ContentId}: {Reason}", contentId, reason);
            return null;
        }

        return descriptor;
    }
}
