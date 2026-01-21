// <copyright file="ContentPeerHints.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;

namespace slskd.Mesh.Dht;

/// <summary>
/// Peer hint entry for a given contentId.
/// </summary>
[MessagePackObject]
public class ContentPeerHint
{
    [Key(0)] public string PeerId { get; set; } = string.Empty;
    [Key(1)] public List<string> Endpoints { get; set; } = new();
    [Key(2)] public long TimestampUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// Payload storing multiple peer hints.
/// </summary>
[MessagePackObject]
public class ContentPeerHints
{
    [Key(0)] public List<ContentPeerHint> Peers { get; set; } = new();
}
