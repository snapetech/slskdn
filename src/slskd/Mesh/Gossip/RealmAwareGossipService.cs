// <copyright file="RealmAwareGossipService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Gossip
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Mesh.Realm;

    /// <summary>
    ///     Realm-aware gossip service implementation.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Tags outgoing gossip feeds with realm IDs and filters
    ///     inbound feeds based on realm configuration. Prevents cross-realm
    ///     gossip contamination while enabling controlled multi-realm gossip.
    /// </remarks>
    public sealed class RealmAwareGossipService : IRealmAwareGossipService, IDisposable
    {
        private readonly IRealmService _realmService;
        private readonly ILogger<RealmAwareGossipService> _logger;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<IGossipMessageHandler>>> _realmSubscriptions
            = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<IGossipMessageHandler>> _globalSubscriptions
            = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmAwareGossipService"/> class.
        /// </summary>
        /// <param name="realmService">The realm service.</param>
        /// <param name="logger">The logger.</param>
        public RealmAwareGossipService(
            IRealmService realmService,
            ILogger<RealmAwareGossipService> logger)
        {
            _realmService = realmService ?? throw new ArgumentNullException(nameof(realmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmAwareGossipService"/> class for multi-realm support.
        /// </summary>
        /// <param name="multiRealmService">The multi-realm service.</param>
        /// <param name="logger">The logger.</param>
        public RealmAwareGossipService(
            MultiRealmService multiRealmService,
            ILogger<RealmAwareGossipService> logger)
        {
            if (multiRealmService == null)
            {
                throw new ArgumentNullException(nameof(multiRealmService));
            }

            // For multi-realm scenarios, we create a composite realm service
            _realmService = new CompositeRealmService(multiRealmService);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task PublishAsync(GossipMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // For backwards compatibility, publish without realm context
            if (string.IsNullOrEmpty(message.RealmId))
            {
                await PublishForRealmAsync(message, "default", cancellationToken);
                return;
            }

            await PublishForRealmAsync(message, message.RealmId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task PublishForRealmAsync(
            GossipMessage message,
            string realmId,
            CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            // Tag message with realm ID
            message.RealmId = realmId;

            // Check if message can still be forwarded
            if (!message.CanForward())
            {
                _logger.LogDebug("[Gossip] Message '{MessageId}' cannot be forwarded (TTL exceeded or max hops reached)", message.Id);
                return;
            }

            _logger.LogDebug(
                "[Gossip] Publishing message '{MessageId}' of type '{Type}' for realm '{RealmId}'",
                message.Id, message.Type, realmId);

            // Forward to subscribers for this realm and message type
            await ForwardToRealmSubscribersAsync(message, realmId, cancellationToken);

            // In a real implementation, this would also forward to other mesh nodes
            // For now, we just handle local subscriptions
        }

        /// <inheritdoc/>
        public IDisposable Subscribe(string messageType, IGossipMessageHandler handler)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                throw new ArgumentException("Message type cannot be null or empty.", nameof(messageType));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var handlers = _globalSubscriptions.GetOrAdd(messageType, _ => new List<IGossipMessageHandler>());
            lock (handlers)
            {
                handlers.Add(handler);
            }

            return new Subscription(() => Unsubscribe(messageType, handler));
        }

        /// <inheritdoc/>
        public IDisposable SubscribeForRealm(string messageType, string realmId, IGossipMessageHandler handler)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                throw new ArgumentException("Message type cannot be null or empty.", nameof(messageType));
            }

            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var realmSubscriptions = _realmSubscriptions.GetOrAdd(realmId, _ => new ConcurrentDictionary<string, List<IGossipMessageHandler>>(StringComparer.OrdinalIgnoreCase));
            var handlers = realmSubscriptions.GetOrAdd(messageType, _ => new List<IGossipMessageHandler>());

            lock (handlers)
            {
                handlers.Add(handler);
            }

            return new Subscription(() => UnsubscribeFromRealm(messageType, realmId, handler));
        }

        /// <summary>
        ///     Handles an incoming gossip message from the network.
        /// </summary>
        /// <param name="message">The incoming gossip message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     T-REALM-03: Filters inbound messages based on realm configuration.
        ///     Only accepts messages for known realms and valid message types.
        /// </remarks>
        public async Task HandleIncomingMessageAsync(GossipMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var realmId = message.RealmId;

            // Validate realm association
            if (string.IsNullOrWhiteSpace(realmId))
            {
                _logger.LogWarning("[Gossip] Received message '{MessageId}' without realm ID - ignoring", message.Id);
                return;
            }

            // Check if we know about this realm
            if (!_realmService.IsSameRealm(realmId))
            {
                _logger.LogWarning(
                    "[Gossip] Received message '{MessageId}' for unknown realm '{RealmId}' - ignoring",
                    message.Id, realmId);
                return;
            }

            // Check message validity
            if (!message.CanForward())
            {
                _logger.LogDebug("[Gossip] Received expired or invalid message '{MessageId}' - ignoring", message.Id);
                return;
            }

            _logger.LogDebug(
                "[Gossip] Processing incoming message '{MessageId}' of type '{Type}' for realm '{RealmId}'",
                message.Id, message.Type, realmId);

            // Forward to realm-specific subscribers
            await ForwardToRealmSubscribersAsync(message, realmId, cancellationToken);

            // Create forwarded copy and continue gossip dissemination
            var forwardedMessage = message.CreateForwardedCopy();
            if (forwardedMessage.CanForward())
            {
                // In a real implementation, forward to neighboring nodes
                _logger.LogDebug("[Gossip] Would forward message '{MessageId}' to neighboring nodes", message.Id);
            }
        }

        private async Task ForwardToRealmSubscribersAsync(GossipMessage message, string realmId, CancellationToken cancellationToken)
        {
            // Get realm-specific subscribers
            if (_realmSubscriptions.TryGetValue(realmId, out var realmSubscriptions) &&
                realmSubscriptions.TryGetValue(message.Type, out var realmHandlers))
            {
                await NotifyHandlersAsync(realmHandlers, message, cancellationToken);
            }

            // Also check global subscribers (for backwards compatibility)
            if (_globalSubscriptions.TryGetValue(message.Type, out var globalHandlers))
            {
                await NotifyHandlersAsync(globalHandlers, message, cancellationToken);
            }
        }

        private static async Task NotifyHandlersAsync(List<IGossipMessageHandler> handlers, GossipMessage message, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            lock (handlers)
            {
                foreach (var handler in handlers.ToList()) // Create a copy to avoid modification during iteration
                {
                    tasks.Add(handler.HandleAsync(message, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);
        }

        private void Unsubscribe(string messageType, IGossipMessageHandler handler)
        {
            if (_globalSubscriptions.TryGetValue(messageType, out var handlers))
            {
                lock (handlers)
                {
                    handlers.Remove(handler);
                }
            }
        }

        private void UnsubscribeFromRealm(string messageType, string realmId, IGossipMessageHandler handler)
        {
            if (_realmSubscriptions.TryGetValue(realmId, out var realmSubscriptions) &&
                realmSubscriptions.TryGetValue(messageType, out var handlers))
            {
                lock (handlers)
                {
                    handlers.Remove(handler);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _realmSubscriptions.Clear();
            _globalSubscriptions.Clear();
            _disposed = true;
        }

        // Composite realm service for multi-realm scenarios
        private class CompositeRealmService : IRealmService
        {
            private readonly MultiRealmService _multiRealmService;

            public CompositeRealmService(MultiRealmService multiRealmService)
            {
                _multiRealmService = multiRealmService;
            }

            public string CurrentRealmId => throw new NotSupportedException("Composite realm service doesn't have a single realm ID");

            public string RealmId => throw new NotSupportedException("Composite realm service doesn't have a single realm ID");

            public byte[] NamespaceSalt => throw new NotSupportedException("Composite realm service doesn't have a single namespace salt");

            public Task InitializeAsync(CancellationToken cancellationToken = default)
                => _multiRealmService.InitializeAsync(cancellationToken);

            public bool IsSameRealm(string realmId)
            {
                return _multiRealmService.RealmIds.Contains(realmId);
            }

            public bool IsTrustedGovernanceRoot(string governanceRoot)
            {
                return _multiRealmService.GetCrossRealmGovernanceRoots().Contains(governanceRoot);
            }

            public string CreateRealmScopedId(string identifier)
            {
                throw new NotSupportedException();
            }

            public bool TryParseRealmScopedId(string scopedId, out string realmId, out string identifier)
            {
                return RealmService.TryParseRealmScopedId(scopedId, out realmId, out identifier);
            }

            public bool IsRealmScopedId(string scopedId)
            {
                if (!TryParseRealmScopedId(scopedId, out var realmId, out _))
                {
                    return false;
                }

                return IsSameRealm(realmId);
            }

            public Task<bool> IsPeerAllowedInRealmAsync(string peerId, CancellationToken cancellationToken = default)
            {
                // For composite realm service, allow all peers by default
                // Individual realm services handle their own peer filtering
                return Task.FromResult(true);
            }
        }

        private class Subscription : IDisposable
        {
            private readonly Action _unsubscribe;

            public Subscription(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                _unsubscribe();
            }
        }
    }
}
