// <copyright file="ListeningPartyHub.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.ListeningParty;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using slskd.Authentication;

/// <summary>
///     SignalR fan-out for pod listen-along state.
/// </summary>
[Authorize(Policy = AuthPolicy.Any)]
public sealed class ListeningPartyHub : Hub
{
    public Task JoinParty(string podId, string channelId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(podId, channelId));
    }

    public Task LeaveParty(string podId, string channelId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(podId, channelId));
    }

    internal static string GroupName(string podId, string channelId)
    {
        return $"party:{podId?.Trim()}:{channelId?.Trim()}";
    }
}
