// <copyright file="IGossipService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Gossip
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for gossip-based information dissemination.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Base interface for gossip operations.
    ///     Extended by IRealmAwareGossipService for realm-specific gossip.
    /// </remarks>
    public interface IGossipService
    {
        /// <summary>
        ///     Publishes a gossip message to the network.
        /// </summary>
        /// <param name="message">The gossip message to publish.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishAsync(GossipMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Subscribes to gossip messages of a specific type.
        /// </summary>
        /// <param name="messageType">The message type to subscribe to.</param>
        /// <param name="handler">The message handler.</param>
        /// <returns>A subscription that can be disposed to unsubscribe.</returns>
        IDisposable Subscribe(string messageType, IGossipMessageHandler handler);
    }

    /// <summary>
    ///     Extended interface for realm-aware gossip operations.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Realm-aware extension that tags messages with realm IDs
    ///     and filters inbound messages based on realm configuration.
    /// </remarks>
    public interface IRealmAwareGossipService : IGossipService
    {
        /// <summary>
        ///     Publishes a gossip message for a specific realm.
        /// </summary>
        /// <param name="message">The gossip message to publish.</param>
        /// <param name="realmId">The realm ID to publish to.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishForRealmAsync(GossipMessage message, string realmId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Subscribes to gossip messages for a specific realm.
        /// </summary>
        /// <param name="messageType">The message type to subscribe to.</param>
        /// <param name="realmId">The realm ID to subscribe to.</param>
        /// <param name="handler">The message handler.</param>
        /// <returns>A subscription that can be disposed to unsubscribe.</returns>
        IDisposable SubscribeForRealm(string messageType, string realmId, IGossipMessageHandler handler);
    }

    /// <summary>
    ///     Handler interface for gossip messages.
    /// </summary>
    public interface IGossipMessageHandler
    {
        /// <summary>
        ///     Handles an incoming gossip message.
        /// </summary>
        /// <param name="message">The gossip message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HandleAsync(GossipMessage message, CancellationToken cancellationToken = default);
    }
}

