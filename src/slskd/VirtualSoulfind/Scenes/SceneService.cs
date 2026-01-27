// <copyright file="SceneService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.VirtualSoulfind.Scenes;

/// <summary>
/// Interface for scene management.
/// </summary>
public interface ISceneService
{
    /// <summary>
    /// Get all scenes the local peer has joined.
    /// </summary>
    Task<List<Scene>> GetJoinedScenesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Join a scene (announce to DHT).
    /// </summary>
    Task JoinSceneAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Leave a scene (remove DHT announcement).
    /// </summary>
    Task LeaveSceneAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Get scene metadata from DHT.
    /// </summary>
    Task<SceneMetadata?> GetSceneMetadataAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Get members of a scene.
    /// </summary>
    Task<List<SceneMember>> GetSceneMembersAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Search for scenes by name or tag.
    /// </summary>
    Task<List<SceneMetadata>> SearchScenesAsync(string query, CancellationToken ct = default);
}

/// <summary>
/// Scene management service.
/// </summary>
public class SceneService : ISceneService
{
    private readonly ILogger<SceneService> logger;
    private readonly ISceneAnnouncementService announcements;
    private readonly ISceneMembershipTracker membershipTracker;
    private readonly IOptionsMonitor<Options> optionsMonitor;
    private readonly ConcurrentDictionary<string, Scene> joinedScenes = new();

    public SceneService(
        ILogger<SceneService> logger,
        ISceneAnnouncementService announcements,
        ISceneMembershipTracker membershipTracker,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.announcements = announcements;
        this.membershipTracker = membershipTracker;
        this.optionsMonitor = optionsMonitor;
    }

    public Task<List<Scene>> GetJoinedScenesAsync(CancellationToken ct)
    {
        var scenes = joinedScenes.Values.ToList();
        logger.LogDebug("[VSF-SCENE] Retrieved {Count} joined scenes", scenes.Count);
        return Task.FromResult(scenes);
    }

    public async Task JoinSceneAsync(string sceneId, CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Scenes?.Enabled != true)
        {
            throw new InvalidOperationException("Scenes are not enabled");
        }

        var maxScenes = options.VirtualSoulfind.Scenes.MaxJoinedScenes > 0
            ? options.VirtualSoulfind.Scenes.MaxJoinedScenes
            : 20;
        if (joinedScenes.Count >= maxScenes)
        {
            throw new InvalidOperationException($"Maximum joined scenes limit reached ({maxScenes})");
        }

        if (joinedScenes.ContainsKey(sceneId))
        {
            logger.LogDebug("[VSF-SCENE] Already joined scene {SceneId}", sceneId);
            return;
        }

        // Get scene metadata
        var metadata = await GetSceneMetadataAsync(sceneId, ct);
        if (metadata == null)
        {
            logger.LogWarning("[VSF-SCENE] Scene {SceneId} not found", sceneId);
            throw new InvalidOperationException($"Scene not found: {sceneId}");
        }

        // Announce membership to DHT
        await announcements.AnnounceJoinAsync(sceneId, ct);

        // Track locally
        var scene = new Scene
        {
            SceneId = sceneId,
            Type = metadata.Type,
            DisplayName = metadata.DisplayName,
            Description = metadata.Description,
            MemberCount = metadata.ApproximateMemberCount,
            JoinedAt = DateTimeOffset.UtcNow
        };

        joinedScenes[sceneId] = scene;

        logger.LogInformation("[VSF-SCENE] Joined scene {SceneId} ({DisplayName})",
            sceneId, metadata.DisplayName);
    }

    public async Task LeaveSceneAsync(string sceneId, CancellationToken ct)
    {
        if (!joinedScenes.TryRemove(sceneId, out _))
        {
            logger.LogDebug("[VSF-SCENE] Not a member of scene {SceneId}", sceneId);
            return;
        }

        // Remove DHT announcement
        await announcements.AnnounceLeaveAsync(sceneId, ct);

        logger.LogInformation("[VSF-SCENE] Left scene {SceneId}", sceneId);
    }

    public async Task<SceneMetadata?> GetSceneMetadataAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE] Getting metadata for scene {SceneId}", sceneId);

        // Query DHT for scene metadata
        var metadata = await membershipTracker.GetSceneMetadataAsync(sceneId, ct);

        if (metadata != null)
        {
            logger.LogInformation("[VSF-SCENE] Retrieved metadata for {SceneId}: {MemberCount} members",
                sceneId, metadata.ApproximateMemberCount);
        }

        return metadata;
    }

    public async Task<List<SceneMember>> GetSceneMembersAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE] Getting members for scene {SceneId}", sceneId);

        var members = await membershipTracker.GetMembersAsync(sceneId, ct);

        logger.LogInformation("[VSF-SCENE] Retrieved {Count} members for scene {SceneId}",
            members.Count, sceneId);

        return members;
    }

    public async Task<List<SceneMetadata>> SearchScenesAsync(string query, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE] Searching scenes: {Query}", query);

        // Phase 6C: T-813 - DHT-based scene search
        // Search for scenes by querying DHT with scene key patterns
        // For now, implement basic search by scene ID prefix matching
        var results = new List<SceneMetadata>();

        // Common scene prefixes to search
        var prefixes = new[]
        {
            $"scene:label:{query}",
            $"scene:genre:{query}",
            query // Direct scene ID
        };

        foreach (var sceneId in prefixes)
        {
            try
            {
                var metadata = await GetSceneMetadataAsync(sceneId, ct);
                if (metadata != null)
                {
                    results.Add(metadata);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[VSF-SCENE] Failed to get metadata for scene {SceneId}", sceneId);
            }
        }

        logger.LogInformation("[VSF-SCENE] Scene search '{Query}' returned {Count} results", query, results.Count);
        return results;
    }
}
