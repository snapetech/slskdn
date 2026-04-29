// <copyright file="SceneLabelCrateIntegration.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.Scenes;

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
            var peers = members
                .Where(m => m.IsActive && !string.IsNullOrWhiteSpace(m.PeerId))
                .Select(m => m.PeerId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

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
            if (metadata != null)
            {
                return true;
            }

            var members = await sceneTracker.GetMembersAsync(sceneId, ct);
            return members.Any(m => m.IsActive);
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
            .Trim()
            .Replace(" ", "-")
            .Replace("_", "-");

        normalized = string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '-'));
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-');

        return $"scene:label:{normalized}";
    }
}
