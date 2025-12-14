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

        /// <summary>
        ///     Initializes a new instance of the <see cref="MultiRealmHostedService"/> class.
        /// </summary>
        /// <param name="multiRealmService">The multi-realm service.</param>
        public MultiRealmHostedService(MultiRealmService multiRealmService)
        {
            _multiRealmService = multiRealmService ?? throw new ArgumentNullException(nameof(multiRealmService));
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _multiRealmService.InitializeAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            return Task.CompletedTask;
        }
    }
}
