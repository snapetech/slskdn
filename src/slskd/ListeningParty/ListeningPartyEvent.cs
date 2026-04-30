// <copyright file="ListeningPartyEvent.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.ListeningParty;

/// <summary>
///     Metadata-only pod listen-along event.
/// </summary>
public sealed record ListeningPartyEvent
{
    public const string KindName = "slskdn.listenAlong.v1";

    public string PartyId { get; init; } = string.Empty;
    public string Kind { get; init; } = KindName;
    public string PodId { get; init; } = string.Empty;
    public string ChannelId { get; init; } = string.Empty;
    public string HostPeerId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ContentId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public double PositionSeconds { get; init; }
    public long ServerTimeUnixMs { get; init; }
    public long Sequence { get; init; }
    public bool Listed { get; init; }
    public bool AllowMeshStreaming { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
