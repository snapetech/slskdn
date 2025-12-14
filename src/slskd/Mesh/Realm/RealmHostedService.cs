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

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmHostedService"/> class.
        /// </summary>
        /// <param name="realmService">The realm service.</param>
        public RealmHostedService(RealmService realmService)
        {
            _realmService = realmService ?? throw new ArgumentNullException(nameof(realmService));
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _realmService.InitializeAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            return Task.CompletedTask;
        }
    }
}


