// <copyright file="IRealmService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.Realm
{
    /// <summary>
    /// Interface for realm management services.
    /// </summary>
    public interface IRealmService
    {
        /// <summary>
        /// Gets the current realm ID.
        /// </summary>
        string CurrentRealmId { get; }

        /// <summary>
        /// Determines whether a peer is allowed in the current realm.
        /// </summary>
        /// <param name="peerId">The peer ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the peer is allowed; otherwise false.</returns>
        Task<bool> IsPeerAllowedInRealmAsync(string peerId, CancellationToken cancellationToken = default);
    }
}
