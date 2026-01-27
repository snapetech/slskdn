// <copyright file="SceneModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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
/// Phase 6C: T-815 - MessagePack serializable.
/// </summary>
[MessagePack.MessagePackObject]
public class SceneMetadata
{
    [MessagePack.Key(0)]
    public string SceneId { get; set; } = string.Empty;
    
    [MessagePack.Key(1)]
    public string DisplayName { get; set; } = string.Empty;
    
    [MessagePack.Key(2)]
    public string? Description { get; set; }
    
    [MessagePack.Key(3)]
    public SceneType Type { get; set; }
    
    [MessagePack.Key(4)]
    public int ApproximateMemberCount { get; set; }
    
    [MessagePack.Key(5)]
    public DateTimeOffset CreatedAt { get; set; }
    
    [MessagePack.Key(6)]
    public DateTimeOffset LastUpdatedAt { get; set; }
    
    /// <summary>
    /// Top content shared in this scene (MBIDs).
    /// </summary>
    [MessagePack.Key(7)]
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
