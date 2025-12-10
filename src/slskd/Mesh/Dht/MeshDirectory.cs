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

    public async Task<MeshPeerDescriptor?> FindPeerByIdAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer:{peerId}";
        var raw = await dht.GetRawAsync(key, ct);
        if (raw == null) return null;

        try
        {
            var desc = MessagePackSerializer.Deserialize<MeshPeerDescriptor>(raw);
            return desc;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDirectory] Failed to decode peer descriptor for {PeerId}", peerId);
            return null;
        }
    }

    public async Task<IReadOnlyList<MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default)
    {
        // For now, no index from content→peers. This could query shadow index later.
        return Array.Empty<MeshPeerDescriptor>();
    }

    public async Task<IReadOnlyList<MeshContentDescriptor>> FindContentByPeerAsync(string peerId, CancellationToken ct = default)
    {
        // Not implemented; would require peer advertisement of content keys.
        return Array.Empty<MeshContentDescriptor>();
    }
}
