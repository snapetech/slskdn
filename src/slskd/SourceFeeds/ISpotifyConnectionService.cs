// <copyright file="ISpotifyConnectionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

public interface ISpotifyConnectionService
{
    SpotifyConnectionStatus GetStatus();

    SpotifyAuthorizationStart BeginAuthorization(string redirectUri);

    Task<SpotifyConnectionStatus> CompleteAuthorizationAsync(
        string state,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);

    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    void Disconnect();
}

