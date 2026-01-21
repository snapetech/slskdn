// <copyright file="TransfersHub.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;

    public static class TransferHubMethods
    {
        public static readonly string Activity = "ACTIVITY";
    }

    /// <summary>
    ///     Extension methods for the transfers SignalR hub.
    /// </summary>
    public static class TransferHubExtensions
    {
        /// <summary>
        ///     Broadcast transfer activity.
        /// </summary>
        /// <param name="hub">The hub.</param>
        /// <param name="activity">The transfer activity to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task EmitTransferActivityAsync(this IHubContext<TransfersHub> hub, TransferActivity activity)
        {
            return hub.Clients.All.SendAsync(TransferHubMethods.Activity, activity);
        }
    }

    /// <summary>
    ///     The transfers SignalR hub.
    /// </summary>
    [Authorize(Policy = AuthPolicy.Any)]
    public class TransfersHub : Hub
    {
        // Hub for broadcasting transfer activity events
    }
}

