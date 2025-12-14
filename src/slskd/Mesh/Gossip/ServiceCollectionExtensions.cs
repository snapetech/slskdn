// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Gossip
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Extension methods for registering gossip services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds gossip services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddGossipServices(this IServiceCollection services)
        {
            services.AddSingleton<IRealmAwareGossipService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RealmAwareGossipService>>();
                var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<slskd.Mesh.Realm.MultiRealmConfig>>().CurrentValue;

                // Multi-realm mode whenever more than one realm is configured
                if (cfg?.Realms != null && cfg.Realms.Length > 1)
                {
                    var multiRealm = sp.GetRequiredService<slskd.Mesh.Realm.MultiRealmService>();
                    return new RealmAwareGossipService(multiRealm, logger);
                }

                var realmService = sp.GetRequiredService<slskd.Mesh.Realm.IRealmService>();
                return new RealmAwareGossipService(realmService, logger);
            });

            services.AddSingleton<IGossipService>(sp => sp.GetRequiredService<IRealmAwareGossipService>());
            return services;
        }
    }
}


