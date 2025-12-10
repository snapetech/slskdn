namespace slskd.VirtualSoulfind.Scenes;

/// <summary>
/// Scene type classification.
/// </summary>
public enum SceneType
{
    /// <summary>
    /// Record label scene (e.g., "scene:label:warp-records").
    /// </summary>
    Label,
    
    /// <summary>
    /// Genre/style scene (e.g., "scene:genre:dub-techno").
    /// </summary>
    Genre,
    
    /// <summary>
    /// Private/invite-only scene (e.g., "scene:key:pubkey:friends").
    /// </summary>
    Private
}

/// <summary>
/// Scene (micro-network / community).
/// </summary>
public class Scene
{
    public string SceneId { get; set; } = string.Empty;
    public SceneType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MemberCount { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
}

/// <summary>
/// Scene metadata from DHT.
/// </summary>
public class SceneMetadata
{
    public string SceneId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SceneType Type { get; set; }
    public int ApproximateMemberCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    
    /// <summary>
    /// Top content shared in this scene (MBIDs).
    /// </summary>
    public List<string> PopularContent { get; set; } = new();
}

/// <summary>
/// Scene membership record.
/// </summary>
public class SceneMember
{
    public string SceneId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Scene chat message (optional feature).
/// </summary>
public class SceneChatMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Signature { get; set; }
}

/// <summary>
/// Scene moderation action (local only).
/// </summary>
public class SceneModerationAction
{
    public string SceneId { get; set; } = string.Empty;
    public string TargetPeerId { get; set; } = string.Empty;
    public ModerationActionType ActionType { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Scene moderation action types.
/// </summary>
public enum ModerationActionType
{
    Mute,
    Block,
    Unmute,
    Unblock
}
