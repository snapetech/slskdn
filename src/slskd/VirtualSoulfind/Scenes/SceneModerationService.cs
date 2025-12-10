namespace slskd.VirtualSoulfind.Scenes;

using System.Collections.Concurrent;

/// <summary>
/// Interface for scene moderation (local only).
/// </summary>
public interface ISceneModerationService
{
    /// <summary>
    /// Mute a peer in a scene (local only, no network effect).
    /// </summary>
    Task MutePeerAsync(string sceneId, string peerId, string? reason = null, CancellationToken ct = default);
    
    /// <summary>
    /// Unmute a peer in a scene.
    /// </summary>
    Task UnmutePeerAsync(string sceneId, string peerId, CancellationToken ct = default);
    
    /// <summary>
    /// Block a peer in a scene (local only, hides all their content).
    /// </summary>
    Task BlockPeerAsync(string sceneId, string peerId, string? reason = null, CancellationToken ct = default);
    
    /// <summary>
    /// Unblock a peer in a scene.
    /// </summary>
    Task UnblockPeerAsync(string sceneId, string peerId, CancellationToken ct = default);
    
    /// <summary>
    /// Check if a peer is muted in a scene.
    /// </summary>
    Task<bool> IsPeerMutedAsync(string sceneId, string peerId, CancellationToken ct = default);
    
    /// <summary>
    /// Check if a peer is blocked in a scene.
    /// </summary>
    Task<bool> IsPeerBlockedAsync(string sceneId, string peerId, CancellationToken ct = default);
    
    /// <summary>
    /// Get moderation actions for a scene.
    /// </summary>
    Task<List<SceneModerationAction>> GetModerationActionsAsync(string sceneId, CancellationToken ct = default);
}

/// <summary>
/// Local moderation service for scenes.
/// </summary>
public class SceneModerationService : ISceneModerationService
{
    private readonly ILogger<SceneModerationService> logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> mutedPeers = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> blockedPeers = new();
    private readonly ConcurrentDictionary<string, List<SceneModerationAction>> moderationLog = new();

    public SceneModerationService(ILogger<SceneModerationService> logger)
    {
        this.logger = logger;
    }

    public Task MutePeerAsync(string sceneId, string peerId, string? reason, CancellationToken ct)
    {
        logger.LogInformation("[VSF-SCENE-MOD] Muting {PeerId} in scene {SceneId}: {Reason}",
            peerId, sceneId, reason ?? "no reason");

        var muted = mutedPeers.GetOrAdd(sceneId, _ => new HashSet<string>());
        lock (muted)
        {
            muted.Add(peerId);
        }

        LogModerationAction(sceneId, peerId, ModerationActionType.Mute, reason);

        return Task.CompletedTask;
    }

    public Task UnmutePeerAsync(string sceneId, string peerId, CancellationToken ct)
    {
        logger.LogInformation("[VSF-SCENE-MOD] Unmuting {PeerId} in scene {SceneId}",
            peerId, sceneId);

        if (mutedPeers.TryGetValue(sceneId, out var muted))
        {
            lock (muted)
            {
                muted.Remove(peerId);
            }
        }

        LogModerationAction(sceneId, peerId, ModerationActionType.Unmute, null);

        return Task.CompletedTask;
    }

    public Task BlockPeerAsync(string sceneId, string peerId, string? reason, CancellationToken ct)
    {
        logger.LogInformation("[VSF-SCENE-MOD] Blocking {PeerId} in scene {SceneId}: {Reason}",
            peerId, sceneId, reason ?? "no reason");

        var blocked = blockedPeers.GetOrAdd(sceneId, _ => new HashSet<string>());
        lock (blocked)
        {
            blocked.Add(peerId);
        }

        LogModerationAction(sceneId, peerId, ModerationActionType.Block, reason);

        return Task.CompletedTask;
    }

    public Task UnblockPeerAsync(string sceneId, string peerId, CancellationToken ct)
    {
        logger.LogInformation("[VSF-SCENE-MOD] Unblocking {PeerId} in scene {SceneId}",
            peerId, sceneId);

        if (blockedPeers.TryGetValue(sceneId, out var blocked))
        {
            lock (blocked)
            {
                blocked.Remove(peerId);
            }
        }

        LogModerationAction(sceneId, peerId, ModerationActionType.Unblock, null);

        return Task.CompletedTask;
    }

    public Task<bool> IsPeerMutedAsync(string sceneId, string peerId, CancellationToken ct)
    {
        if (mutedPeers.TryGetValue(sceneId, out var muted))
        {
            lock (muted)
            {
                return Task.FromResult(muted.Contains(peerId));
            }
        }

        return Task.FromResult(false);
    }

    public Task<bool> IsPeerBlockedAsync(string sceneId, string peerId, CancellationToken ct)
    {
        if (blockedPeers.TryGetValue(sceneId, out var blocked))
        {
            lock (blocked)
            {
                return Task.FromResult(blocked.Contains(peerId));
            }
        }

        return Task.FromResult(false);
    }

    public Task<List<SceneModerationAction>> GetModerationActionsAsync(string sceneId, CancellationToken ct)
    {
        if (moderationLog.TryGetValue(sceneId, out var actions))
        {
            lock (actions)
            {
                return Task.FromResult(actions.ToList());
            }
        }

        return Task.FromResult(new List<SceneModerationAction>());
    }

    private void LogModerationAction(string sceneId, string peerId, ModerationActionType actionType, string? reason)
    {
        var action = new SceneModerationAction
        {
            SceneId = sceneId,
            TargetPeerId = peerId,
            ActionType = actionType,
            Timestamp = DateTimeOffset.UtcNow,
            Reason = reason
        };

        var actions = moderationLog.GetOrAdd(sceneId, _ => new List<SceneModerationAction>());
        lock (actions)
        {
            actions.Add(action);

            // Keep only last 1000 actions per scene
            if (actions.Count > 1000)
            {
                actions.RemoveAt(0);
            }
        }
    }
}
