// <copyright file="SongIdHub.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SongID.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using slskd.Authentication;

public static class SongIdHubMethods
{
    public static readonly string List = "LIST";
    public static readonly string Create = "CREATE";
    public static readonly string Update = "UPDATE";
}

public static class SongIdHubExtensions
{
    public static Task BroadcastCreateAsync(this IHubContext<SongIdHub> hub, SongIdRun run)
    {
        return hub.Clients.All.SendAsync(SongIdHubMethods.Create, run);
    }

    public static Task BroadcastUpdateAsync(this IHubContext<SongIdHub> hub, SongIdRun run)
    {
        return hub.Clients.All.SendAsync(SongIdHubMethods.Update, run);
    }
}

[Authorize(Policy = AuthPolicy.Any)]
public sealed class SongIdHub : Hub
{
    private readonly ISongIdService _songIdService;

    public SongIdHub(ISongIdService songIdService)
    {
        _songIdService = songIdService;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(SongIdHubMethods.List, _songIdService.List(15));
    }
}
