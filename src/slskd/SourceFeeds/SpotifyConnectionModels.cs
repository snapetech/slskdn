// <copyright file="SpotifyConnectionModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

public sealed class SpotifyAuthorizationStart
{
    public string AuthorizationUrl { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;
}

public sealed class SpotifyConnectionStatus
{
    public bool Configured { get; init; }

    public bool Connected { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string SpotifyUserId { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; init; }
}
