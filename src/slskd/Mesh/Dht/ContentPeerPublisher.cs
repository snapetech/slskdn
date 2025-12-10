using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Dht;

/// <summary>
/// Publishes content->peer hints for this node.
/// </summary>
public interface IContentPeerPublisher
{
    Task PublishAsync(string contentId, CancellationToken ct = default);
}

public class ContentPeerPublisher : IContentPeerPublisher
{
    private readonly ILogger<ContentPeerPublisher> logger;
    private readonly IMeshDhtClient dht;
    private readonly MeshOptions options;

    public ContentPeerPublisher(
        ILogger<ContentPeerPublisher> logger,
        IMeshDhtClient dht,
        IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.dht = dht;
        this.options = options.Value;
    }

    public async Task PublishAsync(string contentId, CancellationToken ct = default)
    {
        var hint = new ContentPeerHints
        {
            Peers = new List<ContentPeerHint>
            {
                new()
                {
                    PeerId = options.SelfPeerId,
                    Endpoints = options.SelfEndpoints,
                    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            }
        };

        var key = $"mesh:content-peers:{contentId}";
        await dht.PutAsync(key, hint, ttlSeconds: 1800, ct: ct); // 30m TTL
        logger.LogDebug("[MeshContent] Published peer hint for {ContentId} as {PeerId}", contentId, options.SelfPeerId);
    }
}
