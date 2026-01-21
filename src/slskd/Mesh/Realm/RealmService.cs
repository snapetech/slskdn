// <copyright file="RealmService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Service for managing realm operations and isolation.
    /// </summary>
    /// <remarks>
    ///     T-REALM-01: RealmConfig & RealmID Plumbing.
    ///     Manages realm identity, mesh isolation, and governance scoping.
    /// </remarks>
    public sealed class RealmService : IRealmService, IDisposable
    {
        private readonly IOptionsMonitor<RealmConfig> _realmConfig;
        private readonly ILogger<RealmService> _logger;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _isInitialized;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmService"/> class.
        /// </summary>
        /// <param name="realmConfig">The realm configuration.</param>
        /// <param name="logger">The logger.</param>
        public RealmService(
            IOptionsMonitor<RealmConfig> realmConfig,
            ILogger<RealmService> logger)
        {
            _realmConfig = realmConfig ?? throw new ArgumentNullException(nameof(realmConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Gets the current realm identifier.
        /// </summary>
        public string RealmId => _realmConfig.CurrentValue.Id;

        /// <summary>
        ///     Gets the current realm ID (implements IRealmService).
        /// </summary>
        public string CurrentRealmId => RealmId;

        /// <summary>
        ///     Determines whether a peer is allowed in the current realm (implements IRealmService).
        /// </summary>
        /// <param name="peerId">The peer ID to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the peer is allowed.</returns>
        public Task<bool> IsPeerAllowedInRealmAsync(string peerId, CancellationToken cancellationToken = default)
        {
            // For now, allow all peers. In the future, this could check governance rules.
            return Task.FromResult(true);
        }

        /// <summary>
        ///     Gets the namespace salt for this realm.
        /// </summary>
        /// <remarks>
        ///     T-REALM-01: Used for mesh/DHT overlay namespace isolation.
        /// </remarks>
        public byte[] NamespaceSalt => _realmConfig.CurrentValue.GetNamespaceSalt();

        /// <summary>
        ///     Initializes the realm service and validates configuration.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _initializationLock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                {
                    return;
                }

                var config = _realmConfig.CurrentValue;

                // Validate configuration
                var validationErrors = config.Validate().ToList();
                if (validationErrors.Any())
                {
                    var errorMessages = string.Join("; ", validationErrors.Select(e => e.ErrorMessage));
                    throw new InvalidOperationException($"Realm configuration is invalid: {errorMessages}");
                }

                // Warn about generic IDs
                var genericIds = new[] { "default", "realm", "main", "test", "dev", "prod" };
                if (genericIds.Contains(config.Id.ToLowerInvariant()))
                {
                    _logger.LogWarning(
                        "[Realm] Using generic realm ID '{RealmId}'. Consider using a more specific identifier for better isolation.",
                        config.Id);
                }

                _logger.LogInformation(
                    "[Realm] Initialized realm '{RealmId}' with {GovernanceRoots} governance roots and {BootstrapNodes} bootstrap nodes",
                    config.Id,
                    config.GovernanceRoots.Length,
                    config.BootstrapNodes.Length);

                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        ///     Checks if the given realm ID matches this realm.
        /// </summary>
        /// <param name="realmId">The realm ID to check.</param>
        /// <returns>True if the realm IDs match.</returns>
        public bool IsSameRealm(string realmId)
        {
            return string.Equals(RealmId, realmId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Checks if the given governance root is trusted for this realm.
        /// </summary>
        /// <param name="governanceRoot">The governance root to check.</param>
        /// <returns>True if the root is trusted.</returns>
        public bool IsTrustedGovernanceRoot(string governanceRoot)
        {
            return _realmConfig.CurrentValue.IsTrustedGovernanceRoot(governanceRoot);
        }

        /// <summary>
        ///     Gets the bootstrap nodes for this realm.
        /// </summary>
        /// <returns>The list of bootstrap node addresses.</returns>
        public IReadOnlyList<string> GetBootstrapNodes()
        {
            return _realmConfig.CurrentValue.BootstrapNodes;
        }

        /// <summary>
        ///     Gets the realm policies.
        /// </summary>
        /// <returns>The realm policies.</returns>
        public RealmPolicies GetPolicies()
        {
            return _realmConfig.CurrentValue.Policies;
        }

        /// <summary>
        ///     Creates a realm-scoped identifier.
        /// </summary>
        /// <param name="identifier">The base identifier.</param>
        /// <returns>A realm-scoped identifier.</returns>
        /// <remarks>
        ///     T-REALM-01: Used for scoping governance docs, gossip feeds, etc.
        ///     Format: "realm:{realmId}:{identifier}"
        /// </remarks>
        public string CreateRealmScopedId(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));
            }

            return $"realm:{RealmId}:{identifier}";
        }

        /// <summary>
        ///     Parses a realm-scoped identifier.
        /// </summary>
        /// <param name="scopedId">The scoped identifier.</param>
        /// <param name="realmId">The extracted realm ID.</param>
        /// <param name="identifier">The extracted base identifier.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseRealmScopedId(string scopedId, out string realmId, out string identifier)
        {
            realmId = string.Empty;
            identifier = string.Empty;

            if (string.IsNullOrWhiteSpace(scopedId))
            {
                return false;
            }

            var parts = scopedId.Split(':', 3);
            if (parts.Length != 3 || parts[0] != "realm")
            {
                return false;
            }

            realmId = parts[1];
            identifier = parts[2];
            return true;
        }

        /// <summary>
        ///     Checks if the given scoped identifier belongs to this realm.
        /// </summary>
        /// <param name="scopedId">The scoped identifier.</param>
        /// <returns>True if the identifier belongs to this realm.</returns>
        public bool IsRealmScopedId(string scopedId)
        {
            if (!TryParseRealmScopedId(scopedId, out var realmId, out _))
            {
                return false;
            }

            return IsSameRealm(realmId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _initializationLock.Dispose();
            _disposed = true;
        }
    }
}

