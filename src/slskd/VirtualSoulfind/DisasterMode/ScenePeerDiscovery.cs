// <copyright file="ScenePeerDiscovery.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.DisasterMode;

using slskd.VirtualSoulfind.Scenes;

/// <summary>
/// Interface for scene-based peer discovery.
/// </summary>
public interface IScenePeerDiscovery
{
    /// <summary>
    /// Discover peers from joined scenes (fallback when DHT is sparse).
    /// </summary>
    Task<List<string>> DiscoverPeersAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Discover peers for specific content from scenes.
    /// </summary>
    Task<List<string>> DiscoverPeersForContentAsync(
        string contentType,
        string contentId,
        CancellationToken ct = default);
}

/// <summary>
/// Scene-based peer discovery for disaster mode.
/// </summary>
public class ScenePeerDiscovery : IScenePeerDiscovery
{
    private readonly ILogger<ScenePeerDiscovery> logger;
    private readonly ISceneService sceneService;
    private readonly ISceneMembershipTracker membershipTracker;

    public ScenePeerDiscovery(
        ILogger<ScenePeerDiscovery> logger,
        ISceneService sceneService,
        ISceneMembershipTracker membershipTracker)
    {
        this.logger = logger;
        this.sceneService = sceneService;
        this.membershipTracker = membershipTracker;
    }

    public async Task<List<string>> DiscoverPeersAsync(CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-DISCOVERY] Discovering peers from joined scenes");

        var joinedScenes = await sceneService.GetJoinedScenesAsync(ct);
        var allPeers = new HashSet<string>();

        foreach (var scene in joinedScenes)
        {
            try
            {
                var members = await membershipTracker.GetMembersAsync(scene.SceneId, ct);
                foreach (var member in members.Where(m => m.IsActive))
                {
                    allPeers.Add(member.PeerId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-SCENE-DISCOVERY] Failed to get members for scene {SceneId}",
                    scene.SceneId);
            }
        }

        logger.LogInformation("[VSF-SCENE-DISCOVERY] Discovered {PeerCount} peers from {SceneCount} scenes",
            allPeers.Count, joinedScenes.Count);

        return allPeers.ToList();
    }

    public async Task<List<string>> DiscoverPeersForContentAsync(
        string contentType,
        string contentId,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-DISCOVERY] Discovering peers for {ContentType}:{ContentId}",
            contentType, contentId);

        // If content is a label, find label scenes
        if (contentType == "label")
        {
            var sceneId = $"scene:label:{contentId}";
            var metadata = await membershipTracker.GetSceneMetadataAsync(sceneId, ct);

            if (metadata != null)
            {
                var members = await membershipTracker.GetMembersAsync(sceneId, ct);
                var peers = members.Where(m => m.IsActive).Select(m => m.PeerId).ToList();

                logger.LogInformation("[VSF-SCENE-DISCOVERY] Found {PeerCount} peers in label scene {SceneId}",
                    peers.Count, sceneId);

                return peers;
            }
        }

        // Fallback: discover from all scenes
        return await DiscoverPeersAsync(ct);
    }
}
