// <copyright file="RealmHostedService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    ///     Hosted service for realm initialization.
    /// </summary>
    /// <remarks>
    ///     T-REALM-01: Ensures realm service is initialized during application startup.
    /// </remarks>
    public sealed class RealmHostedService : IHostedService, IDisposable
    {
        private readonly RealmService _realmService;
        private readonly Microsoft.Extensions.Options.IOptionsMonitor<MultiRealmConfig> _multiRealmConfig;
        private readonly Microsoft.Extensions.Logging.ILogger<RealmHostedService> _logger;
        private CancellationTokenSource? _initializationCts;
        private Task? _initializationTask;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmHostedService"/> class.
        /// </summary>
        /// <param name="realmService">The realm service.</param>
        /// <param name="multiRealmConfig">The multi-realm configuration.</param>
        /// <param name="logger">The logger.</param>
        public RealmHostedService(
            RealmService realmService,
            Microsoft.Extensions.Options.IOptionsMonitor<MultiRealmConfig> multiRealmConfig,
            Microsoft.Extensions.Logging.ILogger<RealmHostedService> logger)
        {
            _realmService = realmService ?? throw new ArgumentNullException(nameof(realmService));
            _multiRealmConfig = multiRealmConfig ?? throw new ArgumentNullException(nameof(multiRealmConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Skip single realm initialization if multi-realm is configured
            var multiRealms = _multiRealmConfig.CurrentValue.Realms ?? Array.Empty<RealmConfig>();
            if (multiRealms.Length > 0)
            {
                _logger.LogDebug("[RealmHostedService] Skipping single realm initialization - multi-realm is configured with {Count} realms", multiRealms.Length);
                return Task.CompletedTask;
            }

            // Initialize in background to avoid blocking other hosted services
            _initializationCts?.Cancel();
            _initializationCts?.Dispose();
            var initializationCts = new CancellationTokenSource();
            _initializationCts = initializationCts;
            _initializationTask = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("[RealmHostedService] Starting realm initialization...");
                    await _realmService.InitializeAsync(initializationCts.Token).ConfigureAwait(false);
                    _logger.LogInformation("[RealmHostedService] Realm initialization complete.");
                }
                catch (OperationCanceledException) when (initializationCts.IsCancellationRequested)
                {
                    _logger.LogInformation("[RealmHostedService] Realm initialization cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RealmHostedService] FAILED to initialize realm");
                }
            }, CancellationToken.None);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _initializationCts?.Cancel();

            if (_initializationTask != null)
            {
                try
                {
                    await _initializationTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }

            _initializationTask = null;
            _initializationCts?.Dispose();
            _initializationCts = null;
        }

        public void Dispose()
        {
            _initializationCts?.Cancel();
            _initializationCts?.Dispose();
            _initializationCts = null;
            GC.SuppressFinalize(this);
        }
    }
}
