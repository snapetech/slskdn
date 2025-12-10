using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Dht;

/// <summary>
/// Publishes our peer descriptor to the mesh DHT and refreshes it.
/// </summary>
public interface IPeerDescriptorPublisher
{
    Task PublishSelfAsync(CancellationToken ct = default);
}

public class PeerDescriptorPublisher : IPeerDescriptorPublisher
{
    private readonly ILogger<PeerDescriptorPublisher> logger;
    private readonly IMeshDhtClient dht;
    private readonly MeshOptions options;
    private readonly INatDetector natDetector;

    public PeerDescriptorPublisher(
        ILogger<PeerDescriptorPublisher> logger,
        IMeshDhtClient dht,
        IOptions<MeshOptions> options,
        INatDetector natDetector)
    {
        this.logger = logger;
        this.dht = dht;
        this.options = options.Value;
        this.natDetector = natDetector;
    }

    public async Task PublishSelfAsync(CancellationToken ct = default)
    {
        var nat = await natDetector.DetectAsync(ct);
        var descriptor = new MeshPeerDescriptor
        {
            PeerId = options.SelfPeerId,
            Endpoints = options.SelfEndpoints,
            NatType = nat.ToString().ToLowerInvariant(),
            RelayRequired = nat == NatType.Symmetric
        };

        var key = $"mesh:peer:{descriptor.PeerId}";
        await dht.PutAsync(key, descriptor, ttlSeconds: 3600, ct: ct);
        logger.LogInformation("[MeshDHT] Published self descriptor {PeerId} endpoints={Count} nat={Nat}", descriptor.PeerId, descriptor.Endpoints.Count, descriptor.NatType);
    }
}
