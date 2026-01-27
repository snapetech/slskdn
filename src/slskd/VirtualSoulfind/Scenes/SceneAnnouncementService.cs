// <copyright file="SceneAnnouncementService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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
/// Phase 6C: T-814 - Real implementation.
/// </summary>
public class SceneAnnouncementService : ISceneAnnouncementService
{
    private readonly ILogger<SceneAnnouncementService> logger;
    private readonly IDhtClient dht;
    private readonly IDhtRateLimiter rateLimiter;
    private readonly Identity.IProfileService? profileService;
    private readonly ISceneService? sceneService;

    public SceneAnnouncementService(
        ILogger<SceneAnnouncementService> logger,
        IDhtClient dht,
        IDhtRateLimiter rateLimiter,
        Identity.IProfileService? profileService = null,
        ISceneService? sceneService = null)
    {
        this.logger = logger;
        this.dht = dht;
        this.rateLimiter = rateLimiter;
        this.profileService = profileService;
        this.sceneService = sceneService;
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
        var announcement = await CreateAnnouncementAsync(join: true, ct);

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
        var announcement = await CreateAnnouncementAsync(join: false, ct);

        // Publish leave announcement with short TTL (5 minutes)
        await dht.PutAsync(key, announcement, ttlSeconds: 300, ct);

        logger.LogInformation("[VSF-SCENE-DHT] Announced leave from scene {SceneId}", sceneId);
    }

    public async Task RefreshAnnouncementsAsync(CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-DHT] Refreshing scene announcements");

        if (sceneService == null)
        {
            logger.LogWarning("[VSF-SCENE-DHT] SceneService not available, cannot refresh announcements");
            return;
        }

        // Get all joined scenes
        var joinedScenes = await sceneService.GetJoinedScenesAsync(ct);

        foreach (var scene in joinedScenes)
        {
            try
            {
                // Refresh join announcement (re-publish with new timestamp)
                await AnnounceJoinAsync(scene.SceneId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-SCENE-DHT] Failed to refresh announcement for scene {SceneId}", scene.SceneId);
            }
        }

        logger.LogInformation("[VSF-SCENE-DHT] Refreshed announcements for {Count} scenes", joinedScenes.Count);
    }

    private async Task<byte[]> CreateAnnouncementAsync(bool join, CancellationToken ct)
    {
        // Compact announcement format:
        // [1 byte: join/leave flag] [8 bytes: timestamp] [8 bytes: peer ID hint]
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var buffer = new byte[17]; // 1 + 8 + 8

        buffer[0] = (byte)(join ? 1 : 0);
        BitConverter.GetBytes(timestamp).CopyTo(buffer, 1);

        // Get actual peer ID hint (first 8 bytes of peer ID)
        byte[] peerIdHint = new byte[8];
        if (profileService != null)
        {
            try
            {
                var profile = await profileService.GetMyProfileAsync(ct);
                if (!string.IsNullOrEmpty(profile.PeerId))
                {
                    // Extract first 8 bytes from peer ID (hex string)
                    var peerIdBytes = System.Text.Encoding.UTF8.GetBytes(profile.PeerId);
                    Array.Copy(peerIdBytes, 0, peerIdHint, 0, Math.Min(8, peerIdBytes.Length));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-SCENE-DHT] Failed to get peer ID for announcement, using placeholder");
            }
        }

        Array.Copy(peerIdHint, 0, buffer, 9, 8);
        return buffer;
    }
}
