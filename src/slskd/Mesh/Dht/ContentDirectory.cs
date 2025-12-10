using Microsoft.Extensions.Logging;
using slskd.MediaCore;

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
        var key = $"mesh:peer:{peerId}";
        return await dht.GetAsync<MeshPeerDescriptor>(key, ct);
    }

    public async Task<IReadOnlyList<MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default)
    {
        // For now, peers are not indexed per content; return empty.
        return Array.Empty<MeshPeerDescriptor>();
    }

    public async Task<IReadOnlyList<MeshContentDescriptor>> FindContentByPeerAsync(string peerId, CancellationToken ct = default)
    {
        // Not implemented; would require peer advertisement of content keys.
        return Array.Empty<MeshContentDescriptor>();
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
