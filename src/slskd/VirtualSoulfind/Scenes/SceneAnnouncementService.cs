namespace slskd.VirtualSoulfind.Scenes;

using slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Interface for scene DHT announcements.
/// </summary>
public interface ISceneAnnouncementService
{
    /// <summary>
    /// Announce joining a scene to DHT.
    /// </summary>
    Task AnnounceJoinAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Announce leaving a scene to DHT.
    /// </summary>
    Task AnnounceLeaveAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Refresh scene announcements (periodic heartbeat).
    /// </summary>
    Task RefreshAnnouncementsAsync(CancellationToken ct = default);
}

/// <summary>
/// Manages scene membership announcements to DHT.
/// </summary>
public class SceneAnnouncementService : ISceneAnnouncementService
{
    private readonly ILogger<SceneAnnouncementService> logger;
    private readonly IDhtClient dht;
    private readonly IDhtRateLimiter rateLimiter;

    public SceneAnnouncementService(
        ILogger<SceneAnnouncementService> logger,
        IDhtClient dht,
        IDhtRateLimiter rateLimiter)
    {
        this.logger = logger;
        this.dht = dht;
        this.rateLimiter = rateLimiter;
    }

    public async Task AnnounceJoinAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-DHT] Announcing join to scene {SceneId}", sceneId);

        if (!await rateLimiter.TryAcquireAsync(ct))
        {
            logger.LogWarning("[VSF-SCENE-DHT] Rate limit exceeded, delaying announcement");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        // Derive DHT key for scene membership list
        var key = DhtKeyDerivation.DeriveSceneMembersKey(sceneId);

        // Create announcement value (compact peer ID + timestamp)
        var announcement = CreateAnnouncement(join: true);

        // Publish to DHT with 30 minute TTL (scenes are ephemeral)
        await dht.PutAsync(key, announcement, ttlSeconds: 1800, ct);

        logger.LogInformation("[VSF-SCENE-DHT] Announced join to scene {SceneId}", sceneId);
    }

    public async Task AnnounceLeaveAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-DHT] Announcing leave from scene {SceneId}", sceneId);

        if (!await rateLimiter.TryAcquireAsync(ct))
        {
            logger.LogWarning("[VSF-SCENE-DHT] Rate limit exceeded, delaying announcement");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        var key = DhtKeyDerivation.DeriveSceneMembersKey(sceneId);
        var announcement = CreateAnnouncement(join: false);

        // Publish leave announcement with short TTL (5 minutes)
        await dht.PutAsync(key, announcement, ttlSeconds: 300, ct);

        logger.LogInformation("[VSF-SCENE-DHT] Announced leave from scene {SceneId}", sceneId);
    }

    public async Task RefreshAnnouncementsAsync(CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-DHT] Refreshing scene announcements");

        // TODO: Iterate through joined scenes and refresh announcements
        // For now, this is a no-op stub

        await Task.CompletedTask;
    }

    private byte[] CreateAnnouncement(bool join)
    {
        // Compact announcement format:
        // [1 byte: join/leave flag] [8 bytes: timestamp] [N bytes: peer ID hint]
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var buffer = new byte[17]; // 1 + 8 + 8

        buffer[0] = (byte)(join ? 1 : 0);
        BitConverter.GetBytes(timestamp).CopyTo(buffer, 1);

        // TODO: Add actual peer ID hint (first 8 bytes of peer ID)
        // For now, use placeholder
        return buffer;
    }
}
















