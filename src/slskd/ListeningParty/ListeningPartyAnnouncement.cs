// <copyright file="ListeningPartyAnnouncement.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.ListeningParty;

/// <summary>
///     Listed listening-party directory entry.
/// </summary>
public sealed record ListeningPartyAnnouncement
{
    public const string KindName = "slskdn.listeningParty.announce.v1";

    public string Kind { get; init; } = KindName;
    public string PartyId { get; init; } = string.Empty;
    public string PodId { get; init; } = string.Empty;
    public string ChannelId { get; init; } = string.Empty;
    public string HostPeerId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public string ContentId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public bool AllowMeshStreaming { get; init; }
    public string StreamPath { get; init; } = string.Empty;
    public long StartedAtUnixMs { get; init; }
    public long ExpiresAtUnixMs { get; init; }
    public long LastSeenUnixMs { get; init; }
}

public sealed record ListeningPartyIndex
{
    public List<string> PartyIds { get; init; } = new();
    public long UpdatedAtUnixMs { get; init; }
}
