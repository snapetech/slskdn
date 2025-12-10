namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.Scenes;
using slskd.Jobs.LabelCrate;

/// <summary>
/// Integrates scenes with label crate jobs.
/// </summary>
public interface ISceneLabelCrateIntegration
{
    /// <summary>
    /// Get label crate peers from scene.
    /// </summary>
    Task<List<string>> GetLabelScenePeersAsync(string labelName, CancellationToken ct = default);
    
    /// <summary>
    /// Check if a label has a scene.
    /// </summary>
    Task<bool> HasLabelSceneAsync(string labelName, CancellationToken ct = default);
}

/// <summary>
/// Scene integration for label crate jobs.
/// </summary>
public class SceneLabelCrateIntegration : ISceneLabelCrateIntegration
{
    private readonly ILogger<SceneLabelCrateIntegration> logger;
    private readonly ISceneMembershipTracker sceneTracker;

    public SceneLabelCrateIntegration(
        ILogger<SceneLabelCrateIntegration> logger,
        ISceneMembershipTracker sceneTracker)
    {
        this.logger = logger;
        this.sceneTracker = sceneTracker;
    }

    public async Task<List<string>> GetLabelScenePeersAsync(string labelName, CancellationToken ct)
    {
        var sceneId = NormalizeLabelToSceneId(labelName);
        
        logger.LogDebug("[VSF-INTEGRATION] Getting peers from label scene {SceneId}", sceneId);

        try
        {
            var members = await sceneTracker.GetMembersAsync(sceneId, ct);
            var peers = members.Where(m => m.IsActive).Select(m => m.PeerId).ToList();

            logger.LogInformation("[VSF-INTEGRATION] Found {PeerCount} peers in label scene {SceneId}",
                peers.Count, sceneId);

            return peers;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-INTEGRATION] Failed to get peers from label scene {SceneId}", sceneId);
            return new List<string>();
        }
    }

    public async Task<bool> HasLabelSceneAsync(string labelName, CancellationToken ct)
    {
        var sceneId = NormalizeLabelToSceneId(labelName);
        
        try
        {
            var metadata = await sceneTracker.GetSceneMetadataAsync(sceneId, ct);
            return metadata != null;
        }
        catch
        {
            return false;
        }
    }

    private string NormalizeLabelToSceneId(string labelName)
    {
        // "Warp Records" → "scene:label:warp-records"
        var normalized = labelName
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
        
        return $"scene:label:{normalized}";
    }
}
