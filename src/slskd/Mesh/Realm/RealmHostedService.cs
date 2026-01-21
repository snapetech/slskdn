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
    public sealed class RealmHostedService : IHostedService
    {
        private readonly RealmService _realmService;
        private readonly Microsoft.Extensions.Logging.ILogger<RealmHostedService> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmHostedService"/> class.
        /// </summary>
        /// <param name="realmService">The realm service.</param>
        /// <param name="logger">The logger.</param>
        public RealmHostedService(
            RealmService realmService,
            Microsoft.Extensions.Logging.ILogger<RealmHostedService> logger)
        {
            _realmService = realmService ?? throw new ArgumentNullException(nameof(realmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Initialize in background to avoid blocking other hosted services
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("[RealmHostedService] Starting realm initialization...");
                    await _realmService.InitializeAsync(cancellationToken);
                    _logger.LogInformation("[RealmHostedService] Realm initialization complete.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RealmHostedService] FAILED to initialize realm");
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


