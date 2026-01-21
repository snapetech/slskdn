// <copyright file="ContentPeerPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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
                    Endpoints = options.SelfEndpoints
                        .Concat(options.RelayEndpoints ?? new List<string>())
                        .ToList(),
                    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            }
        };

        var key = $"mesh:content-peers:{contentId}";
        await dht.PutAsync(key, hint, ttlSeconds: 1800, ct: ct); // 30m TTL
        logger.LogDebug("[MeshContent] Published peer hint for {ContentId} as {PeerId}", contentId, options.SelfPeerId);

        // Reverse mapping: peer -> content list
        var peerKey = $"mesh:peer-content:{options.SelfPeerId}";
        var existing = await dht.GetAsync<List<string>>(peerKey, ct) ?? new List<string>();
        if (!existing.Contains(contentId))
        {
            existing.Add(contentId);
        }
        await dht.PutAsync(peerKey, existing, ttlSeconds: 1800, ct: ct);
    }
}
