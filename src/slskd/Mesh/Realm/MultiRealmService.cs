// <copyright file="MultiRealmService.cs" company="slskdN Team">
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
    ///     Service for managing multi-realm operations and bridge enforcement.
    /// </summary>
    /// <remarks>
    ///     T-REALM-02: MultiRealmConfig & Bridge Skeleton.
    ///     Coordinates multiple realm services and enforces cross-realm flow policies.
    /// </remarks>
    public sealed class MultiRealmService : IDisposable
    {
        private readonly IOptionsMonitor<MultiRealmConfig> _multiRealmConfig;
        private readonly ILogger<MultiRealmService> _logger;
        private readonly Dictionary<string, RealmService> _realmServices = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _isInitialized;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MultiRealmService"/> class.
        /// </summary>
        /// <param name="multiRealmConfig">The multi-realm configuration.</param>
        /// <param name="logger">The logger.</param>
        public MultiRealmService(
            IOptionsMonitor<MultiRealmConfig> multiRealmConfig,
            ILogger<MultiRealmService> logger)
        {
            _multiRealmConfig = multiRealmConfig ?? throw new ArgumentNullException(nameof(multiRealmConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Gets all configured realm IDs.
        /// </summary>
        public IReadOnlySet<string> RealmIds => _multiRealmConfig.CurrentValue.RealmIds;

        /// <summary>
        ///     Gets a value indicating whether bridging is enabled.
        /// </summary>
        public bool IsBridgingEnabled => _multiRealmConfig.CurrentValue.IsBridgingEnabled;

        /// <summary>
        ///     Initializes the multi-realm service and all realm services.
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

                var config = _multiRealmConfig.CurrentValue;

                // Validate configuration
                var validationErrors = config.Validate().ToList();
                if (validationErrors.Any())
                {
                    var errorMessages = string.Join("; ", validationErrors.Select(e => e.ErrorMessage));
                    throw new InvalidOperationException($"Multi-realm configuration is invalid: {errorMessages}");
                }

                _logger.LogInformation(
                    "[MultiRealm] Initializing {Count} realms with bridging {BridgingStatus}",
                    config.Realms.Length,
                    config.IsBridgingEnabled ? "enabled" : "disabled");

                // Initialize each realm service
                foreach (var realmConfig in config.Realms)
                {
                    try
                    {
                        var realmService = CreateRealmService(realmConfig);
                        await realmService.InitializeAsync(cancellationToken);

                        _realmServices[realmConfig.Id] = realmService;

                        _logger.LogInformation(
                            "[MultiRealm] Initialized realm '{RealmId}' with {GovernanceRoots} governance roots",
                            realmConfig.Id,
                            realmConfig.GovernanceRoots.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MultiRealm] Failed to initialize realm '{RealmId}'", realmConfig.Id);
                        throw;
                    }
                }

                _isInitialized = true;

                _logger.LogInformation(
                    "[MultiRealm] Successfully initialized {Count} realms",
                    _realmServices.Count);
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        ///     Gets the realm service for a specific realm.
        /// </summary>
        /// <param name="realmId">The realm ID.</param>
        /// <returns>The realm service, or null if not found.</returns>
        public RealmService? GetRealmService(string realmId)
        {
            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            _realmServices.TryGetValue(realmId, out var service);
            return service;
        }

        /// <summary>
        ///     Gets all realm services.
        /// </summary>
        /// <returns>A read-only dictionary of realm services.</returns>
        public IReadOnlyDictionary<string, RealmService> GetAllRealmServices()
        {
            return _realmServices;
        }

        /// <summary>
        ///     Checks if a flow is allowed between realms.
        /// </summary>
        /// <param name="flow">The flow to check (e.g., "governance:read").</param>
        /// <returns>True if the flow is allowed.</returns>
        /// <remarks>
        ///     T-REALM-02: Bridge enforcement - only allowed flows can cross realm boundaries.
        /// </remarks>
        public bool IsFlowAllowed(string flow)
        {
            if (string.IsNullOrWhiteSpace(flow))
            {
                return false;
            }

            return _multiRealmConfig.CurrentValue.IsFlowAllowed(flow);
        }

        /// <summary>
        ///     Creates a bridge-aware scoped identifier across realms.
        /// </summary>
        /// <param name="realmId">The source realm ID.</param>
        /// <param name="identifier">The base identifier.</param>
        /// <returns>A bridge-scoped identifier, or null if bridging not allowed.</returns>
        /// <remarks>
        ///     T-REALM-02: Only creates cross-realm scoped IDs if bridging is enabled.
        ///     Format: "bridge:{sourceRealm}:{targetRealm}:{identifier}"
        /// </remarks>
        public string? CreateBridgeScopedId(string realmId, string identifier)
        {
            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));
            }

            // Only allow bridge-scoped IDs if bridging is enabled
            if (!IsBridgingEnabled)
            {
                return null;
            }

            // For now, we use a simplified format. In a full implementation,
            // this would need to specify source and target realms.
            // Format: "bridge:{realmId}:{identifier}"
            return $"bridge:{realmId}:{identifier}";
        }

        /// <summary>
        ///     Validates that a cross-realm operation is permitted by bridge policies.
        /// </summary>
        /// <param name="sourceRealmId">The source realm ID.</param>
        /// <param name="targetRealmId">The target realm ID.</param>
        /// <param name="flow">The flow being attempted.</param>
        /// <returns>True if the operation is permitted.</returns>
        /// <remarks>
        ///     T-REALM-02: Bridge enforcement - validates cross-realm operations against policies.
        /// </remarks>
        public bool IsCrossRealmOperationPermitted(string sourceRealmId, string targetRealmId, string flow)
        {
            if (string.IsNullOrWhiteSpace(sourceRealmId))
            {
                throw new ArgumentException("Source realm ID cannot be null or empty.", nameof(sourceRealmId));
            }

            if (string.IsNullOrWhiteSpace(targetRealmId))
            {
                throw new ArgumentException("Target realm ID cannot be null or empty.", nameof(targetRealmId));
            }

            if (string.IsNullOrWhiteSpace(flow))
            {
                throw new ArgumentException("Flow cannot be null or empty.", nameof(flow));
            }

            // Same realm operations are always allowed
            if (string.Equals(sourceRealmId, targetRealmId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Cross-realm operations require bridging to be enabled and flow to be allowed
            if (!IsBridgingEnabled)
            {
                _logger.LogDebug(
                    "[MultiRealm] Cross-realm operation blocked - bridging disabled. " +
                    "Source: {SourceRealm}, Target: {TargetRealm}, Flow: {Flow}",
                    sourceRealmId, targetRealmId, flow);
                return false;
            }

            if (!IsFlowAllowed(flow))
            {
                _logger.LogWarning(
                    "[MultiRealm] Cross-realm operation blocked - flow not allowed. " +
                    "Source: {SourceRealm}, Target: {TargetRealm}, Flow: {Flow}",
                    sourceRealmId, targetRealmId, flow);
                return false;
            }

            _logger.LogDebug(
                "[MultiRealm] Cross-realm operation permitted. " +
                "Source: {SourceRealm}, Target: {TargetRealm}, Flow: {Flow}",
                sourceRealmId, targetRealmId, flow);

            return true;
        }

        /// <summary>
        ///     Gets governance roots that are trusted across realms.
        /// </summary>
        /// <returns>A set of governance roots trusted in any realm.</returns>
        /// <remarks>
        ///     T-REALM-02: Returns governance roots from all realms.
        ///     In a real implementation, this might have more complex cross-realm trust logic.
        /// </remarks>
        public IReadOnlySet<string> GetCrossRealmGovernanceRoots()
        {
            var allRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var realmService in _realmServices.Values)
            {
                // In a full implementation, we'd check bridge policies here
                // For now, collect all governance roots from all realms
                var realmConfig = _multiRealmConfig.CurrentValue.GetRealm(realmService.RealmId);
                if (realmConfig != null)
                {
                    allRoots.UnionWith(realmConfig.GovernanceRoots);
                }
            }

            return allRoots;
        }

        /// <summary>
        ///     Creates a realm service for a specific realm configuration.
        /// </summary>
        /// <param name="realmConfig">The realm configuration.</param>
        /// <returns>A new realm service instance.</returns>
        /// <remarks>
        ///     In a production implementation, this would be handled by DI container
        ///     with proper service registration per realm.
        /// </remarks>
        private static RealmService CreateRealmService(RealmConfig realmConfig)
        {
            // For now, create realm services manually
            // In a full implementation, this would use IServiceProvider to resolve
            // properly configured services for each realm

            // Create mock implementations for the realm service dependencies
            // This is a temporary implementation - proper DI would be better
            var optionsMonitor = new RealmConfigOptionsMonitor(realmConfig);
            var logger = new NoOpLogger<RealmService>();

            return new RealmService(optionsMonitor, logger);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var service in _realmServices.Values)
            {
                service.Dispose();
            }

            _realmServices.Clear();
            _initializationLock.Dispose();
            _disposed = true;
        }

        // Temporary implementations - in production these would be proper services

        private class RealmConfigOptionsMonitor : IOptionsMonitor<RealmConfig>
        {
            public RealmConfigOptionsMonitor(RealmConfig currentValue)
            {
                CurrentValue = currentValue ?? throw new ArgumentNullException(nameof(currentValue));
            }

            public RealmConfig CurrentValue { get; }
            public RealmConfig Get(string name) => CurrentValue;
            public IDisposable OnChange(Action<RealmConfig, string> listener) => new NoOpDisposable();
        }

        private class NoOpLogger<T> : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
