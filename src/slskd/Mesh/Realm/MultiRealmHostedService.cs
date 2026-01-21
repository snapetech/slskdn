// <copyright file="MultiRealmHostedService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    ///     Hosted service for multi-realm initialization.
    /// </summary>
    /// <remarks>
    ///     T-REALM-02: Ensures multi-realm service is initialized during application startup.
    /// </remarks>
    public sealed class MultiRealmHostedService : IHostedService
    {
        private readonly MultiRealmService _multiRealmService;
        private readonly Microsoft.Extensions.Options.IOptionsMonitor<MultiRealmConfig> _config;
        private readonly Microsoft.Extensions.Logging.ILogger<MultiRealmHostedService> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MultiRealmHostedService"/> class.
        /// </summary>
        /// <param name="multiRealmService">The multi-realm service.</param>
        /// <param name="config">The multi-realm configuration.</param>
        /// <param name="logger">The logger.</param>
        public MultiRealmHostedService(
            MultiRealmService multiRealmService,
            Microsoft.Extensions.Options.IOptionsMonitor<MultiRealmConfig> config,
            Microsoft.Extensions.Logging.ILogger<MultiRealmHostedService> logger)
        {
            _multiRealmService = multiRealmService ?? throw new ArgumentNullException(nameof(multiRealmService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Only initialize if we have multiple realms configured
            var realms = _config.CurrentValue.Realms ?? Array.Empty<RealmConfig>();
            if (realms.Length == 0)
            {
                _logger.LogDebug("[MultiRealmHostedService] Skipping initialization - no realms configured");
                return Task.CompletedTask;
            }

            // Initialize in background to avoid blocking other hosted services
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("[MultiRealmHostedService] Initializing multi-realm service with {Count} realms", realms.Length);
                    await _multiRealmService.InitializeAsync(cancellationToken);
                    _logger.LogInformation("[MultiRealmHostedService] Multi-realm initialization complete.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MultiRealmHostedService] FAILED to initialize multi-realm service");
                }
            }, cancellationToken);
            
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            return Task.CompletedTask;
        }
    }
}


