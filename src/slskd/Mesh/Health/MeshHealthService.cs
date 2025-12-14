// <copyright file="MeshHealthService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

namespace slskd.Mesh.Health;

/// <summary>
/// Basic mesh health metrics (in-process).
/// </summary>
public interface IMeshHealthService
{
    MeshHealthSnapshot GetSnapshot();
}

public record MeshHealthSnapshot(
    int RoutingNodes,
    int StoredKeys,
    int ContentPeerHints,
    DateTimeOffset GeneratedAt);

public class MeshHealthService : IMeshHealthService
{
    private readonly ILogger<MeshHealthService> logger;
    private readonly InMemoryDhtClient? memDht;

    public MeshHealthService(ILogger<MeshHealthService> logger, IMeshDhtClient dht)
    {
        this.logger = logger;
        memDht = dht as InMemoryDhtClient;
    }

    public MeshHealthSnapshot GetSnapshot()
    {
        if (memDht == null)
        {
            return new MeshHealthSnapshot(0, 0, 0, DateTimeOffset.UtcNow);
        }

        var routingCount = memDht.FindClosest(Array.Empty<byte>(), 1000).Count;
        var (storedKeys, contentHints) = memDht.GetStoreStats();

        var snap = new MeshHealthSnapshot(routingCount, storedKeys, contentHints, DateTimeOffset.UtcNow);
        logger.LogDebug("[MeshHealth] nodes={Nodes} keys={Keys} contentHints={Hints}", routingCount, storedKeys, contentHints);
        return snap;
    }
}
